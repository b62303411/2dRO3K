using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Splines;

[ExecuteAlways]
[RequireComponent(typeof(SplineContainer))]
public class GridSplinePainter : MonoBehaviour
{
    public enum OutputMode { TilesToTilemap, Sprites }
    [Header("Common")]
    public Grid sceneGrid;
    public OutputMode output = OutputMode.TilesToTilemap;
    [Min(0.05f)] public float spacingWorld = 0.25f; // sampling step along spline (world units)
    [Min(0)] public int halfWidth = 0;              // 0 => center only; 1 => 3-wide; 2 => 5-wide...
    public bool widthPerpToSpline = true;           // use tangent⊥ for lateral bands

    [Header("Rules: bands & caps")]
    // band i = lateral offset band (0=center, >0 = steps outward). You can set same tile for all.
    public TileBase centerTile;                     // band 0
    public TileBase sideTile;                       // bands ±1..±halfWidth
    public bool addEndCaps = true;                  // 3×3 by default (below)
    [Min(1)] public int capLenCount = 3;            // along tangent (min 3)
    [Min(1)] public int capHalfWidth = 1;           // across (1 => 3 high)

    [Header("Tiles → Tilemap")]
    public Tilemap targetTilemap;                   // pick your layer Tilemap
    public bool clearCoveredAreaBeforeBake = false; // clears only where we write

    [Header("Sprites mode")]
    public GameObject spritePrefab;                 // if null, uses SpriteRenderer with spriteFallback
    public Sprite spriteFallback;
    public string sortingLayer = "Foreground";
    public int sortingOrder = 0;
    public Transform spritesParent;
    public bool destroyPreviousSprites = true;

    // internal
    readonly List<GameObject> spawned = new();
    SplineContainer sc;

    void OnEnable()
    {
        sc = GetComponent<SplineContainer>();
        if (!sceneGrid) sceneGrid = GetComponentInParent<Grid>();
    }

    // Call this when you tweak the spline or fields (works in Edit mode)
    public void Bake()
    {
        if (sc == null || sc.Spline == null || sc.Spline.Count < 2) { Debug.LogWarning("Spline invalid."); return; }
        if (output == OutputMode.TilesToTilemap) BakeTiles();
        else BakeSprites();
    }

    public void ClearSprites()
    {
        foreach (var go in spawned) if (go) DestroyImmediate(go);
        spawned.Clear();
    }

    // ---------------- TILES ----------------
    void BakeTiles()
    {
        if (!sceneGrid || !targetTilemap) { Debug.LogWarning("Assign sceneGrid + targetTilemap."); return; }
        if (!centerTile && !sideTile) { Debug.LogWarning("Assign at least one tile (center or side)."); return; }

        var cellsByTile = new Dictionary<TileBase, HashSet<Vector3Int>>();
        void Put(TileBase t, Vector3Int c)
        {
            if (!t) return;
            if (!cellsByTile.TryGetValue(t, out var set)) cellsByTile[t] = set = new HashSet<Vector3Int>();
            set.Add(c);
        }

        var trackCells = new HashSet<Vector3Int>();
        SampleAlongPath((world, tangent) =>
        {
            var right = widthPerpToSpline ? new Vector3(tangent.y, -tangent.x, 0f).normalized : Vector3.right;
            for (int w = -halfWidth; w <= halfWidth; w++)
            {
                var offset = right * (w * sceneGrid.cellSize.x);
                var cell = sceneGrid.WorldToCell(world + offset);
                trackCells.Add(cell);
                Put(w == 0 ? centerTile : sideTile, cell);
            }
        });

        if (addEndCaps)
        {
            AddCapsTiles(0f, +1); // start
            AddCapsTiles(1f, -1); // end
        }

        // optionally clear only the covered area
        if (clearCoveredAreaBeforeBake)
        {
            foreach (var c in trackCells) targetTilemap.SetTile(c, null);
        }

        // write tiles grouped by tile type (minor perf win)
        foreach (var kvp in cellsByTile)
        {
            foreach (var c in kvp.Value) targetTilemap.SetTile(c, kvp.Key);
        }
        targetTilemap.RefreshAllTiles();

        // local function for caps
        void AddCapsTiles(float tEnd, int dirSign)
        {
            sc.Evaluate(tEnd, out var pos, out var tan, out var up);
            var origin = transform.TransformPoint((Vector3)pos);
            var fwd = (transform.TransformDirection((Vector3)tan)).normalized * dirSign;
            var right = widthPerpToSpline ? new Vector3(fwd.y, -fwd.x, 0f).normalized : Vector3.right;

            // step sizes = cell size
            float stepAlong = sceneGrid.cellSize.x;
            float stepAcross = sceneGrid.cellSize.y;

            for (int row = -capHalfWidth; row <= capHalfWidth; row++)
            {
                var rowOff = right * (row * stepAcross);
                for (int i = 0; i < capLenCount; i++)
                {
                    var p = origin + rowOff + fwd * (i * stepAlong);
                    var cell = sceneGrid.WorldToCell(p);
                    trackCells.Add(cell);
                    Put(row == 0 ? centerTile : sideTile, cell);
                }
            }
        }
    }

