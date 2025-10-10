#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class GridObjectPainter : EditorWindow
{
    public Grid sceneGrid;
    public Transform parentForObjects;
    public GridObject prefab;              // le module à peindre (son root porte GridObject)
    public string layerTag = "Structures"; // pour collision logique
    public bool alignToCellCenter = true;
    public bool rotated90 = false, flipX = false, flipY = false;
    public Color okColor = new Color(0,1,0,0.35f), badColor = new Color(1,0,0,0.35f);

    Vector3Int hoverCell;
    bool canPlace;
    List<Vector3> ghostVerts = new List<Vector3>(4);

    [MenuItem("Tools/Grid Objects/Painter")]
    static void Open() => GetWindow<GridObjectPainter>("Grid Object Painter");

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        if (!sceneGrid) sceneGrid = FindObjectOfType<Grid>();
        if (!parentForObjects && sceneGrid) parentForObjects = sceneGrid.transform;
    }
    void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    void OnGUI()
    {
        sceneGrid = (Grid)EditorGUILayout.ObjectField("Scene Grid", sceneGrid, typeof(Grid), true);
        parentForObjects = (Transform)EditorGUILayout.ObjectField("Parent", parentForObjects, typeof(Transform), true);
        prefab = (GridObject)EditorGUILayout.ObjectField("Prefab (GridObject)", prefab, typeof(GridObject), false);
        layerTag = EditorGUILayout.TextField("Layer Tag", layerTag);

        alignToCellCenter = EditorGUILayout.Toggle("Align to Cell Center", alignToCellCenter);
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Transform at paint time");
        rotated90 = EditorGUILayout.ToggleLeft("Rotate 90° (R)", rotated90);
        flipX     = EditorGUILayout.ToggleLeft("Flip X (F)", flipX);
        flipY     = EditorGUILayout.ToggleLeft("Flip Y (G)", flipY);

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox("SceneView:\n- Clic gauche: placer\n- R: rotate 90°\n- F: flip X\n- G: flip Y\n- Échap: annuler sélection", MessageType.Info);
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!sceneGrid || !prefab) return;

        // hotkeys
        var e = Event.current;
        if (e.type == EventType.KeyDown)
        {
            if (e.keyCode == KeyCode.R) { rotated90 = !rotated90; Repaint(); e.Use(); }
            if (e.keyCode == KeyCode.F) { flipX = !flipX; Repaint(); e.Use(); }
            if (e.keyCode == KeyCode.G) { flipY = !flipY; Repaint(); e.Use(); }
            if (e.keyCode == KeyCode.Escape) { prefab = null; Repaint(); e.Use(); }
        }

        // ray → cell
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        Plane plane = new Plane(Vector3.back, Vector3.zero); // XY
        if (plane.Raycast(ray, out float dist))
        {
            Vector3 world = ray.GetPoint(dist);
            hoverCell = sceneGrid.WorldToCell(world);
        }

        // footprint + validité
        var fp = prefab.GetFootprint(hoverCell, rotated90, flipX, flipY);
        canPlace = IsAreaFree(fp, parentForObjects, layerTag);

        // draw ghost
        DrawFootprint(fp, canPlace ? okColor : badColor);

        // click = place
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.control && !e.command)
        {
            if (canPlace)
            {
                PlaceAt(fp);
                e.Use();
            }
        }

        // forcer repaint
        sv.Repaint();
    }

    bool IsAreaFree(BoundsInt area, Transform parent, string layer)
    {
        var found = parent ? parent.GetComponentsInChildren<GridObject>(true) : Object.FindObjectsOfType<GridObject>();
        foreach (var go in found)
        {
            if (go.layerTag != layer) continue;

            // approx: on compare les BoundsInt d'instances existantes
            var posCell = WorldToCell(go.transform.position);
            var other = go.GetFootprint(posCell, false, false, false); // stocké posé sans rot? on fait mieux:
            // on tente de lire un cache sur l'instance si dispo
            var cache = go.gameObject.GetComponent<GridObjectRuntimeFootprint>();
            if (cache) other = cache.lastFootprint;

            if (Intersects(area, other)) return false;
        }
        return true;
    }

    void PlaceAt(BoundsInt fp)
    {
        var wpos = CellToWorld(fp.position + new Vector3Int(0,0,0));
        if (alignToCellCenter) wpos += sceneGrid.cellSize/2f;

        var inst = (GridObject)PrefabUtility.InstantiatePrefab(prefab, parentForObjects);
        Undo.RegisterCreatedObjectUndo(inst.gameObject, "Place GridObject");
        inst.transform.position = new Vector3(wpos.x, wpos.y + inst.yOffset, 0f);

        // appliquer rotation/flip visuel
        float rotZ = rotated90 ? 90f : 0f;
        inst.transform.rotation = Quaternion.Euler(0,0,rotZ);
        Vector3 scale = Vector3.one;
        if (flipX) scale.x *= -1f;
        if (flipY) scale.y *= -1f;
        inst.transform.localScale = scale;

        // mémoriser l’emprise réelle (pour les checks suivants)
        var cache = inst.gameObject.AddComponent<GridObjectRuntimeFootprint>();
        cache.lastFootprint = fp;

        // tag logique (pour anti-chevauchement futur)
        inst.layerTag = layerTag;

        Selection.activeObject = inst.gameObject;
    }

    // util
    Vector3 CellToWorld(Vector3Int c) => sceneGrid.CellToWorld(c);
    Vector3Int WorldToCell(Vector3 w) => sceneGrid.WorldToCell(w);

    static bool Intersects(BoundsInt a, BoundsInt b)
    {
        bool sepX = a.xMax <= b.xMin || b.xMax <= a.xMin;
        bool sepY = a.yMax <= b.yMin || b.yMax <= a.yMin;
        return !(sepX || sepY);
    }

    void DrawFootprint(BoundsInt fp, Color col)
    {
        Handles.color = col;
        Vector3 s = sceneGrid.cellSize;
        Vector3 o = Vector3.zero;
        for (int y = fp.yMin; y < fp.yMax; y++)
        for (int x = fp.xMin; x < fp.xMax; x++)
        {
            var p = new Vector3Int(x,y,0);
            var w = CellToWorld(p);
            if (alignToCellCenter) w += s/2f;
            var rmin = w - s/2f; var rmax = w + s/2f;
            Handles.DrawSolidRectangleWithOutline(new Rect(rmin, s), new Color(col.r,col.g,col.b,0.2f), new Color(col.r,col.g,col.b,0.8f));
        }
    }
}

// petit cache pour mémoriser l’emprise posée
public class GridObjectRuntimeFootprint : MonoBehaviour
{
    public BoundsInt lastFootprint;
}
#endif
