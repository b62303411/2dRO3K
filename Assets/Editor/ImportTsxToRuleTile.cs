#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

/// Menu: Tools/Tilemap/Import TSX to RuleTile
/// - TSX: fichier Tiled (.tsx) avec <terraintypes> et des <tile id terrain="a,b,c,d">
/// - Spritesheet: Texture2D déjà "slicée" (Sprite Mode: Multiple)
/// - tileWidth/tileHeight: dimensions px d’une tuile (doivent matcher le slicing)
/// Sortie: un RuleTile .asset mappant chaque sprite en règle avec voisins This/NotThis/Any
public class ImportTsxToRuleTile : EditorWindow
{
    public TextAsset tsxFile;
    public Texture2D spritesheet;
    public int tileWidth = 32;
    public int tileHeight = 32;

    public string outputFolder = "Assets/Tiles/Generated";

    [MenuItem("Tools/Tilemap/Import TSX to RuleTile")]
    public static void Open() => GetWindow<ImportTsxToRuleTile>("TSX → RuleTile");

    void OnGUI()
    {
        tsxFile = (TextAsset)EditorGUILayout.ObjectField("TSX (Tiled tileset)", tsxFile, typeof(TextAsset), false);
        spritesheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet (sliced)", spritesheet, typeof(Texture2D), false);
        tileWidth = EditorGUILayout.IntField("Tile Width (px)", tileWidth);
        tileHeight = EditorGUILayout.IntField("Tile Height (px)", tileHeight);
        outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

        if (GUILayout.Button("Generate RuleTile"))
        {
            try { Build(); }
            catch (System.Exception e)
            {
                Debug.LogError(e);
                EditorUtility.DisplayDialog("Import Error", e.Message, "OK");
            }
        }
    }

    void Build()
    {
        if (!tsxFile) throw new System.Exception("Assigne un fichier .tsx.");
        if (!spritesheet) throw new System.Exception("Assigne le spritesheet (slicé en Multiple).");

        // 1) Charger tous les Sprites de la texture
        string texPath = AssetDatabase.GetAssetPath(spritesheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(texPath).OfType<Sprite>().ToList();
        if (sprites.Count == 0) throw new System.Exception("Aucun Sprite détecté. Assure-toi que la texture est bien slicée (Sprite Editor).");

        // Tri (haut→bas, gauche→droite) pour que id 0 = premier sprite en haut-gauche, etc.
        sprites.Sort((a, b) =>
        {
            int yd = b.rect.y.CompareTo(a.rect.y);
            return yd != 0 ? yd : a.rect.x.CompareTo(b.rect.x);
        });

        // 2) Parser le TSX (LINQ to XML)
        var doc = XDocument.Parse(tsxFile.text);
        var tileset = doc.Element("tileset");
        if (tileset == null) throw new System.Exception("Format TSX invalide (pas de <tileset>).");

        // Optionnel: lecture des <terraintypes><terrain name=.. tile=.. /></terraintypes>
        var terrainTypes = new List<string>();
        var terraintypesEl = tileset.Element("terraintypes");
        if (terraintypesEl != null)
        {
            foreach (var t in terraintypesEl.Elements("terrain"))
                terrainTypes.Add((string)t.Attribute("name") ?? "");
        }

        // Map tile id -> quartet terrain (a,b,c,d) (coins TL,TR,BR,BL). -1 si vide/absent
        var tileTerrains = new Dictionary<int, int[]>();
        foreach (var tileEl in tileset.Elements("tile"))
        {
            var idAttr = tileEl.Attribute("id");
            if (idAttr == null) continue;
            int id = int.Parse(idAttr.Value);

            var terrainAttr = tileEl.Attribute("terrain");
            if (terrainAttr == null) continue;

            var parts = terrainAttr.Value.Split(',');
            if (parts.Length != 4) continue;

            int[] quad = new int[4]; // TL, TR, BR, BL
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(parts[i])) quad[i] = -1;
                else if (int.TryParse(parts[i], out int v)) quad[i] = v;
                else quad[i] = -1;
            }
            tileTerrains[id] = quad;
        }

        if (tileTerrains.Count == 0)
            Debug.LogWarning("Aucun attribut terrain=\"a,b,c,d\" trouvé. On générera des règles 'Any' par sprite.");

