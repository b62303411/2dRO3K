using UnityEngine;
using System.Collections.Generic;
using TMPro;
public class MapRenderer : MonoBehaviour
{
    [Header("Visuals")]
    public Sprite citySprite;          // assign any small circle sprite
    public float citySpriteScale = 0.25f;
    public float roadWidth = 0.05f;

    [Header("Faction Badge")]
    public Sprite badgeSprite;                   // mets un petit carré blanc (sprite)
    public Vector2 badgeOffset = new Vector2(0.6f, 0f);
    public Vector2 badgeScale = new Vector2(0.25f, 0.25f);
    public int badgeSortingOrder = 1;

    [Header("City Labels")]
    public TMP_FontAsset labelFont;     // assigne une fonte TMP
    public float labelYOffset = 0.6f;   // décalage au-dessus du point de ville
    public int labelFontSize = 3;       // taille en unités TMP (monde)
    public Color labelColor = Color.white;
    public bool useLOD = true;          // cacher quand on est trop zoom-out
    public float maxVisibleOrtho = 12f; // cacher au-delà de cette taille ortho
    private Dictionary<string, CityView> cityViews = new Dictionary<string, CityView>();

    public void Build(MapData map, GameManager gm, GameConfig cfg)
    {
        // Clear previous
        for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
        cityViews.Clear();

        // Cities
        foreach (var c in map.cities)
        {
            var go = new GameObject($"City_{c.id}");

            var labelGO = new GameObject($"Label_{c.id}");
            labelGO.transform.SetParent(go.transform, false);
            labelGO.transform.localPosition = new Vector3(0f, labelYOffset, 0f);

            var tmp = labelGO.AddComponent<TextMeshPro>();
            tmp.text = c.displayName;
            tmp.font = labelFont;
            tmp.fontSize = labelFontSize;
            tmp.color = labelColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            // --- BADGE DE FACTION ---
            var badgeGO = new GameObject("FactionBadge");
            badgeGO.transform.SetParent(go.transform, false);
            badgeGO.transform.localPosition = new Vector3(badgeOffset.x, badgeOffset.y, 0f);
            badgeGO.transform.localScale = new Vector3(badgeScale.x, badgeScale.y, 1f);

            var badgeSR = badgeGO.AddComponent<SpriteRenderer>();
            badgeSR.sprite = badgeSprite != null ? badgeSprite : citySprite; // fallback si pas de sprite
            badgeSR.color = cfg.GetFaction(c.ownerFactionId)?.color ?? Color.gray;
            badgeSR.sortingOrder = badgeSortingOrder;
      
        
            go.transform.SetParent(transform, false);
            go.transform.position = c.position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = citySprite;
            sr.color = cfg.GetFaction(c.ownerFactionId)?.color ?? Color.gray;
            go.transform.localScale = Vector3.one * citySpriteScale;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            // --- VIEW / CLIC ---
            var cv = go.AddComponent<CityView>();
            cv.Initialize(c, gm, sr, badgeSR);
            cityViews[c.id] = cv;
        }

        // Roads
        foreach (var r in map.roads)
        {
            var from = map.GetCity(r.fromCityId);
            var to = map.GetCity(r.toCityId);
            if (from == null || to == null) { Debug.LogWarning($"Bad road: {r.fromCityId} -> {r.toCityId}"); continue; }

            var road = new GameObject($"Road_{r.fromCityId}_{r.toCityId}");
            road.transform.SetParent(transform, false);

            var lr = road.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from.position);
            lr.SetPosition(1, to.position);
            lr.widthMultiplier = roadWidth;
            lr.useWorldSpace = true;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.sortingOrder = -1;
        }
    }

    public CityView GetCityView(string id) => cityViews.TryGetValue(id, out var v) ? v : null;
}
