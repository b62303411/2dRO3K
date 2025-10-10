using UnityEngine;

public class PointPair : MonoBehaviour
{
    public Vector3 pA = new Vector3(-1, 0, 0);
    public Vector3 pB = new Vector3(1, 0, 0);
    public Grid sceneGrid;
    [Tooltip("Keep Z fixed while dragging (XY only).")]
    public bool lockZ = true;
    public float fixedZ = 0f;

    public Vector3 Midpoint => (pA + pB) * 0.5f;


    void Reset()
    {
        if (!sceneGrid) sceneGrid = GetComponentInParent<Grid>();
        if (sceneGrid)
        {
            pA = Snap(sceneGrid, transform.position);
            pB = Snap(sceneGrid, transform.position + Vector3.right * Mathf.Max(1f, sceneGrid.cellSize.x * 3f));
        }
    }

    public void SnapPointsToGrid()
    {
        if (!sceneGrid) return;
        pA = Snap(sceneGrid, pA);
        pB = Snap(sceneGrid, pB);
    }

    static Vector3 Snap(Grid grid, Vector3 worldPos)
    {
        Vector3Int cell = grid.WorldToCell(worldPos);
        return grid.GetCellCenterWorld(cell);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(pA), 0.05f);
        Gizmos.DrawSphere(transform.TransformPoint(pB), 0.05f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.TransformPoint(Midpoint), 0.06f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.TransformPoint(pA), transform.TransformPoint(pB));
    }
}
