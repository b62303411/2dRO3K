// Assets/Editor/TilemapHoverCoords.cs
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class TilemapHoverCoords
{
    // Toggle rapide si tu veux couper/activer l’affichage
    const string MenuPath = "Tools/Tilemap Hover Coords (toggle)";
    static bool _enabled = true;

    static TilemapHoverCoords()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
        Menu.SetChecked(MenuPath, _enabled);
    }

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        _enabled = !_enabled;
        Menu.SetChecked(MenuPath, _enabled);
        SceneView.RepaintAll();
    }

    private struct GridHit
    {
        public Grid grid;
        public Vector3 worldPoint;
        public Vector3Int cell;
        public Tilemap tilemap;   // premier tilemap trouvé qui a un tile à cette cell (peut être null)
        public float rayDistance; // pour départager si plusieurs plans s’entrecoupent
    }

    static void OnSceneGUI(SceneView sv)
    {
        if (!_enabled) return;

        Event e = Event.current;
        if (e == null) return;

        // Ray depuis la souris dans la SceneView
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        // Parcourt tous les Grid actifs de la scène (même désactivés dans la hiérarchie => true)
        Grid[] grids = Object.FindObjectsOfType<Grid>(true);
        GridHit? best = null;

        foreach (var grid in grids)
        {
            if (grid.name.StartsWith("Grid", System.StringComparison.OrdinalIgnoreCase))
                continue;
            // Définit un plan passant par l'origine du Grid et orienté selon son forward.
            // (Convient pour Grid 2D, isométrique, hex tant que "forward" est la normale du plan)
            var plane = new Plane(grid.transform.forward, grid.transform.position);

            if (!plane.Raycast(ray, out float dist))
                continue;

            Vector3 world = ray.GetPoint(dist);
            Vector3Int cell = grid.WorldToCell(world);

            // On n’affiche pas les cellules "vides" d’un Grid infini — on vérifie s’il y a au moins un tile.
            Tilemap foundTm = null;
            // Récupère tous les Tilemap ENFANTS de ce Grid (désactivés inclus)
            List<Tilemap> tms = s_TilemapBuffer;
            tms.Clear();
            grid.GetComponentsInChildren(true, tms);

            foreach (var tm in tms)
            {
                // Ignore les TilemapRenderer masqués si tu veux, sinon commente cette ligne
                var r = tm.GetComponent<TilemapRenderer>();
                if (r && !r.enabled) continue;

                // HasTile utilise les coords du Grid parent, donc ok
                if (tm.HasTile(cell))
                {
                    foundTm = tm;
                    break;
                }
            }

            // Si aucun tile trouvé sous la souris pour ce Grid, on peut soit ignorer,
            // soit accepter quand même (show-empty-cells). Ici : on ignore — comportement utile en prod.
            if (foundTm == null) continue;

            var hit = new GridHit
            {
                grid = grid,
                worldPoint = world,
                cell = cell,
                tilemap = foundTm,
                rayDistance = dist
            };

            if (best == null || hit.rayDistance < best.Value.rayDistance)
                best = hit;
        }

        if (best == null) return;

        DrawHitGizmos(sv, best.Value);
    }

    static readonly List<Tilemap> s_TilemapBuffer = new List<Tilemap>(32);

    static void DrawHitGizmos(SceneView sv, GridHit hit)
    {
        var grid = hit.grid;
        var cell = hit.cell;

        // Centre monde de la cellule (via le Grid)
        Vector3 center = grid.GetCellCenterWorld(cell);

        // Taille visuelle de la cellule
        Vector3 size = grid.cellSize;
        if (size == Vector3.zero) size = Vector3.one; // sécurité au cas où

        // Outline + croix
        Handles.color = Color.yellow;
        Handles.DrawWireCube(center, size);
        Handles.DrawLine(center + Vector3.right * (size.x * 0.5f), center - Vector3.right * (size.x * 0.5f));
        Handles.DrawLine(center + Vector3.up * (size.y * 0.5f), center - Vector3.up * (size.y * 0.5f));

        // Labels à l'écran
        Handles.BeginGUI();
        Vector2 guiP = HandleUtility.WorldToGUIPoint(center);

        var y = guiP.y - 30f;
        GUI.Label(new Rect(guiP.x + 12, y, 360, 20), $"Grid: {grid.name}  (layer: {grid.gameObject.layer})");
        y += 16;
        GUI.Label(new Rect(guiP.x + 12, y, 360, 20), $"Cell: {cell.x}, {cell.y}, {cell.z}");
        y += 16;
        if (hit.tilemap != null)
            GUI.Label(new Rect(guiP.x + 12, y, 360, 20), $"Tilemap: {hit.tilemap.name} (has tile)");
        Handles.EndGUI();

        // Status bar (discret)
        sv.ShowNotification(new GUIContent($"Grid: {grid.name} | Cell: {cell} | Tilemap: {hit.tilemap?.name ?? "none"}"));
    }
}
