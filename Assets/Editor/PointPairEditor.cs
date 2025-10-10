#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PointPair))]
public class PointPairEditor : Editor
{
    void OnSceneGUI()
    {
        var t = (PointPair)target;
        var tr = t.transform;

        // World positions
        var wA = tr.TransformPoint(t.pA);
        var wB = tr.TransformPoint(t.pB);
        var wM = (wA + wB) * 0.5f;

        Handles.color = Color.yellow;
        EditorGUI.BeginChangeCheck();
        var newWA = Handles.PositionHandle(wA, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Move A");
            var delta = newWA - wA;
            if (t.lockZ) delta.z = 0f;
            t.pA = tr.InverseTransformPoint(wA + delta);
            if (t.lockZ) t.pA.z = t.fixedZ;
        }

        Handles.color = Color.yellow;
        EditorGUI.BeginChangeCheck();
        var newWB = Handles.PositionHandle(wB, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Move B");
            var delta = newWB - wB;
            if (t.lockZ) delta.z = 0f;
            t.pB = tr.InverseTransformPoint(wB + delta);
            if (t.lockZ) t.pB.z = t.fixedZ;
        }

        // Center handle (moves both points together in XY)
        Handles.color = Color.cyan;
        float size = HandleUtility.GetHandleSize(wM) * 0.12f;
        EditorGUI.BeginChangeCheck();
        var fmh_48_13_638943891239842392 = Quaternion.identity; var newWM = Handles.FreeMoveHandle(
            wM,
            size,
            EditorSnapSettings.move,     // respects Unity snap settings
            Handles.RectangleHandleCap
        );
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(t, "Move Pair (center)");
            var delta = newWM - wM;
            if (t.lockZ) delta.z = 0f;

            // apply same delta to both points
            var newA = wA + delta;
            var newB = wB + delta;

            t.pA = tr.InverseTransformPoint(newA);
            t.pB = tr.InverseTransformPoint(newB);

            if (t.lockZ) { t.pA.z = t.fixedZ; t.pB.z = t.fixedZ; }
        }
    }
}
#endif
