using UnityEngine;

[CreateAssetMenu(fileName = "ChunkSettings", menuName = "Tilemap/Chunk Settings")]
public class ChunkSettings : ScriptableObject
{
    [Min(2)] public int chunkSize = 64;               // usable area per side (cells)
    [Min(0)] public int overlap = 1;                   // extra border cells to keep RuleTiles coherent
    public Vector2Int initialChunks = new Vector2Int(4, 4);
    public GridLayout.CellLayout cellLayout = GridLayout.CellLayout.Rectangle;

    [Header("Layers (top to bottom)")]
    public string[] layerNames = new[] { "Decor", "Objects", "Ground" };
}