using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "TerrainTilerConfig", menuName = "Terrain/Plateau Tiler Config")]
public class TerrainTilerConfig : ScriptableObject
{
    [Header("Height thresholds [0..1]")]
    [Range(0, 1f)] public float seaLevel = 0.35f;
    [Range(0, 1f)] public float riverLevel = 0.2f;
    [Range(0, 0.2f)] public float beachBand = 0.02f;
    [Range(0, 1f)] public float snowLevel = 0.80f;

    [Header("Terracing")]
    [Range(0.005f, 0.2f)] public float terraceStep = 0.05f; // épaisseur d’un plateau (en hauteur normalisée)
    [Range(0f, 0.2f)] public float bridgeRampAllowance = 0.02f; // combien au-dessus du plan d’eau on accepte pour poser un pont/rampe
    public int maxBridgeLength = 128; // anti-débile

    [Header("River/Lake (optionnels)")]
    public bool detectLakes = true;
    public int lakeMinArea = 200;

    [Header("Tiles (plein)")]
    public TileBase oceanTile;
    public TileBase sandTile;
    public TileBase grassTile;
    public TileBase rockTile;  // si tu veux distinguer pente, tu peux remplacer par RuleTile
    public TileBase snowTile;
    public TileBase riverTile; // si tu t’en sers
    public TileBase lakeTile;  // idem

    public int debugLayerIndex;
    public int bridgeLayerIndex;
    public int oceanLayerIndex;
    public int sandLayerIndex;

    [Header("Plateau fill & edges")]
    public TileBase plateauTile; // fallback si tu préfères remplir par plateau explicite
    public bool paintByBiome = true; // true = océan/sable/herbe/roche/neige ; false = plateauTile

    [Header("Bridges")]
    public TileBase bridgeTileStraightNS; // vertical
    public TileBase bridgeTileStraightEW; // horizontal
    public TileBase bridgeTilePillar;     // optionnel (sous-pont), laisse null sinon

    [Header("Debug")]
    public bool logStats = true;
}
