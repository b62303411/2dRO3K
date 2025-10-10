using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class RuleTile_ConvertAndSwap_GridScope
{
    // === A) Convertit les RuleTile assets s�lectionn�s en CrossPartitionRuleTile assets (optionnel) ===
    [MenuItem("Tools/Tiles/Convert Selected RuleTiles -> CrossPartitionRuleTile Assets")]
    public static void ConvertSelectedAssets()
    {
        var objs = Selection.objects;
        if (objs == null || objs.Length == 0)
        {
            EditorUtility.DisplayDialog("Rien de s�lectionn�", "S�lectionne un ou plusieurs RuleTile assets.", "OK");
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
        EditorUtility.DisplayDialog("Conversion termin�e", $"Cr��s: {created}\nIgnor�s: {skipped}", "OK");
    }

    // === B) Swap sous le Grid s�lectionn� **uniquement** ===
    [MenuItem("Tools/Tiles/Swap RuleTile -> CrossPartitionRuleTile under Selected Grid")]
    public static void SwapUnderSelectedGrid()
    {
        // 1) R�cup�rer le Grid s�lectionn�
        var go = Selection.activeGameObject;
        if (!go)
        {
            EditorUtility.DisplayDialog("Aucun Grid s�lectionn�",
                "S�lectionne un GameObject qui contient le Grid parent.", "OK");
            return;
        }
        var rootGrid = go.GetComponent<Grid>();
        if (!rootGrid)
        {
            EditorUtility.DisplayDialog("Mauvaise s�lection",
                "Le GameObject s�lectionn� n'a pas de composant Grid.", "OK");
            return;
        }

        // 2) Lister toutes les Tilemaps **sous** ce Grid (incluant sous-Grids)
        var tilemaps = new List<Tilemap>();
        rootGrid.GetComponentsInChildren(true, tilemaps);
        if (tilemaps.Count == 0)
        {
            EditorUtility.DisplayDialog("Pas de Tilemap",
                "Aucune Tilemap trouv�e sous ce Grid.", "OK");
            return;
        }

        // 3) Construire/compl�ter le mapping RuleTile -> Cross (cr�ation si n�cessaire)
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

            EditorUtility.DisplayDialog("Swap termin�",
                $"Grid: {rootGrid.gameObject.name}\nTilemaps affect�es: {affectedTilemaps}/{tilemaps.Count}\nCellules scann�es: {scanned}\nRemplacements: {replaced}\nTiles uniques converties: {mapping.Count}",
                "OK");
        }
        catch
        {
            Undo.CollapseUndoOperations(undoGroup);
            throw;
        }
    }

    // ---------- Helpers ----------

    // Cr�e un CrossPartitionRuleTile asset � c�t� du RuleTile source (suffixe _Cross)
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

    // Construit un mapping pour **les RuleTile r�ellement utilis�s** par ces Tilemaps.
    // Cr�e au besoin les assets Cross correspondants.
    private static Dictionary<RuleTile, CrossPartitionRuleTile>
        BuildOrCreateMappingForRuleTilesReferencedBy(List<Tilemap> tilemaps)
    {
        var used = new HashSet<RuleTile>(new UnityObjectRefComparer<RuleTile>());

        // 1) Collecter les RuleTiles r�ellement pr�sents sous le Grid
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

        // 2) Pour chacun, trouver ou cr�er l�asset Cross
        var map = new Dictionary<RuleTile, CrossPartitionRuleTile>(new UnityObjectRefComparer<RuleTile>());
        foreach (var rt in used)
        {
            var cross = FindSiblingCross(rt);
            if (!cross) cross = CreateCrossAssetBeside(rt);
            if (cross) map[rt] = cross;
        }
        return map;
    }

    // Essaie de trouver un Cross "� c�t�" (m�me dossier) nomm� *_Cross.asset
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

    // Copie robuste : �vite les champs priv�s/volatils (compat versions)
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

        // R�gles
        dst.m_TilingRules = new List<RuleTile.TilingRule>(src.m_TilingRules.Count);
        foreach (var r in src.m_TilingRules)
        {
            var nr = new RuleTile.TilingRule();
            // Copie via JSON (copie tous les [SerializeField] valides pour ta version)
            var json = JsonUtility.ToJson(r);
            JsonUtility.FromJsonOverwrite(json, nr);

            // Profondeur s�re pour tableaux/listes
            if (r.m_Sprites != null) nr.m_Sprites = (Sprite[])r.m_Sprites.Clone();
            if (r.m_Neighbors != null) nr.m_Neighbors = new List<int>(r.m_Neighbors);
            if (r.m_NeighborPositions != null) nr.m_NeighborPositions = new List<Vector3Int>(r.m_NeighborPositions);

            dst.m_TilingRules.Add(nr);
        }

        // IMPORTANT : ne renseigne PAS dst.index ici (asset ne doit pas r�f�rencer un objet de sc�ne).
        // L�autowiring se fera � l�ex�cution dans CrossPartitionRuleTile.RefreshTile(...)
        // (ou tu peux exposer un champ "indexAssetGUID" si tu as un index en asset, mais en g�n�ral non).
    }

    // Comparer par r�f�rence Unity (�vite les surprises de GetHashCode)
    private sealed class UnityObjectRefComparer<T> : IEqualityComparer<T> where T : Object
    {
        public bool Equals(T a, T b) => ReferenceEquals(a, b);
        public int GetHashCode(T obj) => obj ? obj.GetInstanceID() : 0;
    }
}
