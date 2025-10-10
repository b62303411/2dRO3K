// Assets/Scripts/ChunkedTilemap/ChunkTag.cs
using UnityEngine;

public class ChunkTag : MonoBehaviour
{
    // The chunk this child grid represents in parent-cell space
    public Vector2Int chunk; // e.g. (0,0), (1,0), (0,1), ...

    // Optional: quick authoring by name "Chunk_2_-1"
#if UNITY_EDITOR
    [ContextMenu("Parse chunk from GameObject name")]
    void ParseFromName()
    {
        // Accepts "Chunk_x_y" or "x_y"
        var s = gameObject.name;
        var parts = s.Split('_');
        if (parts.Length >= 3 && int.TryParse(parts[^2], out var cx) && int.TryParse(parts[^1], out var cy))
            chunk = new Vector2Int(cx, cy);
        else if (parts.Length >= 2 && int.TryParse(parts[^2], out cx) && int.TryParse(parts[^1], out cy))
            chunk = new Vector2Int(cx, cy);
    }
#endif
}
