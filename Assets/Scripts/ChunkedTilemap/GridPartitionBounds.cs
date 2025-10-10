using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Zero sugar-coating: this gives you partition bounding boxes at the GRID level.
/// - Defines a logical chunk grid in *cell space* with optional overlap.
/// - Computes both cell-space BoundsInt and world-space Bounds for each chunk.
/// - Maps any cell/world position to its owning chunk key.
/// - Enumerates chunks overlapping arbitrary cell regions (e.g., Tilemap.cellBounds, brush areas).
/// - Optionally draws gizmos for sanity checks.
/// You still have to hook your load/unload and collider/Nav2D rebuild logic.
/// </summary>
[ExecuteAlways]
public class GridPartitionBounds : MonoBehaviour
{
    [Header("ROOT Grid (parent of your Tilemaps)")]
    public Grid parentGrid;

    [Header("Chunk layout in PARENT cells (width x height)")]
    public Vector2Int chunkSize = new Vector2Int(128, 128);

    [Tooltip("Parent-cell origin of chunk (0,0). Usually Tilemap.cellBounds.min or Vector3Int.zero.")]
    public Vector3Int gridOrigin = Vector3Int.zero;

    [Tooltip("Extra cells around each chunk for cross-partition rules/colliders.")]
    [Min(0)] public int overlap = 1;
    [Header("Tilemap Layers (assign in Inspector)")]
    public List<Tilemap> tilemapLayers = new List<Tilemap>();
    readonly Dictionary<int, Tilemap> layers_dic = new();
    [Header("Debug / Gizmos")]
    public bool drawGizmos = true;
    public Color coreColor = new Color(0f, 1f, 0f, 0.10f);
    public Color overlapColor = new Color(1f, 0.92f, 0.016f, 0.08f);
    public Color borderColor = new Color(0f, 0f, 0f, 0.6f);
    public int gizmoRadiusChunks = 2; // around scene view camera/player
    public BoundsInt Bounds;


    private void Start()
    {
        // Auto-find Grid if not assigned
        if (!parentGrid) parentGrid = GetComponent<Grid>();

        // Auto-populate tilemaps if empty
        if (tilemapLayers.Count == 0)
        {
            tilemapLayers.AddRange(GetComponentsInChildren<Tilemap>());
        }
        Bounds = tilemapLayers[0].cellBounds;
        foreach (Tilemap tm in tilemapLayers) 
        {
            var key =  Animator.StringToHash(tm.gameObject.name);
            layers_dic[key] = tm;
        }
    }



    #region Public API

    /// <summary>
    /// Convert a parent-grid cell coordinate to a chunk key (cx, cy).
    /// Works with negative cells, anchored at gridOrigin.
    /// </summary>
    public Vector2Int CellToChunk(Vector3Int cell)
    {
        var rel = cell - gridOrigin;
        int cx = FloorDiv(rel.x, chunkSize.x);
        int cy = FloorDiv(rel.y, chunkSize.y);
        return new Vector2Int(cx, cy);
    }

    /// <summary>
    /// Convert a world position to a chunk key, using parent grid's WorldToCell.
    /// </summary>
    public Vector2Int WorldToChunk(Vector3 worldPos)
    {
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        var cell = parentGrid ? parentGrid.WorldToCell(worldPos) : Vector3Int.FloorToInt(worldPos);
        return CellToChunk(cell);
    }

    /// <summary>
    /// Get the *core* (non-overlapped) cell bounds of a chunk.
    /// </summary>
    public BoundsInt GetChunkCoreCellBounds(Vector2Int chunkKey)
    {
        var min = new Vector3Int(
            gridOrigin.x + chunkKey.x * chunkSize.x,
            gridOrigin.y + chunkKey.y * chunkSize.y,
            0);
        var size = new Vector3Int(chunkSize.x, chunkSize.y, 1);
        return new BoundsInt(min, size);
    }

    /// <summary>
    /// Get the expanded cell bounds (with overlap margin on all sides).
    /// </summary>
    public BoundsInt GetChunkExpandedCellBounds(Vector2Int chunkKey)
    {
        var core = GetChunkCoreCellBounds(chunkKey);
        var min = new Vector3Int(core.xMin - overlap, core.yMin - overlap, 0);
        var size = new Vector3Int(core.size.x + overlap * 2, core.size.y + overlap * 2, 1);
        return new BoundsInt(min, size);
    }

    /// <summary>
    /// World-space AABB for the expanded bounds (snaps to grid cell corners).
    /// </summary>
    public Bounds GetChunkExpandedWorldBounds(Vector2Int chunkKey)
    {
        var b = GetChunkExpandedCellBounds(chunkKey);
        var min = parentGrid.CellToWorld(new Vector3Int(b.xMin, b.yMin, 0));
        var maxCell = new Vector3Int(b.xMax, b.yMax, 0);
        // Convert max as the corner beyond the last cell: add one cell step in x and y
        var max = parentGrid.CellToWorld(maxCell);
        var size = max - min;
        return new Bounds(min + size * 0.5f, size);
    }

