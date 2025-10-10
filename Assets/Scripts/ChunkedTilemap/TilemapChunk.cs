using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TilemapChunk : MonoBehaviour
{
    public Vector2Int coord;            // chunk grid coordinate
    public ChunkSettings settings;      // shared settings

    public Tilemap[] layers;            // parallel to settings.layerNames
    public BoundsInt UsableBounds { get; private set; }
    public BoundsInt FullBounds { get; private set; }

    void OnEnable() { RecomputeBounds(); }

    public void RecomputeBounds()
    {
        if (!settings) return;
        int s = settings.chunkSize; int o = settings.overlap;
        Vector3Int origin = new Vector3Int(coord.x * s, coord.y * s, 0) + new Vector3Int(-o, -o, 0);
        FullBounds = new BoundsInt(origin, new Vector3Int(s + 2 * o, s + 2 * o, 1));
        UsableBounds = new BoundsInt(origin + new Vector3Int(o, o, 0), new Vector3Int(s, s, 1));
    }

    public bool ContainsCell(Vector3Int cell, bool includeOverlap)
    {
        return (includeOverlap ? FullBounds : UsableBounds).Contains(cell);
    }

    public void EnsureLayers(Grid grid, string[] names)
    {
        if (layers != null && layers.Length == names.Length) return;
        layers = new Tilemap[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            string childName = names[i];
            var t = transform.Find(childName);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(childName, typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(transform, false);
                var tr = go.GetComponent<TilemapRenderer>();
                tr.sortingOrder = names.Length - i; // Ground lowest, Decor highest
            }
            else go = t.gameObject;

            var tm = go.GetComponent<Tilemap>();
            tm.tileAnchor = Vector3.zero; // grid-aligned
            layers[i] = tm;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!settings) return;
        RecomputeBounds();
        DrawBounds(UsableBounds, new Color(0,1,0,0.15f));
        DrawBounds(FullBounds, new Color(1,1,0,0.06f));
    }
    void DrawBounds(BoundsInt b, Color c)
    {
        Gizmos.color = c;
        var min = (Vector3Int)b.min; var size = (Vector3Int)b.size;
        var p = new Vector3(min.x, min.y, 0);
        var s = new Vector3(size.x, size.y, 0);
        Gizmos.DrawCube(p + s*0.5f, s);
        Gizmos.color = new Color(c.r, c.g, c.b, c.a*1.8f);
        Gizmos.DrawWireCube(p + s*0.5f, s);
    }
#endif
}
