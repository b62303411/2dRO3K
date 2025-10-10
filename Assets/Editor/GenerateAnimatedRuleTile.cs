#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Tools → Tilemap → Generate Animated RuleTile (banked)
/// - Spritesheet: PNG déjà slicé (Sprite Mode: Multiple, grille = cellSize)
/// - cellSize: taille d'une tuile en px
/// - bankCols/bankRows: nb de colonnes/lignes d'UNE frame (un tileset)
/// - fps: vitesse d’anim
/// Sortie: un RuleTile avec 1 règle par tuile, Output=Animation, aucune condition de voisinage
public class GenerateAnimatedRuleTile : EditorWindow
{
    public Texture2D spritesheet;
    public int cellSize = 32;
    public int bankCols = 0;   // colonnes dans UNE frame
    public int bankRows = 0;   // lignes dans UNE frame
    public float fps = 6f;
    public string outputFolder = "Assets/Tiles/Generated";

    [MenuItem("Tools/Tilemap/Generate Animated RuleTile (banked)")]
    public static void Open() => GetWindow<GenerateAnimatedRuleTile>("Animated RuleTile");

    void OnGUI()
    {
        spritesheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet (sliced)", spritesheet, typeof(Texture2D), false);
        cellSize = EditorGUILayout.IntField("Cell Size (px)", cellSize);
        bankCols = EditorGUILayout.IntField("Bank Columns (per frame)", bankCols);
        bankRows = EditorGUILayout.IntField("Bank Rows (per frame)", bankRows);
        fps = EditorGUILayout.FloatField("FPS", fps);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("Generate"))
        {
            try { Build(); }
            catch (System.SystemException e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Error", e.Message, "OK");
            }
        }
    }

    void Build()
    {
        if (!spritesheet) throw new System.Exception("Assigne le spritesheet.");
        if (cellSize <= 0) throw new System.Exception("cellSize invalide.");
        if (bankCols <= 0 || bankRows <= 0) throw new System.Exception("bankCols/bankRows requis (dimensions d'UNE frame).");

        // Charger tous les sprites du sheet
        string path = AssetDatabase.GetAssetPath(spritesheet);
        var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (all.Count == 0) throw new System.Exception("Aucun Sprite détecté. Slice en Multiple + grille.");

        // Tri (haut→bas, gauche→droite)
        all.Sort((a, b) =>
        {
            int yd = b.rect.y.CompareTo(a.rect.y);
            return yd != 0 ? yd : a.rect.x.CompareTo(b.rect.x);
        });

        int totalCols = Mathf.RoundToInt((float)spritesheet.width / cellSize);
        int totalRows = Mathf.RoundToInt((float)spritesheet.height / cellSize);
        if (totalCols * totalRows != all.Count)
            Debug.LogWarning($"Attention: {totalCols}*{totalRows} != {all.Count}. Vérifie cellSize/slicing.");

        if (totalCols % bankCols != 0)
            throw new System.Exception($"totalCols ({totalCols}) n'est pas divisible par bankCols ({bankCols}). Mauvaise valeur.");

        int frames = totalCols / bankCols;
        int tilesPerFrame = bankCols * bankRows;

        // Découper en frames
        var framesSprites = new List<Sprite[]>(frames);
        for (int f = 0; f < frames; f++)
        {
            var frame = new List<Sprite>(tilesPerFrame);
            for (int row = 0; row < bankRows; row++)
            {
                int globalRowStart = row * totalCols;
                int start = globalRowStart + f * bankCols;
                for (int col = 0; col < bankCols; col++)
                {
                    int idx = start + col;
                    if (idx < 0 || idx >= all.Count) throw new System.Exception("Index frame hors borne (slicing).");
                    frame.Add(all[idx]);
                }
            }
            framesSprites.Add(frame.ToArray());
        }

        // Créer RuleTile vide avec 1 règle animée par tuile (aucune condition)
        EnsureFolder(outputFolder);
        var tile = ScriptableObject.CreateInstance<RuleTile>();
        tile.m_DefaultColliderType = Tile.ColliderType.Sprite;
        tile.m_DefaultSprite = framesSprites[0][0];

        for (int tileIndex = 0; tileIndex < tilesPerFrame; tileIndex++)
        {
            var anim = new List<Sprite>(frames);
            for (int f = 0; f < frames; f++)
            {
                var arr = framesSprites[f];
                if (tileIndex >= arr.Length) throw new System.Exception("tileIndex hors borne dans une frame.");
                anim.Add(arr[tileIndex]);
            }

            var rule = new RuleTile.TilingRule
            {
                // Pas de voisins = “always match”
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Animation,
                m_ColliderType = Tile.ColliderType.Sprite,
                m_Sprites = anim.ToArray(),
                m_MinAnimationSpeed = fps,
                m_MaxAnimationSpeed = fps,
                // Pour les versions récentes: listes vides = aucune condition
                m_NeighborPositions = new List<Vector3Int>(),
                m_Neighbors = new List<int>()
            };

            tile.m_TilingRules.Add(rule);
        }

        // Sauvegarder
        string outPath = Path.Combine(outputFolder, spritesheet.name + "_AnimatedRuleTile.asset").Replace("\\", "/");
        AssetDatabase.CreateAsset(tile, outPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = tile;

        EditorUtility.DisplayDialog("Done", $"RuleTile animé créé:\n{outPath}\nTiles/frame: {tilesPerFrame} | Frames: {frames} | FPS: {fps}", "OK");
    }

    static void EnsureFolder(string folder)
    {
        string norm = folder.Replace("\\", "/").Trim('/');
        if (AssetDatabase.IsValidFolder(norm)) return;
        var parts = norm.Split('/');
        string cur = parts[0];
        if (!AssetDatabase.IsValidFolder(cur)) throw new System.Exception($"Dossier racine invalide: {cur}");
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
