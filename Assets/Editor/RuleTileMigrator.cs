// Assets/Editor/RuleTileMigrator.cs
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public static class RuleTileMigrator
{
    [MenuItem("Tools/Tiles/Migrate RuleTile -> CrossPartitionRuleTile")]
    public static void Migrate()
    {
        var src = Selection.activeObject as RuleTile;
        if (!src) { Debug.LogError("Select a RuleTile asset first."); return; }

        var dst = ScriptableObject.CreateInstance<CrossPartitionRuleTile>();
        AssetDatabase.CreateAsset(dst, AssetDatabase.GenerateUniqueAssetPath("Assets/" + src.name + "_Cross.asset"));

        // Copie basique des champs utiles
        dst.m_DefaultSprite = src.m_DefaultSprite;
        dst.m_DefaultGameObject = src.m_DefaultGameObject;
        dst.m_DefaultColliderType = src.m_DefaultColliderType;
        //dst.m_MinAnimationSpeed = src.m_MinAnimationSpeed;
        //dst.m_MaxAnimationSpeed = src.m_MaxAnimationSpeed;
        //dst.m_AnimationStartTime = src.m_AnimationStartTime;
        //dst.m_RandomTransform = src.m_RandomTransform;

        // Copie des règles
        dst.m_TilingRules = new System.Collections.Generic.List<RuleTile.TilingRule>();
        foreach (var r in src.m_TilingRules)
        {
            var nr = new RuleTile.TilingRule();
            nr.m_Sprites = (Sprite[])r.m_Sprites.Clone();
            nr.m_GameObject = r.m_GameObject;
            nr.m_Output = r.m_Output;
            nr.m_Neighbors = new System.Collections.Generic.List<int>(r.m_Neighbors);
            nr.m_NeighborPositions = new System.Collections.Generic.List<Vector3Int>(r.m_NeighborPositions);
            nr.m_RuleTransform = r.m_RuleTransform;
            nr.m_ColliderType = r.m_ColliderType;
            //nr.m_RandomTransform = r.m_RandomTransform;
            //nr.m_PerlinScale = r.m_PerlinScale;
            //nr.m_PerlinOffset = r.m_PerlinOffset;
            //nr.m_Noise = r.m_Noise;
            dst.m_TilingRules.Add(nr);
        }

        EditorUtility.SetDirty(dst);
        AssetDatabase.SaveAssets();
        Debug.Log("Created " + AssetDatabase.GetAssetPath(dst));
    }
}
