#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;
using System.IO;
using System.Linq;

public class CreateRuleTileFromSprites : EditorWindow
{
    public Texture2D spritesheet;

    [MenuItem("Tools/Tilemap/Create RuleTile From Spritesheet")]
    public static void Open() => GetWindow<CreateRuleTileFromSprites>("Create RuleTile");

    void OnGUI()
    {
        spritesheet = (Texture2D)EditorGUILayout.ObjectField("Spritesheet (sliced)", spritesheet, typeof(Texture2D), false);

        if (GUILayout.Button("Generate RuleTile")) Build();
    }

    void Build()
    {
        if (!spritesheet) { Debug.LogError("Assign a sliced spritesheet."); return; }

        string path = AssetDatabase.GetAssetPath(spritesheet);
        var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
        if (sprites.Count == 0) { Debug.LogError("No sliced Sprites found on this texture."); return; }

        // Create RuleTile
        var ruleTile = ScriptableObject.CreateInstance<RuleTile>();
        ruleTile.m_DefaultSprite = sprites[0];
        ruleTile.m_DefaultColliderType = Tile.ColliderType.Sprite;

        // Add one unconditional rule per sprite
        foreach (var spr in sprites)
        {
            var rule = new RuleTile.TilingRule
            {
                // neighbors omitted on purpose => rule matches always
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
                m_ColliderType = Tile.ColliderType.Sprite,
                m_Sprites = new Sprite[] { spr }
            };

            // If your package exposes m_NeighborPositions/m_Neighbors as lists, leaving them empty is "always".
            // If you're on an older package that REQUIRES a fixed 8-neighbor array, uncomment below:
            // rule.m_Neighbors = new int[8] { 0,0,0,0,0,0,0,0 }; // 0 == Any

            ruleTile.m_TilingRules.Add(rule);
        }

        // Save asset
        const string outPath = "Assets/Tiles/GeneratedRuleTile.asset";
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        AssetDatabase.CreateAsset(ruleTile, outPath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = ruleTile;
        EditorUtility.DisplayDialog("Done", "RuleTile created at " + outPath, "OK");
    }
}
#endif
