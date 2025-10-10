#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RuleTileNeighbors_Import : EditorWindow
{
    [Serializable] public class Payload { public List<Item> rules; }
    [Serializable] public class Item { public Vector3Int[] neighborPositions; public int[] neighbors; } // 0=Any,1=This,2=NotThis

    // UI
    TextAsset jsonAsset;     // option 1: JSON déjà dans le projet
    string    jsonPath = ""; // option 2: JSON externe via file picker
    RuleTile  targetTile;    // RuleTile à modifier
    bool      resizeToMatch = true;

    // Réflexion tolérante versions
    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [MenuItem("Tools/Tilemap/RuleTile Neighbors/Import JSON → Selected RuleTile")]
    static void Open() => GetWindow<RuleTileNeighbors_Import>("Import RuleTile Neighbors");

    void OnGUI()
    {
        // Auto-sélection
        if (!targetTile && Selection.activeObject is RuleTile sel) targetTile = sel;

        GUILayout.Label("JSON source", EditorStyles.boldLabel);
        jsonAsset = (TextAsset)EditorGUILayout.ObjectField(new GUIContent("TextAsset (in Assets)"), jsonAsset, typeof(TextAsset), false);

        EditorGUILayout.BeginHorizontal();
        jsonPath = EditorGUILayout.TextField(new GUIContent("External file path"), jsonPath);
        if (GUILayout.Button("Browse...", GUILayout.Width(90)))
        {
            var p = EditorUtility.OpenFilePanel("Pick neighbors JSON", "", "json");
            if (!string.IsNullOrEmpty(p)) jsonPath = p;
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Target", EditorStyles.boldLabel);
        targetTile = (RuleTile)EditorGUILayout.ObjectField(new GUIContent("Target RuleTile"), targetTile, typeof(RuleTile), false);

        resizeToMatch = EditorGUILayout.Toggle(new GUIContent("Resize rule list to JSON length",
            "ON: reconstruit la liste pour coller exactement au JSON (sprites/output conservés pour les indices qui existent). OFF: ne remplace que les N premières règles."), resizeToMatch);

        GUILayout.Space(8);
        if (GUILayout.Button("Apply neighbors to target"))
        {
            try { Apply(); }
            catch (Exception e) { Debug.LogError(e); EditorUtility.DisplayDialog("Import Error", e.Message, "OK"); }
        }
    }

    void Apply()
    {
        if (!targetTile) throw new Exception("Select a target RuleTile.");

        // Charger le JSON (priorité au TextAsset, sinon chemin externe)
        string jsonText = null;
        if (jsonAsset) jsonText = jsonAsset.text;
        else if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath)) jsonText = File.ReadAllText(jsonPath);
        else throw new Exception("Provide a JSON (TextAsset or external file).");

        var payload = JsonUtility.FromJson<Payload>(jsonText);
        if (payload?.rules == null || payload.rules.Count == 0)
            throw new Exception("JSON payload is empty or invalid.");

        var tgtRules = GetRules(targetTile);
        int count = resizeToMatch ? payload.rules.Count : Math.Min(payload.rules.Count, tgtRules.Count);

        var newList = new List<RuleTile.TilingRule>(resizeToMatch ? payload.rules.Count : tgtRules.Count);

        // Met à jour/clone les N premières
        for (int i = 0; i < count; i++)
        {
            var src = payload.rules[i];
            RuleTile.TilingRule baseRule = (i < tgtRules.Count) ? CloneRuleKeepSpritesAndOutput(tgtRules[i])
                                                                : new RuleTile.TilingRule();

            // Remplace uniquement le voisinage
            SetNeighborPositions(baseRule, src.neighborPositions ?? Array.Empty<Vector3Int>());
            SetNeighborCodes(baseRule, src.neighbors ?? Array.Empty<int>());

            newList.Add(baseRule);
        }

        // Si on ne redimensionne pas: on garde le reste tel quel
        if (!resizeToMatch && tgtRules.Count > count)
        {
            for (int i = count; i < tgtRules.Count; i++)
                newList.Add(CloneRuleKeepSpritesAndOutput(tgtRules[i]));
        }

        // Si on redimensionne et que le JSON est plus long que l’existant, on a déjà créé des règles vides plus haut

        // Écrire et sauver
        SetRules(targetTile, newList);
        EditorUtility.SetDirty(targetTile);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Done",
            $"Applied neighbors to '{targetTile.name}'.\nRules count: {newList.Count}\n(Reused sprites & outputs from existing rules where available.)",
            "OK");
        Selection.activeObject = targetTile;
    }

    // ===== helpers (réflexion tolérante) =====
    static List<RuleTile.TilingRule> GetRules(RuleTile t)
    {
        var f = typeof(RuleTile).GetField("m_TilingRules", BF);
        return (List<RuleTile.TilingRule>)(f?.GetValue(t) ?? new List<RuleTile.TilingRule>());
    }

    static void SetRules(RuleTile t, List<RuleTile.TilingRule> list)
    {
        var f = typeof(RuleTile).GetField("m_TilingRules", BF);
        if (f != null) f.SetValue(t, list);
    }

    static RuleTile.TilingRule CloneRuleKeepSpritesAndOutput(RuleTile.TilingRule r)
    {
        var nr = new RuleTile.TilingRule();
        // Sprites
        CopyFieldIfExists(r, nr, "m_Sprites");
        // Output + FPS
        CopyFieldIfExists(r, nr, "m_Output");
        CopyFieldIfExists(r, nr, "m_MinAnimationSpeed");
        CopyFieldIfExists(r, nr, "m_MaxAnimationSpeed");
        // Collider/Transforms (facultatif mais utile)
        CopyFieldIfExists(r, nr, "m_ColliderType");
        CopyFieldIfExists(r, nr, "m_RuleTransform");
        CopyFieldIfExists(r, nr, "m_RandomTransform");
        CopyFieldIfExists(r, nr, "m_PerlinScale");
        CopyFieldIfExists(r, nr, "m_Noise");
        CopyFieldIfExists(r, nr, "m_ID");
        // Ne PAS copier les voisins; ils seront remplacés
        return nr;
    }

    static void CopyFieldIfExists(object src, object dst, string field)
    {
        var sf = src.GetType().GetField(field, BF);
        var df = dst.GetType().GetField(field, BF);
        if (sf != null && df != null) df.SetValue(dst, sf.GetValue(src));
    }

    static void SetNeighborPositions(object rule, Vector3Int[] pos)
    {
        var f = rule.GetType().GetField("m_NeighborPositions", BF);
        if (f == null) return;
        if (f.FieldType == typeof(List<Vector3Int>)) f.SetValue(rule, new List<Vector3Int>(pos ?? Array.Empty<Vector3Int>()));
        else if (f.FieldType == typeof(Vector3Int[])) f.SetValue(rule, pos ?? Array.Empty<Vector3Int>());
    }

    static void SetNeighborCodes(object rule, int[] codes)
    {
        var f = rule.GetType().GetField("m_Neighbors", BF);
        if (f == null) return;
        if (f.FieldType == typeof(List<int>)) f.SetValue(rule, new List<int>(codes ?? Array.Empty<int>()));
        else if (f.FieldType == typeof(int[])) f.SetValue(rule, codes ?? Array.Empty<int>());
    }
}
#endif
