// Assets/Scripts/Tilemap/RuntimeRuleTileConverter.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DefaultExecutionOrder(-1000)] // run early
public class RuntimeRuleTileConverter : MonoBehaviour
{
    [Tooltip("Leave empty to auto-find under this Grid.")]
    public List<Tilemap> tilemaps = new();

    [Tooltip("If null, will auto-find on the same Grid parent.")]
    public ChunkGridIndex globalIndex;

    // Cache: source RuleTile -> converted CrossPartitionRuleTile
    private readonly Dictionary<RuleTile, CrossPartitionRuleTile> _cache = new(64);

    void Awake()
    {
        var grid = GetComponentInParent<Grid>();
        if (!grid)
        {
            Debug.LogError("[RuntimeRuleTileConverter] Must be placed under a Grid.");
            return;
        }

        if (!globalIndex)
            globalIndex = grid.GetComponentInParent<ChunkGridIndex>();

        if (tilemaps.Count == 0)
        {
            tilemaps.Clear();
            grid.GetComponentsInChildren(true, tilemaps);
        }

        int replaced = 0, scanned = 0;

        foreach (var tm in tilemaps)
        {
            if (!tm) continue;

            var b = tm.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    var src = tm.GetTile(p);
                    if (!src) continue;
                    scanned++;

                    // Only convert RuleTiles (others left as-is)
                    var rt = src as RuleTile;
                    if (rt == null) continue;

                    // Get or make a CrossPartitionRuleTile
                    if (!_cache.TryGetValue(rt, out var cpt))
                    {
                        cpt = CloneAsCross(rt, globalIndex);
                        _cache[rt] = cpt;
                    }

                    // Swap on the map
                    tm.SetTile(p, cpt);
                    replaced++;
                }

            // Keep bounds tight for perf
            tm.CompressBounds();
            tm.RefreshAllTiles();
        }

        Debug.Log($"[RuntimeRuleTileConverter] Scanned {scanned} cells, replaced {replaced} RuleTiles with CrossPartitionRuleTile (cache {_cache.Count}).");
    }

    private static CrossPartitionRuleTile CloneAsCross(RuleTile src, ChunkGridIndex index)
    {
        var dst = ScriptableObject.CreateInstance<CrossPartitionRuleTile>();
        //dst.index = index;
        //dst.index = index;

        // --- Copy base RuleTile fields (Unity 6 / Extras) ---
        dst.m_DefaultSprite = src.m_DefaultSprite;
        dst.m_DefaultGameObject = src.m_DefaultGameObject;
        dst.m_DefaultColliderType = src.m_DefaultColliderType;
        //dst.m_MinAnimationSpeed = src.m_MinAnimationSpeed;
        //dst.m_MaxAnimationSpeed = src.m_MaxAnimationSpeed;
        //dst.m_AnimationStartTime = src.m_AnimationStartTime;
        //dst.m_RandomTransform = src.m_RandomTransform;

        // Copy tiling rules (deep enough for runtime)
        dst.m_TilingRules = new List<RuleTile.TilingRule>(src.m_TilingRules.Count);
        foreach (var r in src.m_TilingRules)
        {
            var nr = new RuleTile.TilingRule
            {
                m_Sprites = (Sprite[])r.m_Sprites?.Clone(),
                m_GameObject = r.m_GameObject,
                m_Output = r.m_Output,
                m_RuleTransform = r.m_RuleTransform,
                m_ColliderType = r.m_ColliderType,
                //m_RandomTransform = r.m_RandomTransform,
                //m_PerlinScale = r.m_PerlinScale,
                //m_PerlinOffset = r.m_PerlinOffset,
                //m_Noise = r.m_Noise
            };

            // Lists need fresh instances
            if (r.m_Neighbors != null)
                nr.m_Neighbors = new List<int>(r.m_Neighbors);
            if (r.m_NeighborPositions != null)
                nr.m_NeighborPositions = new List<Vector3Int>(r.m_NeighborPositions);

            dst.m_TilingRules.Add(nr);
        }

        return dst;
    }
}
