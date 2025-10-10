// Assets/Scripts/TwoPointRoad.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TwoPointRoad : MonoBehaviour
{
    [Header("Grid & Layer")]
    public Grid sceneGrid;
    public Tilemap targetTilemap;

    [Header("Tiles & Rules (side bands use +/- relative to the road's local 'right')")]
    public TileBase horizontal_centerTile;            // band 0
    public TileBase horizontal_upperTile;             // bands w > 0
    public TileBase horizontal_lowerTile;             // bands w < 0

    // Cap tiles (used at each end). "left" = start (p0), "right" = end (p1).
    public TileBase horizontal_left_upperTile;
    public TileBase horizontal_left_centerTile;
    public TileBase horizontal_left_lowerTile;
    public TileBase horizontal_right_upperTile;
    public TileBase horizontal_right_centerTile;
    public TileBase horizontal_right_lowerTile;

    [Min(0)] public int halfWidth = 0;               // 0=1 wide; 1=3 wide; 2=5 wide...
    public bool addEndCaps = false;
    [Min(1)] public int capLengthCells = 3;          // 3 along tangent
    [Min(1)] public int capHalfWidth = 1;            // 1 => 3 rows (-1,0,1)

    [Header("Points (world, snapped)")]
    public Vector3 p0World;
    public Vector3 p1World;

    [Header("Bake options")]
    public bool clearCoveredAreaBeforeBake = true;

    void Reset()
    {
        if (!sceneGrid) sceneGrid = GetComponentInParent<Grid>();
        if (sceneGrid)
        {
            p0World = Snap(sceneGrid, transform.position);
            p1World = Snap(sceneGrid, transform.position + Vector3.right * Mathf.Max(1f, sceneGrid.cellSize.x * 3f));
        }
    }

    public void SnapPointsToGrid()
    {
        if (!sceneGrid) return;
        p0World = Snap(sceneGrid, p0World);
        p1World = Snap(sceneGrid, p1World);
    }

    static Vector3 Snap(Grid grid, Vector3 worldPos)
    {
        Vector3Int cell = grid.WorldToCell(worldPos);
        return grid.GetCellCenterWorld(cell);
    }
    // Returns all grid cells (Vector3Int) the line between two cells passes through (supercover).
    static List<Vector3Int> SupercoverLine(Vector3Int a, Vector3Int b)
    {
        var cells = new List<Vector3Int>();
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;
        int e2;
        int x = x0, y = y0;
        cells.Add(new Vector3Int(x, y, 0));

        // Bresenham supercover variant: when crossing a corner, include both cells
        while (x != x1 || y != y1)
        {
            e2 = err * 2;
            int xPrev = x, yPrev = y;

            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }

            // corner case: we advanced both x and y; add the intermediate cell too
            if (x != xPrev && y != yPrev)
            {
                cells.Add(new Vector3Int(x, yPrev, 0));
                cells.Add(new Vector3Int(xPrev, y, 0));
            }

            cells.Add(new Vector3Int(x, y, 0));
        }
        return cells;
    }

    // Cell-space perpendicular “right” for band expansion.
    // Given main step (sx, sy), a perpendicular is (-sy, +sx).
    static Vector3Int PerpRightCell(int sx, int sy) => new Vector3Int(-sy, sx, 0);

    // Sign helper (returns -1,0,1)
    static int Sgn(int v) => v < 0 ? -1 : (v > 0 ? 1 : 0);

    public void Bake()
    {
        if (!sceneGrid || !targetTilemap)
        {
            Debug.LogWarning("[TwoPointRoad] Assign sceneGrid + targetTilemap.");
            return;
        }
        if (!horizontal_centerTile && !horizontal_upperTile && !horizontal_lowerTile)
        {
            Debug.LogWarning("[TwoPointRoad] Assign at least one of the tiles.");
            return;
        }

        var cellsByTile = new Dictionary<TileBase, HashSet<Vector3Int>>();
        var covered = new HashSet<Vector3Int>();

        void Put(TileBase t, Vector3Int c)
        {
            if (!t) return;
            if (!cellsByTile.TryGetValue(t, out var set)) cellsByTile[t] = set = new HashSet<Vector3Int>();
            if (set.Add(c)) covered.Add(c);
        }
        // Main path bands
        // --- Main path in CELL SPACE ---
        Vector3Int c0 = sceneGrid.WorldToCell(p0World);
        Vector3Int c1 = sceneGrid.WorldToCell(p1World);

        Vector3Int up = GridSpace.PerpRight(1, 0);   // (-sy, +sx)
        Vector3Int down = -up;

        List <Vector3Int> cells = GridSpace.LineCells(c0, c1);
        Vector3Int start = cells[0];
        Vector3Int end = cells[cells.Count - 1];

        Put( horizontal_left_upperTile, start + up);
        Put(horizontal_left_centerTile, start);
        Put(horizontal_left_lowerTile, start + down);

        Put(horizontal_right_upperTile, end + up);
        Put(horizontal_right_centerTile, end);
        Put(horizontal_right_lowerTile, end + down);


        for (int i = 1; i < cells.Count - 1; i++)
        {
            var c = cells[i];
            Put(horizontal_upperTile, c + up);
            Put(horizontal_centerTile, c);
            Put(horizontal_lowerTile, c + down);
        }
     
        Vector3 dir = (p1World - p0World);
        float len = dir.magnitude;
        if (len < 1e-5f) return;

        // fwd = along the road; right = "upper" direction (perpendicular)
        Vector3 fwd = dir / len;
        Vector3 right = new Vector3(fwd.y, -fwd.x, 0f).normalized;

        // Dense sampling to avoid gaps
        float step = Mathf.Min(sceneGrid.cellSize.x, sceneGrid.cellSize.y) * 0.5f;

        // Direction signs in cell space
        int sx = Sgn(c1.x - c0.x);
        int sy = Sgn(c1.y - c0.y);

        // Perpendicular “right” in cell units (upper = +w uses this)
        Vector3Int rightCell = PerpRightCell(sx, sy);

        // Core cells along the line (supercover)
        var lineCells = SupercoverLine(c0, c1);
        
        foreach (var core in lineCells)
        {
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                Vector3Int cell = core + rightCell * w;

                TileBase t =
                    (w == 0) ? horizontal_centerTile :
                    (w > 0) ? horizontal_upperTile :
                               horizontal_lowerTile;

                //Put(t, cell);
            }
        }

        addEndCaps = false;
        // End caps
        if (addEndCaps)
        {
            // Start end (p0) → "left" cap tiles
            AddCaps(
                origin: p0World,
                forward: fwd,
                upperTile: horizontal_left_upperTile,
                lowerTile: horizontal_left_lowerTile
            );

            // End end (p1) → "right" cap tiles
            AddCaps(
                origin: p1World,
                forward: -fwd,
                upperTile: horizontal_right_upperTile,
                lowerTile: horizontal_right_lowerTile
            );
        }

        // Write
        if (clearCoveredAreaBeforeBake)
            foreach (var c in covered) targetTilemap.SetTile(c, null);

        foreach (var kv in cellsByTile)
            foreach (var c in kv.Value)
                targetTilemap.SetTile(c, kv.Key);

        targetTilemap.RefreshAllTiles();

        // ------- local: cap writer -------
        void AddCaps(Vector3 origin, Vector3 forward, TileBase upperTile, TileBase lowerTile)
        {
            Vector3 r = new Vector3(forward.y, -forward.x, 0f).normalized;
            float stepAlong = sceneGrid.cellSize.x;
            float stepAcross = sceneGrid.cellSize.y;

            for (int row = -capHalfWidth; row <= capHalfWidth; row++)
            {
                Vector3 rowOff = r * (row * stepAcross);
                for (int i = 0; i < capLengthCells; i++)
                {
                    Vector3 p = origin + rowOff + forward * (i * stepAlong);
                    Vector3Int c = sceneGrid.WorldToCell(p);

                    // center row uses center tile if provided; otherwise fall back to upper/lower
                    TileBase t;
                    if (row == 0 && horizontal_centerTile)
                        t = horizontal_centerTile;
                    else
                        t = (row > 0) ? upperTile : lowerTile;

                    Put(t, c);
                }
            }
        }
    }
}