    /// <summary>
    /// Enumerate all chunk keys overlapping a given cell-space bounds (e.g., Tilemap.cellBounds or an edit brush area).
    /// </summary>
    public IEnumerable<Vector2Int> ChunksOverlappingCells(BoundsInt cells)
    {
        // Expand by overlap to be conservative when this region will write cross-chunk rules
        var region = new BoundsInt(
            new Vector3Int(cells.xMin - overlap, cells.yMin - overlap, 0),
            new Vector3Int(cells.size.x + overlap * 2, cells.size.y + overlap * 2, 1));

        // Figure out the chunk key range intersecting this region
        var minKey = CellToChunk(new Vector3Int(region.xMin, region.yMin, 0));
        // Use xMax-1/yMax-1 so inclusive
        var maxKey = CellToChunk(new Vector3Int(region.xMax - 1, region.yMax - 1, 0));

        for (int cy = minKey.y; cy <= maxKey.y; cy++)
            for (int cx = minKey.x; cx <= maxKey.x; cx++)
                yield return new Vector2Int(cx, cy);
    }

    /// <summary>
    /// Enumerate chunk keys in a radius (in chunks) around a world position (player/camera).
    /// </summary>
    public IEnumerable<Vector2Int> ChunksAroundWorld(Vector3 worldPos, int radiusChunks)
    {
        var center = WorldToChunk(worldPos);
        for (int dy = -radiusChunks; dy <= radiusChunks; dy++)
            for (int dx = -radiusChunks; dx <= radiusChunks; dx++)
                yield return new Vector2Int(center.x + dx, center.y + dy);
    }

    /// <summary>
    /// Clamp a chunk's expanded cell bounds to a given Tilemap's valid cell bounds (so you don't sample outside).
    /// </summary>
    public static BoundsInt ClampToTilemap(BoundsInt src, Tilemap tm)
    {
        var b = tm.cellBounds;
        int xMin = Mathf.Max(src.xMin, b.xMin);
        int yMin = Mathf.Max(src.yMin, b.yMin);
        int xMax = Mathf.Min(src.xMax, b.xMax);
        int yMax = Mathf.Min(src.yMax, b.yMax);
        if (xMax < xMin) xMax = xMin;
        if (yMax < yMin) yMax = yMin;
        return new BoundsInt(xMin, yMin, 0, xMax - xMin, yMax - yMin, 1);
    }

    /// <summary>
    /// Quick helper: does a cell fall inside a chunk's core or expanded bounds?
    /// </summary>
    public bool CellInChunk(Vector3Int cell, Vector2Int chunkKey, bool expanded)
    {
        var b = expanded ? GetChunkExpandedCellBounds(chunkKey) : GetChunkCoreCellBounds(chunkKey);
        return cell.x >= b.xMin && cell.x < b.xMax && cell.y >= b.yMin && cell.y < b.yMax;
    }

    public TileBase GetTile(Vector3Int cell, int layer_id) 
    {
        var tm = tilemapLayers[1];
        var local_coord = tm.WorldToCell(cell);

        return tilemapLayers[1].GetTile(local_coord);


    }
    public void RefreshNeighborhoodSameLayer(Vector3Int cell, int layer_id)
    {
        var tm = tilemapLayers[1];
        var local_coord = tm.WorldToCell(cell);
        tm.RefreshTile(local_coord);
    }

    public bool IsInbound(Vector3Int coordinate) 
    {
        var tm = tilemapLayers[1];
        var local_coord = tm.WorldToCell(coordinate);
        return this.Bounds.Contains(local_coord);
    }
    #endregion

    #region Internals

    private static int FloorDiv(int a, int b)
    {
        // exact floor division for negatives
        int q = a / b;
        int r = a % b;
        if ((r != 0) && ((r > 0) != (b > 0))) q--;
        return q;
    }

    #endregion

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        if (!parentGrid) return;
        if (chunkSize.x <= 0 || chunkSize.y <= 0) return;

        // Pick a reasonable focus point: scene view camera or object position
        var cam = UnityEditor.SceneView.lastActiveSceneView ? UnityEditor.SceneView.lastActiveSceneView.camera : null;
        var focus = cam ? cam.transform.position : transform.position;

        var keys = new HashSet<Vector2Int>();
        foreach (var k in ChunksAroundWorld(focus, gizmoRadiusChunks)) keys.Add(k);

        foreach (var key in keys)
        {
            var core = GetChunkCoreCellBounds(key);
            var exp = GetChunkExpandedCellBounds(key);

            // World boxes
            var coreMin = parentGrid.CellToWorld(new Vector3Int(core.xMin, core.yMin, 0));
            var coreMax = parentGrid.CellToWorld(new Vector3Int(core.xMax, core.yMax, 0));
            var coreSize = coreMax - coreMin;
            var coreBounds = new Bounds(coreMin + coreSize * 0.5f, coreSize);

            var expMin = parentGrid.CellToWorld(new Vector3Int(exp.xMin, exp.yMin, 0));
            var expMax = parentGrid.CellToWorld(new Vector3Int(exp.xMax, exp.yMax, 0));
            var expSize = expMax - expMin;
            var expBounds = new Bounds(expMin + expSize * 0.5f, expSize);

            // Expanded (overlap) fill
            Gizmos.color = overlapColor;
            Gizmos.DrawCube(expBounds.center, expBounds.size);
            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(expBounds.center, expBounds.size);

            // Core fill
            Gizmos.color = coreColor;
            Gizmos.DrawCube(coreBounds.center, coreBounds.size);
            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(coreBounds.center, coreBounds.size);
        }
    }
#endif
}
