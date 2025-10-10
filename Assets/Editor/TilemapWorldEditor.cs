// ============================================================================
// File: Assets/Scripts/ChunkedTilemap/Editor/TilemapWorldEditor.cs
// ============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TilemapWorld))]
public class TilemapWorldEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var tw = (TilemapWorld)target;
        if (tw && GUILayout.Button("Refresh Dirty Now")) tw.RefreshDirty();
    }
}
#endif