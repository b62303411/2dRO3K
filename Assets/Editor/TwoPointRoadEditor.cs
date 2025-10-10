#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TwoPointRoad))]
public class TwoPointRoadEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (TwoPointRoad)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Snap Points To Grid", GUILayout.Height(24)))
            {
                Undo.RecordObject(t, "Snap Points");
                t.SnapPointsToGrid();
                EditorUtility.SetDirty(t);
            }
            if (GUILayout.Button("Bake / Update", GUILayout.Height(24)))
            {
                t.SnapPointsToGrid();
                t.Bake();
            }
        }
    }

    void OnSceneGUI()
    {
        var t = (TwoPointRoad)target;
        if (!t.sceneGrid) return;

        Handles.color = Color.cyan;
        Handles.DrawAAPolyLine(3f, t.p0World, t.p1World);

        float s0 = HandleUtility.GetHandleSize(t.p0World) * 0.08f;
        float s1 = HandleUtility.GetHandleSize(t.p1World) * 0.08f;

        EditorGUI.BeginChangeCheck();
        var fmh_43_53_638943154278449033 = Quaternion.identity; var np0 = Handles.FreeMoveHandle(t.p0World, s0, Vector3.zero, Handles.SphereHandleCap);
        var fmh_44_53_638943154278492363 = Quaternion.identity; var np1 = Handles.FreeMoveHandle(t.p1World, s1, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Move Road Points");
            t.p0World = Snap(t.sceneGrid, np0);
            t.p1World = Snap(t.sceneGrid, np1);
            EditorUtility.SetDirty(t);
        }

        Handles.Label(t.p0World, "P0");
        Handles.Label(t.p1World, "P1");
    }

    static Vector3 Snap(Grid grid, Vector3 w)
    {
        var c = grid.WorldToCell(w);
        return grid.CellToWorld(c) + grid.cellSize / 2f;
    }
}
#endif
