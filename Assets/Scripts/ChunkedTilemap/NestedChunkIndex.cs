// Assets/Scripts/Tilemap/NestedChunkIndex.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class NestedChunkIndex : MonoBehaviour
{
    [Header("Global (parent)")]
    public Grid parentGrid;

    [Tooltip("Chunk size en cellules du PARENT (ex: 64x64)")]
    public Vector2Int chunkSize = new(64, 64);

    [Tooltip("Laisse vide: auto-scan sous ce Grid")]
    public List<Tilemap> tilemaps = new();

    // (layer,cx,cy) -> tilemaps candidates
    struct Key { public int layer, cx, cy; public Key(int l, int x, int y) { layer = l; cx = x; cy = y; } }
    readonly Dictionary<Key, List<Tilemap>> _index = new();
    readonly Dictionary<Tilemap, int> _layerOf = new();
    readonly Dictionary<Tilemap, Grid> _gridOf = new();
    readonly Dictionary<Tilemap, int> _prio = new();

    void OnEnable()
    {
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        AutoDiscoverTilemaps();
        BuildIndex();
    }

    void OnValidate()
    {
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        if (chunkSize.x <= 0) chunkSize.x = 64;
        if (chunkSize.y <= 0) chunkSize.y = 64;
    }

    public void AutoDiscoverTilemaps()
    {
        if (!parentGrid) return;
        tilemaps ??= new List<Tilemap>();
        tilemaps.Clear();
        parentGrid.GetComponentsInChildren(true, tilemaps); // enfants + sous-Grids
    }

    public void BuildIndex()
    {
        _index.Clear(); _layerOf.Clear(); _gridOf.Clear(); _prio.Clear();

        int added = 0;
        foreach (var tm in tilemaps)
        {
            if (!tm) continue;
            var childGrid = tm.layoutGrid ? tm.layoutGrid : tm.GetComponentInParent<Grid>();
            if (!childGrid) continue;

            _gridOf[tm] = childGrid;
            _layerOf[tm] = ResolveLayerId(tm);
            _prio[tm] = 0;

            // bounds du TM en local enfant → approx en parent
            var bLocal = tm.cellBounds;
            if (bLocal.size.x == 0 || bLocal.size.y == 0) continue;

            var minP = LocalToParentCell(childGrid, parentGrid, bLocal.min);
            var maxP = LocalToParentCell(childGrid, parentGrid, bLocal.max); // exclusif

            var minCx = Mathf.FloorToInt(minP.x / (float)chunkSize.x);
            var minCy = Mathf.FloorToInt(minP.y / (float)chunkSize.y);
            var maxCx = Mathf.FloorToInt((maxP.x - 1) / (float)chunkSize.x);
            var maxCy = Mathf.FloorToInt((maxP.y - 1) / (float)chunkSize.y);

            for (int cy = minCy; cy <= maxCy; cy++)
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    var k = new Key(_layerOf[tm], cx, cy);
                    if (!_index.TryGetValue(k, out var list))
                        _index[k] = list = new List<Tilemap>(1);
                    list.Add(tm);
                    added++;
                }
        }

#if UNITY_EDITOR
        Debug.Log($"[NestedChunkIndex] BuildIndex ok. Tilemaps: {tilemaps.Count}, cells→buckets: {added}, layers: {_layerOf.Count}");
#endif
    }

    public int GetLayerId(Tilemap tm)
    {
        return _layerOf.TryGetValue(tm, out var id) ? id : ResolveLayerId(tm);
    }

    public TileBase GetTileGlobalSameLayer(Vector3Int parentCell, int layerId)
    {
        var ch = ParentCellToChunk(parentCell);
        var k = new Key(layerId, ch.x, ch.y);
        if (!_index.TryGetValue(k, out var list)) return null;

        TileBase best = null; int bestP = int.MinValue;
        for (int i = 0; i < list.Count; i++)
        {
            var tm = list[i];
            if (!tm || !tm.isActiveAndEnabled) continue;

            var local = ParentToLocalCell(_gridOf[tm], parentGrid, parentCell);
            if (!tm.cellBounds.Contains(local)) continue;

            var t = tm.GetTile(local);
            if (!t) continue;

            int p = _prio[tm];
            if (best == null || p > bestP) { best = t; bestP = p; }
        }
        return best;
    }

    public void RefreshNeighborhoodSameLayer(Vector3Int parentCenter, int layerId)
    {
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                var pc = new Vector3Int(parentCenter.x + x, parentCenter.y + y, parentCenter.z);
                var ch = ParentCellToChunk(pc);
                var k = new Key(layerId, ch.x, ch.y);
                if (!_index.TryGetValue(k, out var list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    var tm = list[i];
                    var local = ParentToLocalCell(_gridOf[tm], parentGrid, pc);
                    tm.RefreshTile(local);
                }
            }
    }

    // --- utils ---
    int ResolveLayerId(Tilemap tm)
    {
        // Choisis une convention stable (nom, tag, sortingLayerName, etc.)
        return Animator.StringToHash(tm.gameObject.name);
    }
    Vector2Int ParentCellToChunk(Vector3Int pc)
    {
        int cx = Mathf.FloorToInt(pc.x / (float)chunkSize.x);
        int cy = Mathf.FloorToInt(pc.y / (float)chunkSize.y);
        return new Vector2Int(cx, cy);
    }
    static Vector3Int LocalToParentCell(Grid child, Grid parent, Vector3Int local)
    {
        var world = child.CellToWorld(local);
        return parent.WorldToCell(world);
    }
    static Vector3Int ParentToLocalCell(Grid child, Grid parent, Vector3Int parentCell)
    {
        var world = parent.CellToWorld(parentCell);
        return child.WorldToCell(world);
    }
}
