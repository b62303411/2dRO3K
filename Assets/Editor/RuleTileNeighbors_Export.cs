#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RuleTileNeighbors_Export : EditorWindow
{
    RuleTile sourceTile;

    [MenuItem("Tools/Tilemap/RuleTile/Export Neighbors to JSON")]
    static void Open() => GetWindow<RuleTileNeighbors_Export>("Export RuleTile Neighbors");

    void OnGUI()
    {
        sourceTile = (RuleTile)EditorGUILayout.ObjectField("RuleTile (source)", sourceTile, typeof(RuleTile), false);

        if (GUILayout.Button("Export"))
        {
            try { Export(); }
            catch (Exception e) { Debug.LogError(e); EditorUtility.DisplayDialog("Export Error", e.Message, "OK"); }
        }
    }

    void Export()
    {
        if (!sourceTile) throw new Exception("Select a RuleTile.");

        var rules = GetRules(sourceTile);
        if (rules.Count == 0) throw new Exception("The RuleTile has no rules.");

        // Find a sprite to index by (just to compute indices for the first sprite per rule)
        var firstSprite = FindFirstSprite(rules) ?? sourceTile.m_DefaultSprite;
        if (!firstSprite) throw new Exception("No sprite found on the RuleTile to infer indices.");

        // Load sliced sprites from that texture and order top→bottom, left→right
        var srcPath = AssetDatabase.GetAssetPath(firstSprite.texture);
        var srcSprites = AssetDatabase.LoadAllAssetsAtPath(srcPath).OfType<Sprite>().ToList();
        if (srcSprites.Count == 0) throw new Exception("No sliced sprites found on source texture.");
        SortGrid(srcSprites);

        var payload = new Payload { rules = new List<Item>() };

        foreach (var r in rules)
        {
            // neighbor data
            var pos = GetNeighborPositions(r);
            var cod = GetNeighbors(r);

            // sprite index (only the first sprite of the rule, used later as the base tile index)
            int spriteIndex = -1;
            var sprites = GetSprites(r);
            if (sprites != null && sprites.Length > 0 && sprites[0] != null)
                spriteIndex = IndexOfSprite(srcSprites, sprites[0]);

            payload.rules.Add(new Item
            {
                spriteIndex = spriteIndex,
                neighborPositions = pos,
                neighbors = cod
            });
        }

        var json = JsonUtility.ToJson(payload, true);
        var save = EditorUtility.SaveFilePanel("Save neighbors JSON", "Assets", sourceTile.name + "_neighbors.json", "json");
        if (string.IsNullOrEmpty(save)) return;

        File.WriteAllText(save, json);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Exported:\n" + save, "OK");
    }

    // --- reflection-lite helpers (version tolerant) ---
    static BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static List<RuleTile.TilingRule> GetRules(RuleTile t)
    {
        var f = typeof(RuleTile).GetField("m_TilingRules", BF);
        return (List<RuleTile.TilingRule>)(f?.GetValue(t) ?? new List<RuleTile.TilingRule>());
    }

    static Sprite[] GetSprites(object rule)
    {
        var f = rule.GetType().GetField("m_Sprites", BF);
        return f != null ? (Sprite[])f.GetValue(rule) : null;
    }

    static Vector3Int[] GetNeighborPositions(object rule)
    {
        var f = rule.GetType().GetField("m_NeighborPositions", BF);
        if (f == null) return Array.Empty<Vector3Int>();
        var v = f.GetValue(rule);
        if (v is List<Vector3Int> l) return l.ToArray();
        if (v is Vector3Int[] a)     return a;
        return Array.Empty<Vector3Int>();
    }

    static int[] GetNeighbors(object rule)
    {
        var f = rule.GetType().GetField("m_Neighbors", BF);
        if (f == null) return Array.Empty<int>();
        var v = f.GetValue(rule);
        if (v is List<int> l) return l.ToArray();
        if (v is int[] a)     return a;
        return Array.Empty<int>();
    }

    static void SortGrid(List<Sprite> list)
    {
        list.Sort((a, b) =>
        {
            int yd = b.rect.y.CompareTo(a.rect.y); // top→bottom
            return yd != 0 ? yd : a.rect.x.CompareTo(b.rect.x); // left→right
        });
    }

    static int IndexOfSprite(List<Sprite> ordered, Sprite s)
    {
        int i = ordered.IndexOf(s);
        if (i >= 0) return i;
        // fallback: rect match
        var r = s.rect;
        for (int k = 0; k < ordered.Count; k++)
        {
            var rr = ordered[k].rect;
            if (Mathf.Approximately(rr.x, r.x) &&
                Mathf.Approximately(rr.y, r.y) &&
                Mathf.Approximately(rr.width, r.width) &&
                Mathf.Approximately(rr.height, r.height))
                return k;
        }
        return -1;
    }

    static Sprite FindFirstSprite(List<RuleTile.TilingRule> rules)
    {
        foreach (var r in rules)
        {
            var s = GetSprites(r);
            if (s != null && s.Length > 0 && s[0] != null) return s[0];
        }
        return null;
    }

    // --- DTO ---
    [Serializable] public class Payload { public List<Item> rules; }
    [Serializable] public class Item
    {
        public int spriteIndex;              // index in frame 0
        public Vector3Int[] neighborPositions; // directions
        public int[] neighbors;              // 0=Any, 1=This, 2=NotThis
    }
}
#endif
