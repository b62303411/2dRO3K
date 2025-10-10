// Assets/Scripts/ChunkedTilemap/Editor/GridSubdivider.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class GridSubdivider : EditorWindow
{
    [Header("Source Grid")]
    public Grid sourceGrid;

    [Header("Subdivision")]
    [Min(2)] public int subGridSize = 128;    // width/height in cells per sub-grid
    //[Min(0)] public int overlap = 1;          // one-tile overlap ring

    [Header("Options")]
    public bool includeInactive = true;
    public bool copyTileFlags = true;         // copy color/transform/flags per cell
    public bool disableSourceAfter = false;   // disable original Tilemaps after copy
    public string subGridPrefix = "SubGrid_";

    [MenuItem("Tools/Subdivide Grid → Multiple Grids")]
    public static void Open() => GetWindow<GridSubdivider>(true, "Grid Subdivider");

    void OnGUI()
    {
        sourceGrid        = (Grid)EditorGUILayout.ObjectField("Source Grid", sourceGrid, typeof(Grid), true);
        subGridSize       = EditorGUILayout.IntField("SubGrid Size (cells)", Mathf.Max(2, subGridSize));
        //overlap           = EditorGUILayout.IntField("Overlap (cells)", Mathf.Max(0, overlap));
        includeInactive   = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        copyTileFlags     = EditorGUILayout.Toggle("Copy Tile Properties", copyTileFlags);
        disableSourceAfter= EditorGUILayout.Toggle("Disable Source After", disableSourceAfter);
        subGridPrefix     = EditorGUILayout.TextField("SubGrid Prefix", subGridPrefix);

        using (new EditorGUI.DisabledScope(sourceGrid == null))
            if (GUILayout.Button("Subdivide Now")) Subdivide();

        EditorGUILayout.HelpBox(
            "Creates multiple child Grids (no runtime deps). For each legacy Tilemap, makes a matching Tilemap under each sub-Grid and copies tiles. " +
            "Uses SetTilesBlock for speed, plus optional per-cell pass to copy color/transform/flags.",
            MessageType.Info);
    }
    
    public static void SwapTiles(Tilemap tm, TileBase oldTile, TileBase newTile)
    {
        //tm.BeginTilemapEdit(); // si tu as une extension; sinon enlève
        var bounds = tm.cellBounds;
        for (int y = bounds.yMin; y < bounds.yMax; y++)
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            var p = new Vector3Int(x,y,0);
            if (tm.GetTile(p) == oldTile) tm.SetTile(p, newTile);
        }
        tm.CompressBounds();
        tm.RefreshAllTiles();
    }
    
    void Subdivide()
    {
        if (!sourceGrid) { Debug.LogError("No source Grid."); return; }
        Undo.RegisterFullObjectHierarchyUndo(sourceGrid.gameObject, "Subdivide Grid");

        // Collect legacy Tilemaps
        var legacyMaps = sourceGrid.GetComponentsInChildren<Tilemap>(includeInactive);
        var renderers  = new Dictionary<Tilemap, TilemapRenderer>();
        var boundsList = new Dictionary<Tilemap, BoundsInt>();
        foreach (var tm in legacyMaps)
        {
            var r = tm.GetComponent<TilemapRenderer>();
            if (!r) continue; // skip helpers
            tm.CompressBounds();
            renderers[tm]  = r;
            boundsList[tm] = tm.cellBounds;
        }
        if (renderers.Count == 0) { Debug.LogWarning("No renderable Tilemaps under the Grid."); return; }

        // Global bounds
        bool hasAny = false;
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;

        foreach (var tm in legacyMaps)   // or mapToLayer.Keys if you filtered
        {
            // use Tilemap (source of truth), NOT the renderer
            tm.CompressBounds();
            var b = tm.cellBounds;
            if (b.size.x == 0 || b.size.y == 0) continue;

            if (b.xMin < xMin) xMin = b.xMin;
            if (b.yMin < yMin) yMin = b.yMin;
            if (b.xMax > xMax) xMax = b.xMax;
            if (b.yMax > yMax) yMax = b.yMax;

            hasAny = true;
        }

        if (!hasAny) {
            Debug.LogWarning("All Tilemaps are empty.");
            return;
        }

        int s = subGridSize;
        int gxMin = Mathf.FloorToInt(xMin / (float)s);
        int gxMax = Mathf.CeilToInt (xMax / (float)s);
        int gyMin = Mathf.FloorToInt(yMin / (float)s);
        int gyMax = Mathf.CeilToInt (yMax / (float)s);

        int totalX = gxMax - gxMin;
        int totalY = gyMax - gyMin;
        int total   = totalX * totalY * renderers.Count;
        int progress = 0;

        try
        {
            for (int gy = gyMin; gy < gyMax; gy++)
            for (int gx = gxMin; gx < gxMax; gx++)
            {
                // Create sub-Grid
                var subGO = new GameObject($"{subGridPrefix}{gx}_{gy}");
                subGO.transform.SetParent(sourceGrid.transform, false);
                var subGrid = subGO.AddComponent<Grid>();
                subGrid.cellLayout = sourceGrid.cellLayout;
                subGrid.cellSize   = sourceGrid.cellSize;

                // Create matching Tilemaps
                var dstMap = new Dictionary<Tilemap, Tilemap>();
                     foreach (var tm in renderers.Keys)
                {
                    var go = new GameObject(tm.name, typeof(Tilemap), typeof(TilemapRenderer));
                    go.transform.SetParent(subGO.transform, false);
                    var ntm = go.GetComponent<Tilemap>();
                    var nrd = go.GetComponent<TilemapRenderer>();
                    var r   = renderers[tm];

                    // Copy renderer-ish settings
                    nrd.sortingLayerID = r.sortingLayerID;
                    nrd.sortingOrder   = r.sortingOrder;
                    nrd.maskInteraction= r.maskInteraction;
                    nrd.material       = r.sharedMaterial;
                    ntm.tileAnchor     = tm.tileAnchor;

                    dstMap[tm] = ntm;
                }

                // Copy tiles for each legacy Tilemap
                foreach (var tm in renderers.Keys)
                {
                    var srcBounds = boundsList[tm];
                    // bounds for this sub-grid with overlap
                    var subMin = new Vector3Int(gx * s, gy * s, 0);
                    var subBounds = new BoundsInt(
                        subMin ,
                        new Vector3Int(s , s , 1)
                    );
                    var inter = Intersect(subBounds, srcBounds);
                    if (inter.size.x <= 0 || inter.size.y <= 0) { progress++; continue; }

                    var tiles = tm.GetTilesBlock(inter);
                    var dst   = dstMap[tm];

                    // We keep world cell coordinates; each sub-grid has its own Tilemaps at same coords.
                    var local = new BoundsInt(inter.min, inter.size);
                    dst.SetTilesBlock(local, tiles);

                    if (copyTileFlags)
                    {
                        foreach (var pos in local.allPositionsWithin)
                        {
                            var t = tm.GetTile(pos);
                            if (!t) continue;
                            dst.SetTransformMatrix(pos, tm.GetTransformMatrix(pos));
                            dst.SetColor(pos,           tm.GetColor(pos));
                            dst.SetTileFlags(pos,       tm.GetTileFlags(pos));
                        }
                    }

                    progress++;
                    if (EditorUtility.DisplayCancelableProgressBar(
                            "Subdividing Grid", $"SubGrid {gx},{gy} — {tm.name}",
                            progress / (float)total))
                        throw new System.OperationCanceledException();
                }
            }
        }
        catch (System.OperationCanceledException)
        {
            Debug.LogWarning("Operation canceled by user.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (disableSourceAfter)
        {
            foreach (var tm in renderers.Keys) tm.gameObject.SetActive(false);
        }

        Debug.Log($"Subdivide complete: {totalX*totalY} sub-grids created");
    }

    static BoundsInt Intersect(BoundsInt a, BoundsInt b)
    {
        int xMin = Mathf.Max(a.xMin, b.xMin);
        int yMin = Mathf.Max(a.yMin, b.yMin);
        int xMax = Mathf.Min(a.xMax, b.xMax);
        int yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin) return new BoundsInt(0,0,0,0,0,1);
        return new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }
}
#endif
