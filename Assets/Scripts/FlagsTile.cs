// Assets/Scripts/Tiles/FlagsTile.cs
using UnityEngine;
using UnityEngine.Tilemaps;

[System.Flags]
public enum CellFlags : ushort
{
    None = 0,
    BlockFoot = 1 << 0,   // walls/cliffs/deep water
    BlockBoat = 1 << 1,   // rocks/shallow/pillars/low clearance
    BridgeDeck = 1 << 2,   // walkable strip over water
    Slow = 1 << 3,   // example: mud
}

[CreateAssetMenu(menuName = "Tiles/FlagsTile")]
public class FlagsTile : Tile
{
    public CellFlags flags = CellFlags.None;

    // Force collider generation on this tile type.
    private void OnValidate()
    {
        colliderType = ColliderType.Grid;
    }
}
