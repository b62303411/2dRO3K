using UnityEngine;
using UnityEngine.Tilemaps;

public class TilemapDimensions : MonoBehaviour
{
    public Tilemap targetTilemap;

    void Start()
    {
        if (targetTilemap != null)
        {
            // Ensure the bounds reflect the actual tiles placed
            targetTilemap.CompressBounds();

            // Get the bounds of the tilemap in cell coordinates
            BoundsInt bounds = targetTilemap.cellBounds;

            // The 'size' property of BoundsInt gives the dimensions in cells
            Vector3Int dimensions = bounds.size;

            Debug.Log($"Tilemap dimensions (in cells): X={dimensions.x}, Y={dimensions.y}, Z={dimensions.z}");
            Debug.Log($"Tilemap origin (in cells): X={bounds.xMin}, Y={bounds.yMin}, Z={bounds.zMin}");
        }
        else
        {
            Debug.LogError("Assign a Tilemap to the 'targetTilemap' field in the Inspector.");
        }
    }
}

