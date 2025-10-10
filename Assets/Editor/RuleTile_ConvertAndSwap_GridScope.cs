using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuleTile_ConvertAndSwap_GridScope
{
    // === A) Convertit les RuleTile assets sélectionnés en CrossPartitionRuleTile assets (optionnel) ===
    [MenuItem("Tools/Tiles/Convert Selected RuleTiles -> CrossPartitionRuleTile Assets")]
    public static void ConvertSelectedAssets()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            EditorUtility.DisplayDialog("Rien de sélectionné", "Sélectionne un ou plusieurs RuleTile assets.", "OK");
            return;
        }

        int created = 0, skipped = 0;
        foreach (var o in objs)
        {
            if (o is RuleTile rt)
            {
                var dst = CreateCrossAssetBeside(rt);
                if (dst) created++; else skipped++;
            }
            else skipped++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Conversion terminée", $"Créés: {created}\nIgnorés: {skipped}", "OK");
    }

    // === B) Swap sous le Grid sélectionné **uniquement** ===
    [MenuItem("Tools/Tiles/Swap RuleTile -> CrossPartitionRuleTile under Selected Grid")]
    public static void SwapUnderSelectedGrid()
    {
        // 1) Récupérer le Grid sélectionné
        var go = Selection.activeGameObject;
        if (!go)
        {
            EditorUtility.DisplayDialog("Aucun Grid sélectionné",
                "Sélectionne un GameObject qui contient le Grid parent.", "OK");
            return;
        }
        var rootGrid = go.GetComponent<Grid>();
        if (!rootGrid)
        {
            EditorUtility.DisplayDialog("Mauvaise sélection",
                "Le GameObject sélectionné n'a pas de composant Grid.", "OK");
            return;
        }

        // 2) Lister toutes les Tilemaps **sous** ce Grid (incluant sous-Grids)
        var tilemaps = new List<Tilemap>();
        rootGrid.GetComponentsInChildren(true, tilemaps);
        if (tilemaps.Count == 0)
        {
            EditorUtility.DisplayDialog("Pas de Tilemap",
                "Aucune Tilemap trouvée sous ce Grid.", "OK");
            return;
        }

        // 3) Construire/compléter le mapping RuleTile -> Cross (création si nécessaire)
        var mapping = BuildOrCreateMappingForRuleTilesReferencedBy(tilemaps);

        // 4) Parcours + swap
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        int scanned = 0, replaced = 0, affectedTilemaps = 0;

        try
        {
            foreach (var tm in tilemaps)
            {
                if (!tm) continue;

                bool tmChanged = false;
                Undo.RegisterFullObjectHierarchyUndo(tm.gameObject, "Swap RuleTiles");

                var bounds = tm.cellBounds;
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                    for (int x = bounds.xMin; x < bounds.xMax; x++)
                    {
                        var p = new Vector3Int(x, y, 0);
                        var tile = tm.GetTile(p);
                        if (!tile) continue;
                        scanned++;

                        if (tile is RuleTile rt)
                        {
                            if (mapping.TryGetValue(rt, out var cross) && cross && tile != cross)
                            {
                                tm.SetTile(p, cross);
                                tmChanged = true;
                                replaced++;
                            }
                        }
                    }

                if (tmChanged)
                {
                    tm.CompressBounds();
                    tm.RefreshAllTiles();
                    EditorUtility.SetDirty(tm);
                    affectedTilemaps++;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Swap terminé",
                $"Grid: {rootGrid.gameObject.name}\nTilemaps affectées: {affectedTilemaps}/{tilemaps.Count}\nCellules scannées: {scanned}\nRemplacements: {replaced}\nTiles uniques converties: {mapping.Count}",
                "OK");
        }
        catch
        {
            Undo.CollapseUndoOperations(undoGroup);
            throw;
        }
    }

    // ---------- Helpers ----------

    // Crée un CrossPartitionRuleTile asset à côté du RuleTile source (suffixe _Cross)
    private static CrossPartitionRuleTile CreateCrossAssetBeside(RuleTile src)
    {
        if (!src) return null;

        string srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath)) return null;

        string dir = Path.GetDirectoryName(srcPath);
        string name = Path.GetFileNameWithoutExtension(srcPath);
        string dstPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, name + "_Cross.asset"));

        var cross = ScriptableObject.CreateInstance<CrossPartitionRuleTile>();
        CloneRuleTileData(src, cross);

        AssetDatabase.CreateAsset(cross, dstPath);
        EditorUtility.SetDirty(cross);
        return cross;
    }

    // Construit un mapping pour **les RuleTile réellement utilisés** par ces Tilemaps.
    // Crée au besoin les assets Cross correspondants.
    private static Dictionary<RuleTile, CrossPartitionRuleTile>
        BuildOrCreateMappingForRuleTilesReferencedBy(List<Tilemap> tilemaps)
    {
        var used = new HashSet<RuleTile>(new UnityObjectRefComparer<RuleTile>());

        // 1) Collecter les RuleTiles réellement présents sous le Grid
        foreach (var tm in tilemaps)
        {
            if (!tm) continue;
            var b = tm.cellBounds;
            for (int y = b.yMin; y < b.yMax; y++)
                for (int x = b.xMin; x < b.xMax; x++)
                {
                    var t = tm.GetTile(new Vector3Int(x, y, 0));
                    if (t is RuleTile rt) used.Add(rt);
                }
        }

        // 2) Pour chacun, trouver ou créer l’asset Cross
        var map = new Dictionary<RuleTile, CrossPartitionRuleTile>(new UnityObjectRefComparer<RuleTile>());
        foreach (var rt in used)
        {
            var cross = FindSiblingCross(rt);
            if (!cross) cross = CreateCrossAssetBeside(rt);
            if (cross) map[rt] = cross;
        }
        return map;
    }

    // Essaie de trouver un Cross "à côté" (même dossier) nommé *_Cross.asset
    private static CrossPartitionRuleTile FindSiblingCross(RuleTile src)
    {
        var srcPath = AssetDatabase.GetAssetPath(src);
        if (string.IsNullOrEmpty(srcPath)) return null;
        var dir = Path.GetDirectoryName(srcPath);
        var name = Path.GetFileNameWithoutExtension(srcPath);
        var candidate = Path.Combine(dir, name + "_Cross.asset");
        var dst = AssetDatabase.LoadAssetAtPath<CrossPartitionRuleTile>(candidate);
        return dst;
    }

    // Copie robuste : évite les champs privés/volatils (compat versions)
    private static void CloneRuleTileData(RuleTile src, CrossPartitionRuleTile dst)
    {
        // Champs de base (stables)
        dst.m_DefaultSprite = src.m_DefaultSprite;
        dst.m_DefaultGameObject = src.m_DefaultGameObject;
        dst.m_DefaultColliderType = src.m_DefaultColliderType;
        //dst.m_MinAnimationSpeed = src.m_MinAnimationSpeed;
        //dst.m_MaxAnimationSpeed = src.m_MaxAnimationSpeed;
        //dst.m_AnimationStartTime = src.m_AnimationStartTime;
        //dst.m_RandomTransform = src.m_RandomTransform;

        // Règles
        dst.m_TilingRules = new List<RuleTile.TilingRule>(src.m_TilingRules.Count);
        foreach (var r in src.m_TilingRules)
        {
            var nr = new RuleTile.TilingRule();
            // Copie via JSON (copie tous les [SerializeField] valides pour ta version)
            var json = JsonUtility.ToJson(r);
            JsonUtility.FromJsonOverwrite(json, nr);

            // Profondeur sûre pour tableaux/listes
            if (r.m_Sprites != null) nr.m_Sprites = (Sprite[])r.m_Sprites.Clone();
            if (r.m_Neighbors != null) nr.m_Neighbors = new List<int>(r.m_Neighbors);
            if (r.m_NeighborPositions != null) nr.m_NeighborPositions = new List<Vector3Int>(r.m_NeighborPositions);

            dst.m_TilingRules.Add(nr);
        }

        // IMPORTANT : ne renseigne PAS dst.index ici (asset ne doit pas référencer un objet de scène).
        // L’autowiring se fera à l’exécution dans CrossPartitionRuleTile.RefreshTile(...)
        // (ou tu peux exposer un champ "indexAssetGUID" si tu as un index en asset, mais en général non).
    }

    // Comparer par référence Unity (évite les surprises de GetHashCode)
    private sealed class UnityObjectRefComparer<T> : IEqualityComparer<T> where T : Object
    {
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T obj) => obj ? obj.GetInstanceID() : 0;
    }
}