        // 3) Créer le RuleTile
        EnsureFolder(outputFolder);
        var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
        ruleTile.m_DefaultColliderType = Tile.ColliderType.Sprite;
        if (sprites.Count > 0) ruleTile.m_DefaultSprite = sprites[0];

        // Pour chaque sprite (id = index)
        for (int id = 0; id < sprites.Count; id++)
        {
            var spr = sprites[id];

            // Construire une règle
            var rule = new RuleTile.TilingRule
            {
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_ColliderType = Tile.ColliderType.Sprite,
                m_Sprites = new Sprite[] { spr },
            };

            // Si TSX fournit un quartet, on déduit conditions Up/Right/Down/Left
            // Convention: TL,TR,BR,BL (coins). Bord "same" si les 2 coins du bord == même terrain >=0.
            // On prend comme "terrain de référence" la majorité des coins (ou le premier non -1).
            if (tileTerrains.TryGetValue(id, out var quad))
            {
                int refTerrain = MajorityTerrain(quad);
                // Si aucun terrain fiable, on laisse Any
                if (refTerrain >= 0)
                {
                    var conds = new Dictionary<Vector3Int, int>(); // dir -> neighbor code
                    // Haut: TL & TR
                    conds[new Vector3Int(0, 1, 0)] = EdgeMatch(quad[0], quad[1], refTerrain);
                    // Droite: TR & BR
                    conds[new Vector3Int(1, 0, 0)] = EdgeMatch(quad[1], quad[2], refTerrain);
                    // Bas: BR & BL
                    conds[new Vector3Int(0, -1, 0)] = EdgeMatch(quad[2], quad[3], refTerrain);
                    // Gauche: BL & TL
                    conds[new Vector3Int(-1, 0, 0)] = EdgeMatch(quad[3], quad[0], refTerrain);

                    // Sur versions récentes, on remplit m_NeighborPositions / m_Neighbors (List<>)
                    rule.m_NeighborPositions = new List<Vector3Int>();
                    rule.m_Neighbors = new List<int>();
                    foreach (var kv in conds)
                    {
                        // 0 = Any, 1 = This, 2 = NotThis (valeurs usuelles dans RuleTile)
                        rule.m_NeighborPositions.Add(kv.Key);
                        rule.m_Neighbors.Add(kv.Value);
                    }
                }
            }

            ruleTile.m_TilingRules.Add(rule);
        }

        // 4) Sauvegarde
        string assetPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(tsxFile.name) + "_RuleTile.asset").Replace("\\", "/");
        AssetDatabase.CreateAsset(ruleTile, assetPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = ruleTile;
        EditorUtility.DisplayDialog("Done", $"RuleTile créé:\n{assetPath}", "OK");
    }

    static int MajorityTerrain(int[] quad)
    {
        // retourne le terrain le plus fréquent (>=0), sinon premier non -1, sinon -1
        var counts = new Dictionary<int, int>();
        foreach (var q in quad)
        {
            if (q < 0) continue;
            counts[q] = counts.TryGetValue(q, out var c) ? c + 1 : 1;
        }
        if (counts.Count == 0) return -1;
        return counts.OrderByDescending(kv => kv.Value).First().Key;
    }

    // Renvoie le code RuleTile: 1 = This, 2 = NotThis, 0 = Any
    // Si les deux coins sont == refTerrain ⇒ This
    // Si les deux coins sont >=0 et != refTerrain ⇒ NotThis
    // Sinon ⇒ Any (transition ambiguë)
    static int EdgeMatch(int a, int b, int refTerrain)
    {
        bool aIs = (a == refTerrain);
        bool bIs = (b == refTerrain);
        if (aIs && bIs) return 1; // This
        if (a >= 0 && b >= 0 && a == b && a != refTerrain) return 2; // NotThis
        return 0; // Any (mixte / inconnu)
    }

    static void EnsureFolder(string folder)
    {
        string norm = folder.Replace("\\", "/").Trim('/');
        if (AssetDatabase.IsValidFolder(norm)) return;

        var parts = norm.Split('/');
        string cur = parts[0];
        if (!AssetDatabase.IsValidFolder(cur))
            throw new System.Exception($"Dossier racine invalide: {cur}");

        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
