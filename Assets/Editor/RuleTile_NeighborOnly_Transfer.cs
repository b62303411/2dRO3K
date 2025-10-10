#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

public class RuleTile_NeighborOnly_Transfer : EditorWindow
{
    // === UI ===
    RuleTile exportSource;
    TextAsset importJson;
    Texture2D targetSheet;

    int cellSize = 32;            // taille px d'une tuile (pour vérifier / animer)
    bool animated = false;        // convertit en Animation via sheet banqué
    int bankCols = 0, bankRows = 0;
    float fps = 6f;
    string outputFolder = "Assets/Tiles/Generated";

    [MenuItem("Tools/Tilemap/RuleTile (Neighbors Only)")]
    static void Open() => GetWindow<RuleTile_NeighborOnly_Transfer>("RuleTile Neighbors Only");

    void OnGUI()
    {
        // EXPORT
        GUILayout.Label("Export neighbors → JSON", EditorStyles.boldLabel);
        exportSource = (RuleTile)EditorGUILayout.ObjectField("RuleTile (source)", exportSource, typeof(RuleTile), false);
        if (GUILayout.Button("Export"))
        {
            try { ExportNeighbors(exportSource); }
            catch (Exception e) { Debug.LogError(e); EditorUtility.DisplayDialog("Export Error", e.Message, "OK"); }
        }

        EditorGUILayout.Space();

        // IMPORT
        GUILayout.Label("Import neighbors ← JSON", EditorStyles.boldLabel);
        importJson  = (TextAsset)EditorGUILayout.ObjectField("Rules JSON", importJson, typeof(TextAsset), false);
        targetSheet = (Texture2D)EditorGUILayout.ObjectField("Target Spritesheet (sliced)", targetSheet, typeof(Texture2D), false);
        cellSize    = EditorGUILayout.IntField("Cell Size (px)", cellSize);
        animated    = EditorGUILayout.Toggle("Animated (banked)?", animated);
        if (animated)
        {
            bankCols = EditorGUILayout.IntField("Bank Columns (per frame)", bankCols);
            bankRows = EditorGUILayout.IntField("Bank Rows (per frame)", bankRows);
            fps      = EditorGUILayout.FloatField("FPS", fps);
        }
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("Import / Build RuleTile"))
        {
            try { ImportNeighbors(importJson, targetSheet, cellSize, animated, bankCols, bankRows, fps, outputFolder); }
            catch (Exception e) { Debug.LogError(e); EditorUtility.DisplayDialog("Import Error", e.Message, "OK"); }
        }
    }

    // ===== EXPORT (neighbors + sprite indices seulement) =====
    static void ExportNeighbors(RuleTile tile)
    {
        if (!tile) throw new Exception("Select a RuleTile to export.");
        var rules = GetRules(tile);

        var firstSprite = FindFirstSprite(rules) ?? tile.m_DefaultSprite;
        if (!firstSprite) throw new Exception("No sprite found on the RuleTile (needed to index sprites).");

        // Source sprites triés en ordre grille
        var srcPath = AssetDatabase.GetAssetPath(firstSprite.texture);
        var srcSprites = AssetDatabase.LoadAllAssetsAtPath(srcPath).OfType<Sprite>().ToList();
        if (srcSprites.Count == 0) throw new Exception("Could not load sliced sprites from the source texture.");
        SortGrid(srcSprites);

        var payload = new Payload { rules = new List<Item>() };

        foreach (var r in rules)
        {
            var sprites = GetSprites(r) ?? Array.Empty<Sprite>();

            // Neighbors: positions + codes
            var pos = GetNeighborPositions(r);
            var cod = GetNeighbors(r);

            // Sprite indices (on ne garde que le 1er index pour animer ensuite proprement)
            int spriteIndex = -1;
            if (sprites.Length > 0 && sprites[0] != null)
                spriteIndex = IndexOfSprite(srcSprites, sprites[0]);

            payload.rules.Add(new Item
            {
                spriteIndex = spriteIndex,           // index relatif à la frame 0
                neighborPositions = pos,
                neighbors = cod
            });
        }

        var json = JsonUtility.ToJson(payload, true);
        var save = EditorUtility.SaveFilePanel("Save JSON (neighbors only)", "Assets", tile.name + "_neighbors.json", "json");
        if (string.IsNullOrEmpty(save)) return;
        File.WriteAllText(save, json);
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", "Exported:\n" + save, "OK");
    }

    // ===== IMPORT (reconstruit un RuleTile avec ces voisins; option animation) =====
    static void ImportNeighbors(TextAsset json, Texture2D target, int cellSize, bool animated, int bankCols, int bankRows, float fps, string outFolder)
    {
        if (!json)   throw new Exception("Assign a JSON (neighbors only).");
        if (!target) throw new Exception("Assign a target spritesheet (sliced).");
        if (cellSize <= 0) throw new Exception("cellSize must be > 0.");

        var payload = JsonUtility.FromJson<Payload>(json.text);
        if (payload?.rules == null) throw new Exception("Bad JSON payload.");

        // Tous les sprites cibles
        var newPath = AssetDatabase.GetAssetPath(target);
        var newAll = AssetDatabase.LoadAllAssetsAtPath(newPath).OfType<Sprite>().ToList();
        if (newAll.Count == 0) throw new Exception("Target spritesheet has no sliced sprites.");
        SortGrid(newAll);

        // Partition en frames si animé
        List<Sprite[]> frames = null;
        int framesCount = 1, tilesPerFrame = newAll.Count;

        if (animated)
        {
            if (bankCols <= 0 || bankRows <= 0) throw new Exception("Animated: bankCols/bankRows must be > 0.");
            int totalCols = Mathf.RoundToInt((float)target.width / cellSize);
            if (totalCols % bankCols != 0) throw new Exception($"totalCols={totalCols} not divisible by bankCols={bankCols}.");

            framesCount = totalCols / bankCols;
            tilesPerFrame = bankCols * bankRows;

            frames = new List<Sprite[]>(framesCount);
            for (int f = 0; f < framesCount; f++)
            {
                var list = new List<Sprite>(tilesPerFrame);
                for (int row = 0; row < bankRows; row++)
                {
                    int globalRowStart = row * totalCols;
                    int start = globalRowStart + f * bankCols;
                    for (int col = 0; col < bankCols; col++)
                    {
                        int idx = start + col;
                        if (idx < 0 || idx >= newAll.Count) throw new Exception("Frame slicing out of bounds.");
                        list.Add(newAll[idx]);
                    }
                }
                frames.Add(list.ToArray());
            }
        }

        EnsureFolder(outFolder);
        var outTile = ScriptableObject.CreateInstance<RuleTile>();
        outTile.m_DefaultColliderType = Tile.ColliderType.Sprite;
        outTile.m_DefaultSprite = newAll[0];

        var rulesList = new List<RuleTile.TilingRule>();
        SetRules(outTile, rulesList);

        foreach (var it in payload.rules)
        {
            var r = new RuleTile.TilingRule();

            // Voisinage
            SetNeighborPositions(r, it.neighborPositions ?? Array.Empty<Vector3Int>());
            SetNeighborCodes(r, it.neighbors ?? Array.Empty<int>());

            // Sprites
            if (!animated)
            {
                var s = (it.spriteIndex >= 0 && it.spriteIndex < newAll.Count) ? newAll[it.spriteIndex] : null;
                SetSprites(r, s != null ? new[] { s } : Array.Empty<Sprite>());
                SetOutput(r, RuleTile.TilingRuleOutput.OutputSprite.Single);
            }
            else
            {
                int tileIndex = it.spriteIndex < 0 ? 0 : it.spriteIndex % Math.Max(1, tilesPerFrame);
                var anim = new List<Sprite>(framesCount);
                for (int f = 0; f < framesCount; f++)
                {
                    var arr = frames[f];
                    anim.Add(tileIndex < arr.Length ? arr[tileIndex] : arr[0]);
                }
                SetSprites(r, anim.ToArray());
                SetOutput(r, RuleTile.TilingRuleOutput.OutputSprite.Animation);
                SetAnimFps(r, fps);
            }

            rulesList.Add(r);
        }

        SetRules(outTile, rulesList);

        var outPath = Path.Combine(outFolder, (json ? Path.GetFileNameWithoutExtension(json.name) : "RuleTile") + (animated ? "_Animated" : "") + ".asset").Replace("\\", "/");
        AssetDatabase.CreateAsset(outTile, outPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = outTile;
        EditorUtility.DisplayDialog("Done", $"Created:\n{outPath}", "OK");
    }

    // ===== Helpers (réflexion ultra-light) =====
    static BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    static List<RuleTile.TilingRule> GetRules(RuleTile t)
    {
        var f = typeof(RuleTile).GetField("m_TilingRules", BF);
        return (List<RuleTile.TilingRule>) (f?.GetValue(t) ?? new List<RuleTile.TilingRule>());
    }
    static void SetRules(RuleTile t, List<RuleTile.TilingRule> list)
    {
        var f = typeof(RuleTile).GetField("m_TilingRules", BF);
        if (f != null) f.SetValue(t, list);
    }

    static Sprite[] GetSprites(object rule)
    {
        var f = rule.GetType().GetField("m_Sprites", BF);
        return f != null ? (Sprite[])f.GetValue(rule) : null;
    }
    static void SetSprites(object rule, Sprite[] arr)
    {
        var f = rule.GetType().GetField("m_Sprites", BF);
        if (f != null) f.SetValue(rule, arr ?? Array.Empty<Sprite>());
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

    static void SetOutput(object rule, RuleTile.TilingRuleOutput.OutputSprite mode)
    {
        var f = rule.GetType().GetField("m_Output", BF);
        if (f != null) f.SetValue(rule, mode);
    }
    static void SetAnimFps(object rule, float fps)
    {
        var f1 = rule.GetType().GetField("m_MinAnimationSpeed", BF);
        var f2 = rule.GetType().GetField("m_MaxAnimationSpeed", BF);
        if (f1 != null) f1.SetValue(rule, fps);
        if (f2 != null) f2.SetValue(rule, fps);
    }

    static void SortGrid(List<Sprite> list)
    {
        list.Sort((a,b) => {
            int yd = b.rect.y.CompareTo(a.rect.y);
            return yd != 0 ? yd : a.rect.x.CompareTo(b.rect.x);
        });
    }
    static int IndexOfSprite(List<Sprite> ordered, Sprite s)
    {
        int i = ordered.IndexOf(s);
        if (i >= 0) return i;
        // fallback rect
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

    static void EnsureFolder(string folder)
    {
        folder = folder.Replace("\\","/").Trim('/');
        if (AssetDatabase.IsValidFolder(folder)) return;
        if (!folder.StartsWith("Assets")) throw new Exception("Folder must be inside Assets/.");

        string[] parts = folder.Split('/');
        string cur = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    // ====== DTO (neighbors only) ======
    [Serializable] public class Payload { public List<Item> rules; }
    [Serializable] public class Item
    {
        public int spriteIndex;             // index dans la frame 0
        public Vector3Int[] neighborPositions; // directions
        public int[] neighbors;             // 0=Any, 1=This, 2=NotThis
    }
}
#endif