    // ---------------- SPRITES ----------------
    void BakeSprites()
    {
        if (destroyPreviousSprites) ClearSprites();
        if (!spritesParent) spritesParent = transform;
        if (!spritePrefab && !spriteFallback) { Debug.LogWarning("Sprite mode: assign prefab or sprite."); return; }

        var placed = new HashSet<Vector2>(); // dedupe
        void Spawn(Vector3 world, float rotZ)
        {
            var key = new Vector2(Mathf.Round(world.x * 1000f), Mathf.Round(world.y * 1000f));
            if (!placed.Add(key)) return;

            GameObject go;
            if (spritePrefab)
            {
                go = (Application.isPlaying ? Instantiate(spritePrefab, spritesParent)
                                            : (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(spritePrefab, spritesParent));
                go.transform.SetPositionAndRotation(world, Quaternion.Euler(0, 0, rotZ));
            }
            else
            {
                go = new GameObject("SplineSprite");
                go.transform.SetPositionAndRotation(world, Quaternion.Euler(0, 0, rotZ));
                go.transform.SetParent(spritesParent, true);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = spriteFallback;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder;
            }
            var r = go.GetComponentInChildren<SpriteRenderer>();
            if (r) { r.sortingLayerName = sortingLayer; r.sortingOrder = sortingOrder; }
            spawned.Add(go);
        }

        // main run
        SampleAlongPath((world, tangent) =>
        {
            float rotZ = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
            Spawn(world, rotZ);
        });

        // 3×3 caps (or custom) in sprite mode too
        if (addEndCaps)
        {
            AddSpriteCaps(0f, +1);
            AddSpriteCaps(1f, -1);
        }

        void AddSpriteCaps(float tEnd, int dirSign)
        {
            sc.Evaluate(tEnd, out var pos, out var tan, out var up);
            var origin = transform.TransformPoint((Vector3)pos);
            var fwd = (transform.TransformDirection((Vector3)tan)).normalized * dirSign;
            var right = new Vector3(fwd.y, -fwd.x, 0f).normalized;

            // step sizes try sprite bounds; fallback to grid cell size or spacing
            var step = GuessSpriteStep();
            float stepAlong = step.x;
            float stepAcross = step.y;

            for (int row = -capHalfWidth; row <= capHalfWidth; row++)
            {
                var rowOff = right * (row * stepAcross);
                for (int i = 0; i < capLenCount; i++)
                {
                    var p = origin + rowOff + fwd * (i * stepAlong);
                    float rotZ = Mathf.Atan2(fwd.y, fwd.x) * Mathf.Rad2Deg;
                    Spawn(p, rotZ);
                }
            }
        }
    }

    // ---- helpers ----
    void SampleAlongPath(System.Action<Vector3, Vector2> onSample)
    {
        float total = sc.Spline.GetLength();
        float step = Mathf.Max(0.05f, spacingWorld);
        for (float d = 0; d <= total + 1e-4f; d += step)
        {
            sc.Evaluate(Mathf.Clamp01(d / total), out var pos, out var tan, out var up);
            var world = transform.TransformPoint((Vector3)pos);
            var fwd = (Vector2)(transform.TransformDirection((Vector3)tan)).normalized;
            onSample(world, fwd);
        }
    }

    Vector2 GuessSpriteStep()
    {
        if (spritePrefab)
        {
            var sr = spritePrefab.GetComponentInChildren<SpriteRenderer>();
            if (sr && sr.sprite) return sr.bounds.size;
        }
        if (spriteFallback) return spriteFallback.bounds.size;
        if (sceneGrid) return new Vector2(sceneGrid.cellSize.x, sceneGrid.cellSize.y);
        return new Vector2(Mathf.Max(0.1f, spacingWorld), Mathf.Max(0.1f, spacingWorld));
    }

    void OnDrawGizmosSelected()
    {
        if (!sceneGrid) return;
        // visualize width band
        Gizmos.color = new Color(1, 1, 0, 0.25f);
        SampleAlongPath((world, tangent) =>
        {
            var right = widthPerpToSpline ? new Vector3(tangent.y, -tangent.x, 0f).normalized : Vector3.right;
            var a = world + right * (-halfWidth * sceneGrid.cellSize.x);
            var b = world + right * (halfWidth * sceneGrid.cellSize.x);
            Gizmos.DrawLine(a, b);
        });
    }
}
