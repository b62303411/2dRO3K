#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

public static class SplineGizmos
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void Draw(SplineContainer sc, GizmoType type)
    {
        if (sc == null || sc.Spline == null || sc.Spline.Count < 2) return;

        // sample two ends
        sc.Evaluate(0f, out var p0, out _, out _);
        sc.Evaluate(1f, out var p1, out _, out _);

        var t = sc.transform;
        var w0 = t.TransformPoint((Vector3)p0);
        var w1 = t.TransformPoint((Vector3)p1);

        var oldC = Gizmos.color;
        Gizmos.color = (type & GizmoType.Selected) != 0 ? Color.cyan : new Color(0,1,1,0.6f);

        // line
        Gizmos.DrawLine(w0, w1);

        // end circles
        float r = HandleUtility.GetHandleSize(w0) * 0.1f;
        Gizmos.DrawWireSphere(w0, r);
        Gizmos.DrawWireSphere(w1, r);

        Gizmos.color = oldC;
    }
}
#endif
