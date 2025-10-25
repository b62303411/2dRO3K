
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
[ExecuteAlways]
public class ParentChunkGridIndex : MonoBehaviour
{
    public struct Req 
    {
        public Vector3Int pos;
        public Tilemap map;
    }

    [Header("Put this on the ROOT Grid")]
    public Grid parentGrid;

    [Header("Chunk layout in PARENT cells")]
    public Vector2Int chunkSize = new(64, 64);        // width/height of a chunk (in parent cells)
    public Vector3Int gridOrigin = Vector3Int.zero;   // parent-cell origin of chunk (0,0)
                                                      // was: readonly Dictionary<(Vector2Int chunk, int layerId), Tilemap> _layerMap = new();

    // auto-discovered
    readonly Dictionary<Vector2Int, ChunkTag> _chunkAt = new();  // (cx,cy) -> child Grid
    readonly Dictionary<LayerKey, Tilemap> _layerMap = new(); // (chunk,layer) -> Tilemap
    readonly Dictionary<Tilemap, int> _layerOf = new(); // cached layerId
    readonly List<ChunkTag> _chunkBuf = new();
    readonly List<GridPartitionBounds> _GridPartitionBounds = new();
    readonly List<Tilemap> _tmBuf = new();
    // Floor-division that works for negatives too
    static int DivFloor(int a, int b) => (a >= 0) ? (a / b) : ((a - (b - 1)) / b);
     
    public Queue<Req> _queue_resolve = new();
    public Queue<Req> _queue_update = new();
    void OnEnable()
    {
        if (!parentGrid) parentGrid = GetComponent<Grid>();
        RebuildIndex();
    }



    void Update()
    {
        // Your per-editor-frame work (keep it tiny; budgeted if heavy)
        // e.g., DrainQueues(); throttle long tasks; use timeSinceStartup for pacing
        // double t = EditorApplication.timeSinceStartup;
        Dequeue();
    }


    void Dequeue() 
    {
        int budget = Mathf.Max(0, 100);
        while (budget-- > 0 && _queue_update.Count > 0)
        {
            var r = _queue_update.Dequeue();
            if (!r.map) continue;
            r.map.RefreshTile(r.pos);
        }
    }
    
    
    void Enqueue(Tilemap map, Vector3Int pos)
    {
        var req = new Req { map = map, pos = pos };
        //var key = new Key { map = map, pos = pos };
        _queue_update.Enqueue(req);
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
        int cx = DivFloor(rx,128);
        int cy = DivFloor(ry, 128);
        return new Vector2Int(cx, cy);
    }

    ChunkTag GetChunk(Vector3Int position) 
    {
        if (_chunkAt.Count == 0) 
        {
            RebuildIndex();
        }
        var chunk_coord = ParentToChunk(position);
        ChunkTag chunk;
        _chunkAt.TryGetValue(chunk_coord, out chunk);
        return chunk;

    }


    Tilemap GetTileMap(Vector3Int position, int layerId) 
    {
        ChunkTag chunk = GetChunk(position);
        if (chunk == null)
            return null;
        var tm = chunk.GetLayer(layerId);
       
        if (tm == null)
        {
            List<Tilemap> _tilemaps = new();

            chunk.GetComponentsInChildren(true, _tilemaps);

            tm = _tilemaps[2];
        }
        return tm;
    }

    // Using a worldposition because what else ?!! so then the job is to narrow 
    // on what chunk then on what cell of that chunk no idear whatyou were doing 
    // not even sure parent to chunk works really  ironically we can infer as were in a
    // grid and we know what chunk were bordering then making that request. 
    public TileBase GetTileGlobalSameLayer(Vector3Int position, int layerId)
    {
        var tm = GetTileMap(position, layerId);
        if (null != tm) 
        {
            var tb = tm.GetTile(position);

            return tb;
        }
        return null;
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
                var tm = GetTileMap(pc, layerId);
                if (null != tm) {
                    //tm.RefreshTile(pc);
                    Enqueue(tm, pc);
                }               
            }
    }
  
    // --- Build / Discovery ---

    public void RebuildIndex()
    {
        _chunkAt.Clear();
        _layerMap.Clear();
        _layerOf.Clear();

        // 1) find ALL child Grids under parent (each child grid = one chunk)
        _chunkBuf.Clear();

        var root = parentGrid.transform;

        int childCount = root.childCount;

        for (int i = 0; i < childCount; i++) 
        {

            var child = root.GetChild(i);

            if (!child.TryGetComponent<ChunkTag>(out var chunk)) continue;

            var tm = child.GetChild(0).GetComponent<Tilemap>();

            
            var b = tm.cellBounds;

            var pos = b.center;

            var pc = new Vector3Int((int)pos.x, (int)pos.y, 0);

            var value = ParentToChunk(pc);

            _chunkAt[value] = chunk;
        }
           


#if UNITY_EDITOR
        Debug.Log("test");
        //Debug.Log($"[ParentChunkGridIndex] Rebuilt. chunks={_chunkGridAt.Count}, layers={_layerMap.Count}");
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
