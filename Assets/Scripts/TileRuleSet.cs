using UnityEngine;
using UnityEngine.Tilemaps;
[CreateAssetMenu(menuName = "Adjacency/Tile Rule Set (4-neighbors)")]
public sealed class TileRuleSet : ScriptableObject
{
    // 0..15 => 4 bits: N=1, E=2, S=4, W=8
    [Tooltip("Index = mask (N=1,E=2,S=4,W=8). Size must be 16.")]
    public TileBase[] tilesByMask = new TileBase[16];

    [Header("Optional: default/fallback when entry is null")]
    public TileBase fallback;

    public TileBase GetTileByMask(int mask)
    {
        if (mask < 0 || mask > 15) return fallback;
        var t = tilesByMask[mask];
        return t != null ? t : fallback;
    }
}
