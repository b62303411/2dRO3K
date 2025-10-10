using UnityEngine;

/// Marqueur d'objet multi-tuiles qui s'accroche à une Grid.
/// Place ce composant sur tes prefabs "maison", "pont", etc.
[ExecuteAlways]
public class GridObject : MonoBehaviour
{
    [Tooltip("Largeur en cellules")]
    public int width = 2;
    [Tooltip("Hauteur en cellules")]
    public int height = 2;

    [Tooltip("Pivot local en cellules (0..width-1, 0..height-1). (0,0) = bas-gauche.")]
    public Vector2Int pivot = new Vector2Int(0, 0);

    [Tooltip("Nom logique de couche (ex: Structures, Props, Roads). Sert à empêcher les chevauchements dans la même couche.")]
    public string layerTag = "Structures";

    [Tooltip("Décalage vertical monde si tu veux élever l'objet au-dessus du sol.")]
    public float yOffset = 0f;

    public BoundsInt GetFootprint(Vector3Int cellBase, bool rotated90, bool flipX, bool flipY)
    {
        var w = width;
        var h = height;
        // rotation 90 = swap w/h
        if (rotated90) (w, h) = (h, w);

        // pivot appliqué post-rot/flip
        var p = GetPivotTransformed(rotated90, flipX, flipY);
        var origin = cellBase - new Vector3Int(p.x, p.y, 0);
        return new BoundsInt(origin, new Vector3Int(w, h, 1));
    }

    public Vector2Int GetPivotTransformed(bool rot90, bool fx, bool fy)
    {
        int w = width, h = height;
        int px = pivot.x, py = pivot.y;

        // flip pivot dans l’espace non-roté
        if (fx) px = (w - 1) - px;
        if (fy) py = (h - 1) - py;

        if (rot90)
        {
            // rotation 90° autour (0,0): (x,y) -> (y, w-1-x), puis w/h échangés
            int rx = py;
            int ry = (w - 1) - px;
            return new Vector2Int(rx, ry);
        }
        return new Vector2Int(px, py);
    }
}
