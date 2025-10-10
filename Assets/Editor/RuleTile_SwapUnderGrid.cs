using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuleTile_SwapUnderGrid
{
    [MenuItem("Tools/Tiles/Swap RuleTile -> CrossPartitionRuleTile UNDER Selected Grid")]
    public static void SwapUnderSelectedGrid()
    {
        var gridGO = Selection.activeGameObject;
        if (!gridGO) { EditorUtility.DisplayDialog("Select a Grid", "Select the parent Grid GameObject.", "OK"); return; }
        var rootGrid = gridGO.GetComponent<Grid>();
        if (!rootGrid) { EditorUtility.DisplayDialog("Not a Grid", "Selected object has no Grid.", "OK"); return; }

        // 1) Gather tilemaps under this Grid (including child Grids)
        var tilemaps = new List<Tilemap>();
        rootGrid.GetComponentsInChildren(true, tilemaps);
        if (tilemaps.Count == 0) { EditorUtility.DisplayDialog("No Tilemaps", "No Tilemaps under this Grid.", "OK"); return; }

        // 2) Discover all RuleTiles actually used under this Grid (agnostic parse)
        var usedRuleTiles = new HashSet<RuleTile>(new RefComparer<RuleTile>());
        foreach (var tm in tilemaps)
        {
            if (!tm) continue;
            var b = tm.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var t = tm.GetTile(new Vector3Int(x, y, 0));
                    if (t is RuleTile rt) usedRuleTiles.Add(rt);
                }
        }
        if (usedRuleTiles.Count == 0) { EditorUtility.DisplayDialog("Nothing to swap", "No RuleTiles found under this Grid.", "OK"); return; }

        // 3) Build mapping RuleTile -> CrossPartitionRuleTile (auto-create if missing)
        var mapping = new Dictionary<RuleTile, CrossPartitionRuleTile>(new RefComparer<RuleTile>());
        int createdAssets = 0;
        foreach (var rt in usedRuleTiles)
        {
            var cross = FindSiblingCross(rt);
            if (!cross) { cross = CreateCrossBeside(rt); if (cross) createdAssets++; }
            if (cross) mapping[rt] = cross;
        }

        // 4) Swap cells
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        int scanned = 0, replaced = 0, affectedMaps = 0;

        try
        {
            int tmIndex = 0;
            foreach (var tm in tilemaps)
            {
                if (!tm) continue;
                bool changed = false;
                Undo.RegisterFullObjectHierarchyUndo(tm.gameObject, "Swap RuleTiles");

                var b = tm.cellBounds;
                for (int y = b.yMin; y < b.yMax; y++)
                    for (int x = b.xMin; x < b.xMax; x++)
                    {
                        var p = new Vector3Int(x, y, 0);
                        var t = tm.GetTile(p);
                        if (!t) continue;
                        scanned++;

                        if (t is RuleTile rt && mapping.TryGetValue(rt, out var cross) && cross && t != cross)
                        {
                            tm.SetTile(p, cross);
                            changed = true;
                            replaced++;
                        }
                    }

                if (changed)
                {
                    tm.CompressBounds();
                    tm.RefreshAllTiles();
                    EditorUtility.SetDirty(tm);
                    affectedMaps++;
                }

                EditorUtility.DisplayProgressBar("Swapping tiles", tm.name, (float)(++tmIndex) / tilemaps.Count);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Done",
            $"Grid: {rootGrid.gameObject.name}\nTilemaps touched: {affectedMaps}/{tilemaps.Count}\nCells scanned: {scanned}\nReplaced: {replaced}\nNew Cross assets: {createdAssets}\nUnique RuleTiles mapped: {mapping.Count}",
            "OK"
        );
    }

    // -------- helpers --------

    private static CrossPartitionRuleTile FindSiblingCross(RuleTile src)
    {
        var path = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(path)) return null;
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var candidate = Path.Combine(dir, name + "_Cross.asset");
        return AssetDatabase.LoadAssetAtPath<CrossPartitionRuleTile>(candidate);
    }

    private static CrossPartitionRuleTile CreateCrossBeside(RuleTile src)
    {
        var path = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(path)) return null;
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var dstPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, name + "_Cross.asset"));

        var dst = ScriptableObject.CreateInstance<CrossPartitionRuleTile>();
        CloneRuleTileData(src, dst);

        AssetDatabase.CreateAsset(dst, dstPath);
        EditorUtility.SetDirty(dst);
        return dst;
    }

    private static void CloneRuleTileData(RuleTile src, CrossPartitionRuleTile dst)
    {
        // base fields
        dst.m_DefaultSprite = src.m_DefaultSprite;
        dst.m_DefaultGameObject = src.m_DefaultGameObject;
        dst.m_DefaultColliderType = src.m_DefaultColliderType;
        //dst.m_MinAnimationSpeed = src.m_MinAnimationSpeed;
        //dst.m_MaxAnimationSpeed = src.m_MaxAnimationSpeed;
        //dst.m_AnimationStartTime = src.m_AnimationStartTime;
        //dst.m_RandomTransform = src.m_RandomTransform;

        // rules (version-robust via JSON)
        dst.m_TilingRules = new List<RuleTile.TilingRule>(src.m_TilingRules.Count);
        foreach (var r in src.m_TilingRules)
        {
            var nr = new RuleTile.TilingRule();
            var json = JsonUtility.ToJson(r);
            JsonUtility.FromJsonOverwrite(json, nr);

            if (r.m_Sprites != null) nr.m_Sprites = (Sprite[])r.m_Sprites.Clone();
            if (r.m_Neighbors != null) nr.m_Neighbors = new List<int>(r.m_Neighbors);
            if (r.m_NeighborPositions != null) nr.m_NeighborPositions = new List<Vector3Int>(r.m_NeighborPositions);

            dst.m_TilingRules.Add(nr);
        }
        // IMPORTANT: don't assign scene objects here (dst.index stays null). Autowire at runtime.
    }

    private sealed class RefComparer<T> : IEqualityComparer<T> where T : Object
    {
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T obj) => obj ? obj.GetInstanceID() : 0;
    }
}
