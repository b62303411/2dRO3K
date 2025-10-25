using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Resolves all RuleTiles under a Grid, then clones the Grid hierarchy
/// and writes a plain (static) Tile version into the clone.
/// No chunking. Full-grid refresh, then copy. Deferred by cellsPerFrame.
/// </summary>
public class GridBakerPlainCopy : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Root Grid containing Tilemaps to bake.")]
    public Grid sourceGrid;

    [Header("Output")]
    [Tooltip("Optional parent for the baked copy. If null, copy sits next to source.")]
    public Transform outputParent;
    [Tooltip("Suffix to append to the cloned grid name.")]
    public string outputGridSuffix = "_BAKED";

    [Header("Deferral / Budget")]
    [Tooltip("Max cells written per frame across the whole process.")]
    public int cellsPerFrame = 1000;
    [Tooltip("Frames to wait after RefreshAllTiles() before reading sprites.")]
    public int settleFrames = 1;

    [Header("Rules")]
    [Tooltip("If true, only copy cells that are RuleTiles (others left empty in the baked copy). If false, copy any Tile as plain.")]
    public bool onlyBakeRuleTiles = true;
    [Tooltip("Copy collider type when source cell is a Tile with collider set.")]
    public bool copyColliderType = false;

    [Header("Renderer copy")]
    public bool copySorting = true;
    public bool copyMaterials = true;
    public bool copyMaskInteraction = true;

    private bool _busy;

    void Reset()
    {
        sourceGrid = GetComponent<Grid>();
    }

    [ContextMenu("Bake Grid (Plain Copy)")]
    public void Bake()
    {
        if (_busy) return;
        if (!sourceGrid)
        {
            Debug.LogError("[GridBakerPlainCopy] No source Grid assigned.");
            return;
        }
        StartCoroutine(BakeRoutine());
    }

    private IEnumerator BakeRoutine()
    {
        _busy = true;

        // 1) Collect all tilemaps (include inactive)
        var srcTilemaps = sourceGrid.GetComponentsInChildren<Tilemap>(true);
        if (srcTilemaps.Length == 0)
        {
            Debug.LogWarning("[GridBakerPlainCopy] No Tilemaps found under the Grid.");
            _busy = false;
            yield break;
        }

        // 2) Force a full resolve once
        foreach (var tm in srcTilemaps) tm.RefreshAllTiles();
        for (int i = 0; i < Mathf.Max(1, settleFrames); i++) yield return null;

        // 3) Create the destination Grid (clone transform + settings)
        GameObject dstRootGO = new GameObject(sourceGrid.gameObject.name + outputGridSuffix);
        if (outputParent) dstRootGO.transform.SetParent(outputParent, worldPositionStays: true);
        else dstRootGO.transform.SetParent(sourceGrid.transform.parent, worldPositionStays: true);

        CopyTransform(sourceGrid.transform, dstRootGO.transform);

        var dstGrid = dstRootGO.AddComponent<Grid>();
        CopyGridSettings(sourceGrid, dstGrid);

        // 4) Recreate hierarchy + tilemaps + renderers
        var dstMapOf = new Dictionary<Tilemap, Tilemap>(srcTilemaps.Length);
        foreach (var srcTM in srcTilemaps)
        {
            Transform dstParent = EnsureParentChain(srcTM.transform.parent, sourceGrid.transform, dstRootGO.transform);

            var dstTMGO = new GameObject(srcTM.gameObject.name);
            dstTMGO.transform.SetParent(dstParent, false);
            CopyTransform(srcTM.transform, dstTMGO.transform);

            var dstTM = dstTMGO.AddComponent<Tilemap>();
            CopyTilemapSettings(srcTM, dstTM);

            var srcRend = srcTM.GetComponent<TilemapRenderer>();
            if (srcRend)
            {
                var dstRend = dstTMGO.AddComponent<TilemapRenderer>();
                CopyRendererSettings(srcRend, dstRend, copySorting, copyMaterials, copyMaskInteraction);
            }

            dstMapOf[srcTM] = dstTM;
        }

        yield return null;

        // 5) Single pass copy (deferred by cellsPerFrame)
        int budget = Mathf.Max(1, cellsPerFrame);

        foreach (var srcTM in srcTilemaps)
        {
            var dstTM = dstMapOf[srcTM];
            BoundsInt b = srcTM.cellBounds;

            // iterate every cell in bounds
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var pos = new Vector3Int(x, y, 0);

                    TileBase srcTB = srcTM.GetTile(pos);
                    if (srcTB == null) continue;

                    if (onlyBakeRuleTiles && !(srcTB is RuleTile))
                        continue;

                    Sprite s = srcTM.GetSprite(pos);
                    if (!s)
                    {
                        // if the rule resolved to empty, ensure no tile at dst
                        dstTM.SetTile(pos, null);
                        continue;
                    }

                    var baked = ScriptableObject.CreateInstance<Tile>();
                    baked.sprite = s;
                    baked.color = srcTM.GetColor(pos);
                    baked.transform = srcTM.GetTransformMatrix(pos);

                    if (copyColliderType)
                    {
                        if (srcTB is Tile tSrc) baked.colliderType = tSrc.colliderType;
                        else baked.colliderType = Tile.ColliderType.None;
                    }

                    dstTM.SetTile(pos, baked);

                    // throttle by global budget
                    if (--budget <= 0)
                    {
                        budget = Mathf.Max(1, cellsPerFrame);
                        yield return null;
                    }
                }

            dstTM.RefreshAllTiles();
            yield return null;
        }

        Debug.Log($"[GridBakerPlainCopy] Done. Output -> {dstRootGO.name}");
        _busy = false;
    }

    // ---------- helpers ----------

    private static void CopyTransform(Transform src, Transform dst)
    {
        dst.localPosition = src.localPosition;
        dst.localRotation = src.localRotation;
        dst.localScale = src.localScale;
    }

    private static void CopyGridSettings(Grid src, Grid dst)
    {
        dst.cellSize = src.cellSize;
        dst.cellGap = src.cellGap;
        dst.cellLayout = src.cellLayout;
        dst.cellSwizzle = src.cellSwizzle;
    }

    private static void CopyTilemapSettings(Tilemap src, Tilemap dst)
    {
        dst.animationFrameRate = src.animationFrameRate;
        dst.color = src.color;
        dst.tileAnchor = src.tileAnchor;
        dst.orientation = src.orientation;
        dst.orientationMatrix = src.orientationMatrix;
        // bounds are driven by SetTile calls; no need to copy
    }

    private static void CopyRendererSettings(
        TilemapRenderer src, TilemapRenderer dst,
        bool copySorting, bool copyMaterials, bool copyMaskInteraction)
    {
        dst.mode = src.mode;
        dst.detectChunkCullingBounds = src.detectChunkCullingBounds;
        dst.chunkCullingBounds = src.chunkCullingBounds;

        if (copySorting)
        {
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = src.sortingOrder;
        }
        if (copyMaterials)
        {
            dst.material = src.material;
            dst.sharedMaterial = src.sharedMaterial;
            var mats = src.sharedMaterials;
            if (mats != null && mats.Length > 0) dst.sharedMaterials = mats;
        }
#if UNITY_2021_2_OR_NEWER
        if (copyMaskInteraction) dst.maskInteraction = src.maskInteraction;
#endif
    }

    private static Transform EnsureParentChain(Transform srcParent, Transform stopAt, Transform dstRoot)
    {
        var stack = new Stack<Transform>();
        var cur = srcParent;
        while (cur && cur != stopAt)
        {
            stack.Push(cur);
            cur = cur.parent;
        }

        Transform dst = dstRoot;
        while (stack.Count > 0)
        {
            var s = stack.Pop();
            var existing = dst.Find(s.name);
            if (!existing)
            {
                var go = new GameObject(s.name);
                go.transform.SetParent(dst, false);
                CopyTransform(s, go.transform);
                existing = go.transform;
            }
            dst = existing;
        }
        return dst;
    }
}
