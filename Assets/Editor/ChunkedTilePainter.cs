// ============================================================================
// File: Assets/Scripts/ChunkedTilemap/Editor/ChunkedTilePainter.cs
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkedTilePainter : EditorWindow
{
    private TilemapWorld world;
    private int layerIndex;
    private TileBase paintTile;
    private bool painting;

    [MenuItem("Tools/Chunked Tile Painter")] static void Open() => GetWindow<ChunkedTilePainter>(true, "Chunk Painter");

    void OnGUI()
    {
        world = (TilemapWorld)EditorGUILayout.ObjectField("World", world, typeof(TilemapWorld), true);
        layerIndex = EditorGUILayout.IntField("Layer Index", layerIndex);
        paintTile = (TileBase)EditorGUILayout.ObjectField("Tile", paintTile, typeof(TileBase), false);

        using (new EditorGUI.DisabledScope(!world || !paintTile))
            painting = GUILayout.Toggle(painting, painting ? "Painting (ESC to stop)" : "Start Painting", "Button");

        if (world && GUILayout.Button("Create/Reset World"))
        {
            Undo.RegisterFullObjectHierarchyUndo(world.gameObject, "Create World");
            world.CreateWorld();
        }

        EditorGUILayout.HelpBox("LMB: paint tile into the correct chunk. Only affected chunks + borders refresh.", MessageType.Info);
    }

    void OnEnable() { SceneView.duringSceneGui += OnSceneGUI; }
    void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; }

    void OnSceneGUI(SceneView sv)
    {
        if (!painting || !world || Event.current.type != EventType.MouseDown || Event.current.button != 0) return;
        var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        var plane = new Plane(Vector3.forward, Vector3.zero);
        if (plane.Raycast(ray, out float d))
        {
            var p = ray.GetPoint(d);
            var cell = world.grid.WorldToCell(p);
            Undo.RegisterFullObjectHierarchyUndo(world.gameObject, "Paint Tile");
            world.SetTile(layerIndex, cell, paintTile);
            world.RefreshDirty();
            Event.current.Use();
            Repaint();
        }
    }
}
#endif