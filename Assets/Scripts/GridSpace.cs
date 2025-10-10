using System.Collections.Generic;
using UnityEngine;

public static class GridSpace
{
    // Convert world ↔ cell (center)
    public static Vector3Int WorldToCell(Grid g, Vector3 w) => g.WorldToCell(w);
    public static Vector3 CellCenterWorld(Grid g, Vector3Int c) => g.GetCellCenterWorld(c);

    // Integer sign
    static int Sgn(int v) => v < 0 ? -1 : (v > 0 ? 1 : 0);

    // Perpendicular to a cell-step (sx,sy): "up" relative to the line
    public static Vector3Int PerpRight(int sx, int sy) => new Vector3Int(-sy, sx, 0);

    // All cells touched by the segment [a..b] (supercover Bresenham)
    public static List<Vector3Int> LineCells(Vector3Int a, Vector3Int b)
    {
        var cells = new List<Vector3Int>();
        int x0 = a.x, y0 = a.y, x1 = b.x, y1 = b.y;
        int dx = Mathf.Abs(x1 - x0), dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;

        int err = dx - dy;
        int x = x0, y = y0;
        cells.Add(new Vector3Int(x, y, 0));

        while (x != x1 || y != y1)
        {
            int e2 = err << 1;
            int xPrev = x, yPrev = y;
            if (e2 > -dy) { err -= dy; x += sx; }
            if (e2 < dx) { err += dx; y += sy; }
            // if we stepped in both x and y, include the corner-adjacent cells
            if (x != xPrev && y != yPrev)
            {
                cells.Add(new Vector3Int(x, yPrev, 0));
                cells.Add(new Vector3Int(xPrev, y, 0));
            }
            cells.Add(new Vector3Int(x, y, 0));
        }
        return cells;
    }

    // Convenience: from world endpoints, get core, up, down, and the two endpoints.
    public static void LineCoreUpDown(Grid g, Vector3 worldA, Vector3 worldB,
        out List<Vector3Int> core, out List<Vector3Int> up, out List<Vector3Int> down,
        out Vector3Int startCell, out Vector3Int endCell)
    {
        var a = WorldToCell(g, worldA);
        var b = WorldToCell(g, worldB);

        core = LineCells(a, b);
        startCell = core[0];
        endCell = core[core.Count - 1];

        int sx = Sgn(b.x - a.x);
        int sy = Sgn(b.y - a.y);
        var r = PerpRight(sx, sy);   // "up" relative to the line

        up = new List<Vector3Int>(core.Count);
        down = new List<Vector3Int>(core.Count);
        for (int i = 0; i < core.Count; i++)
        {
            up.Add(core[i] + r);
            down.Add(core[i] - r);
        }
    }
}
