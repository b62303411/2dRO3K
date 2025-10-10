#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GridSplinePainter))]
public class GridSplinePainterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var t = (GridSplinePainter)target;

        t.sceneGrid = (Grid)EditorGUILayout.ObjectField("Scene Grid", t.sceneGrid, typeof(Grid), true);
        t.output = (GridSplinePainter.OutputMode)EditorGUILayout.EnumPopup("Output Mode", t.output);
        t.spacingWorld = EditorGUILayout.FloatField("Spacing (world)", t.spacingWorld);
        t.halfWidth = EditorGUILayout.IntField(new GUIContent("Half Width (cells)","0=1 wide, 1=3 wide..."), t.halfWidth);
        t.widthPerpToSpline = EditorGUILayout.Toggle("Width Perp to Spline", t.widthPerpToSpline);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
        t.centerTile = (UnityEngine.Tilemaps.TileBase)EditorGUILayout.ObjectField("Center Tile", t.centerTile, typeof(UnityEngine.Tilemaps.TileBase), false);
        t.sideTile   = (UnityEngine.Tilemaps.TileBase)EditorGUILayout.ObjectField("Side Tile", t.sideTile, typeof(UnityEngine.Tilemaps.TileBase), false);
        t.addEndCaps = EditorGUILayout.Toggle("Add End Caps", t.addEndCaps);
        if (t.addEndCaps)
        {
            t.capLenCount  = EditorGUILayout.IntField("Cap Length (sprites/tiles)", t.capLenCount);
            t.capHalfWidth = EditorGUILayout.IntField("Cap Half-Width", t.capHalfWidth);
        }

        EditorGUILayout.Space();
        if (t.output == GridSplinePainter.OutputMode.TilesToTilemap)
        {
            EditorGUILayout.LabelField("Tiles → Tilemap", EditorStyles.boldLabel);
            t.targetTilemap = (UnityEngine.Tilemaps.Tilemap)EditorGUILayout.ObjectField("Target Tilemap (layer)", t.targetTilemap, typeof(UnityEngine.Tilemaps.Tilemap), true);
            t.clearCoveredAreaBeforeBake = EditorGUILayout.Toggle("Clear Covered Area First", t.clearCoveredAreaBeforeBake);
        }
        else
        {
            EditorGUILayout.LabelField("Sprites Mode", EditorStyles.boldLabel);
            t.spritePrefab = (GameObject)EditorGUILayout.ObjectField("Sprite Prefab", t.spritePrefab, typeof(GameObject), false);
            t.spriteFallback = (Sprite)EditorGUILayout.ObjectField("Sprite (fallback)", t.spriteFallback, typeof(Sprite), false);
            // sorting layer popup
            var layers = SortingLayer.layers;
            var names = new string[layers.Length];
            int sel = 0;
            for (int i=0;i<layers.Length;i++){ names[i]=layers[i].name; if (layers[i].name==t.sortingLayer) sel=i; }
            sel = EditorGUILayout.Popup("Sorting Layer", sel, names);
            if (names.Length>0) t.sortingLayer = names[Mathf.Clamp(sel,0,names.Length-1)];
            t.sortingOrder = EditorGUILayout.IntField("Order in Layer", t.sortingOrder);
            t.spritesParent = (Transform)EditorGUILayout.ObjectField("Sprites Parent", t.spritesParent, typeof(Transform), true);
            t.destroyPreviousSprites = EditorGUILayout.Toggle("Clear Old Sprites", t.destroyPreviousSprites);
        }

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Bake / Update", GUILayout.Height(28))) t.Bake();
            if (t.output == GridSplinePainter.OutputMode.Sprites)
                if (GUILayout.Button("Clear Sprites", GUILayout.Height(28))) t.ClearSprites();
        }

        if (GUI.changed) EditorUtility.SetDirty(target);
    }
}
#endif
