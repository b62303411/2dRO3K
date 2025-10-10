
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
[ExecuteAlways]
public class ParentChunkGridIndex : MonoBehaviour
{
    [Header("Put this on the ROOT Grid")]
    public Grid parentGrid;

    [Header("Chunk layout in PARENT cells")]
    public Vector2Int chunkSize = new(64, 64);        // width/height of a chunk (in parent cells)
    public Vector3Int gridOrigin = Vector3Int.zero;   // parent-cell origin of chunk (0,0)
                                                      // was: readonly Dictionary<(Vector2Int chunk, int layerId), Tilemap> _layerMap = new();

    // auto-discovered
    readonly Dictionary<Vector2Int, Grid> _chunkGridAt = new();  // (cx,cy) -> child Grid
    readonly Dictionary<LayerKey, Tilemap> _layerMap = new(); // (chunk,layer) -> Tilemap
    readonly Dictionary<Tilemap, int> _layerOf = new(); // cached layerId
    readonly List<Grid> _childGridsBuf = new();
    readonly List<GridPartitionBounds> _GridPartitionBounds = new();
    readonly List<Tilemap> _tmBuf = new();
    // Floor-division that works for negatives too
    static int DivFloor(int a, int b) => (a >= 0) ? (a / b) : ((a - (b - 1)) / b);


    void OnEnable()
    {
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        RebuildIndex();
    }

    void OnValidate()
    {
        if (chunkSize.x <= 0) chunkSize.x = 64;
        if (chunkSize.y <= 0) chunkSize.y = 64;
        if (!parentGrid) parentGrid = GetComponent<Grid>();
    }

    // --- Public API ---

    public int GetLayerId(Tilemap tm)
    {
        // Stable mapping; customize to your needs (name, sortingLayerName, tag, etc.)
        if (tm && _layerOf.TryGetValue(tm, out var id)) return id;
        return tm ? Animator.StringToHash(tm.gameObject.name) : 0;
    }


    Vector2Int ParentToChunk(Vector3Int pc)
    {
        int rx = pc.x - gridOrigin.x, ry = pc.y - gridOrigin.y;
        int cx = DivFloor(rx, chunkSize.x);
        int cy = DivFloor(ry, chunkSize.y);
        return new Vector2Int(cx, cy);
    }
    public TileBase GetTileGlobalSameLayer2(Vector3Int worldPos, int layerId)
    {

        foreach (var childGrid in _childGridsBuf)
        {
            var bounds = childGrid.GetComponentInChildren<GridPartitionBounds>();
            if (null != bounds  && bounds.IsInbound(worldPos)) 
            {
                return bounds.GetTile(worldPos, layerId);
            }            
        }
        return null;
    }
    // Using a worldposition because what else ?!! so then the job is to narrow 
    // on what chunk then on what cell of that chunk no idear whatyou were doing 
    // not even sure parent to chunk works really  ironically we can infer as were in a
    // grid and we know what chunk were bordering then making that request. 
    public TileBase GetTileGlobalSameLayer(Vector3Int worldPos, int layerId)
    {

        var ok = GetTileGlobalSameLayer2(worldPos, layerId);

        
        return ok;
    }

    public void RefreshNeighborhoodSameLayer(Vector3Int parentCenter, int layerId)
    {
        //var baseChunk = ParentToChunk(parentCenter);
        // we only need to touch up to 4 chunks if parentCenter is on borders, but
        // keeping it simple: refresh 8-neighborhood and let the chunk lookup route it.
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var pc = new Vector3Int(parentCenter.x + dx, parentCenter.y + dy, 0);
                //var ch = ParentToChunk(pc);

                foreach (var childGrid in _childGridsBuf)
                {
                    var bounds = childGrid.GetComponentInChildren<GridPartitionBounds>();
                    if (null != bounds && bounds.IsInbound(pc))
                    {
                        bounds.RefreshNeighborhoodSameLayer(pc, layerId);
                    }
                }
            }
    }
  
    // --- Build / Discovery ---

    public void RebuildIndex()
    {
        _chunkGridAt.Clear();
        _layerMap.Clear();
        _layerOf.Clear();

        // 1) find ALL child Grids under parent (each child grid = one chunk)
        _childGridsBuf.Clear();
        parentGrid.GetComponentsInChildren(true, _childGridsBuf);

        for (int gi = 0; gi < _childGridsBuf.Count; gi++)
        {
            var g = _childGridsBuf[gi];
            if (!g || g == parentGrid) continue; // skip the root itself

            // compute this chunk's coord in parent-cell space
            // take the child grid's local cell (0,0,0) in world, map to parent cell:
            var childOriginWorld = g.CellToWorld(Vector3Int.zero);
            var childOriginParentCell = parentGrid.WorldToCell(childOriginWorld);
         
            ChunkTag tag = g.GetComponent<ChunkTag>();
            var chunk = tag.chunk;

            // store chunk grid
            _chunkGridAt[chunk] = g;

            // 2) find all Tilemaps that belong to THIS child grid (layers)
            _tmBuf.Clear();
            g.GetComponentsInChildren(true, _tmBuf);
            foreach (var tm in _tmBuf)
            {
                if (!tm) continue;
                // ensure this TM is actually laid out on THIS child grid
                if (tm.layoutGrid != g) continue;

                var layerId = ResolveLayerId(tm);
                _layerOf[tm] = layerId;
                var key = new LayerKey(chunk, layerId);
                // one tilemap per (chunk,layer). If you allow multiple, add priority logic.
                _layerMap[key] = tm;
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[ParentChunkGridIndex] Rebuilt. chunks={_chunkGridAt.Count}, layers={_layerMap.Count}");
#endif
    }

    // --- Helpers: parent cell <-> chunk math (floor-stable) ---

    Vector2Int ParentCellToChunk(Vector3Int pc)
    {
        int rx = pc.x - gridOrigin.x;
        int ry = pc.y - gridOrigin.y;
        int cx = DivFloor(rx, chunkSize.x);
        int cy = DivFloor(ry, chunkSize.y);
        return new Vector2Int(cx, cy);
    }

    Vector3Int ChunkToParentCellOrigin(Vector2Int chunk)
    {
        int x = gridOrigin.x + chunk.x * chunkSize.x;
        int y = gridOrigin.y + chunk.y * chunkSize.y;
        return new Vector3Int(x, y, 0);
    }

    //static int DivFloor(int a, int b) => (a >= 0) ? (a / b) : ((a - (b - 1)) / b);

    public static int ResolveLayerId(Tilemap tm)
    {
        // pick ONE rule and stick to it. Here: use GO name.
        return Animator.StringToHash(tm.gameObject.name);
    }
}
