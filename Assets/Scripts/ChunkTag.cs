// Assets/Scripts/ChunkedTilemap/ChunkTag.cs
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEditor;

[ExecuteAlways]
public class ChunkTag : MonoBehaviour
{
    // The chunk this child grid represents in parent-cell space
    public Vector2Int chunk; // e.g. (0,0), (1,0), (0,1), ...
    public List<Tilemap> layers = new List<Tilemap>();
    public readonly Dictionary<int, Tilemap> _layerMap = new();
    void Awake()
    {
        DiscoverLayers();
    }
    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged; // Updated for Unity 6
        DiscoverLayers();
    }


    public Tilemap GetLayer(int layerId) 
    {
        var orderInLayer = 0;
        if (_layerMap.Count == 0) 
        {
            foreach (Tilemap tm in layers)
            {
                var hash = Animator.StringToHash(tm.gameObject.name);
                _layerMap[hash] = tm;
                var tilemapRenderer = tm.GetComponent<TilemapRenderer>();
                tilemapRenderer.sortingOrder = orderInLayer;
                orderInLayer++;
                tm.enabled = false;
            }
        }
        return this._layerMap[layerId];
    }

    void DiscoverLayers() 
    {

        if (layers.Count == 0)
        {
            GetComponentsInChildren(true, layers);
            foreach (Tilemap tm in layers)
            {
                var hash = Animator.StringToHash(tm.gameObject.name);
                _layerMap[hash] = tm;
                tm.enabled = false;
            }

        }
    }
    void SetTileMapActive(bool active)
    {
        foreach (Tilemap tm in layers)
        {
            tm.enabled = active; // Re-enable for Editor
        } 
    }
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
#if UNITY_EDITOR
    void OnDisable()
    {
        EditorApplication.playModeStateChanged  -= OnPlayModeStateChanged;
    }
    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
       if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SetTileMapActive(false);
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SetTileMapActive(true);
        }
    }

#endif
}
