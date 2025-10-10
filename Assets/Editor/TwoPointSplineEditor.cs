#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

[CustomEditor(typeof(SplineContainer))]
public class TwoPointSplineEditor : Editor
{
    void OnSceneGUI()
    {
        var sc = (SplineContainer)target;
        if (sc == null || sc.Spline == null) return;
        var spline = sc.Spline;
        if (spline.Count < 2) return; // expects 2 points

        // read knots
        var k0 = spline[0]; // BezierKnot
        var k1 = spline[1];

        var tf = sc.transform;
        Vector3 w0 = tf.TransformPoint((Vector3)k0.Position);
        Vector3 w1 = tf.TransformPoint((Vector3)k1.Position);

        // draw line
        Handles.color = Color.cyan;
        Handles.DrawAAPolyLine(3f, w0, w1);

        // draw + drag end handles
        float size0 = HandleUtility.GetHandleSize(w0) * 0.08f;
        float size1 = HandleUtility.GetHandleSize(w1) * 0.08f;

        EditorGUI.BeginChangeCheck();
        var fmh_33_48_638943104174799179 = Quaternion.identity; var newW0 = Handles.FreeMoveHandle(w0, size0, Vector3.zero, Handles.SphereHandleCap);
        var fmh_34_48_638943104174825697 = Quaternion.identity; var newW1 = Handles.FreeMoveHandle(w1, size1, Vector3.zero, Handles.SphereHandleCap);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(sc, "Move Spline Endpoints");
            // write back in local space (keep tangents zero for straight segment)
            var l0 = (Vector3)tf.InverseTransformPoint(newW0);
            var l1 = (Vector3)tf.InverseTransformPoint(newW1);

            var bk0 = new BezierKnot(l0, Vector3.zero, Vector3.zero, Quaternion.identity);
            var bk1 = new BezierKnot(l1, Vector3.zero, Vector3.zero, Quaternion.identity);

            spline.SetKnot(0, bk0);
            spline.SetKnot(1, bk1);
            spline.SetTangentMode(0, TangentMode.Linear);
            spline.SetTangentMode(1, TangentMode.Linear);

            // tell Unity the object changed
            EditorUtility.SetDirty(sc);
        }

        // optional: labels
        Handles.color = Color.white;
        Handles.Label(w0, "P0");
        Handles.Label(w1, "P1");
    }
}
#endif
