using UnityEngine;

public class CityView : MonoBehaviour
{
    public MapData.City City { get; private set; }

    private GameManager gm;
    private SpriteRenderer dotSR;
    private SpriteRenderer badgeSR;

    // Local UI state
    private bool isHovered;
    private bool isSelected;


    public void Initialize(MapData.City city, GameManager manager, SpriteRenderer dot, SpriteRenderer badge)
    {
        City = city;
        gm = manager;
        dotSR = dot;
        badgeSR = badge;
        RefreshVisual();
    }


    public void RefreshVisual()
    {
        var owner = gm?.GetFaction(City.ownerFactionId);
        if (badgeSR) badgeSR.color = owner != null ? owner.color : Color.gray;
        // Si tu veux aussi recolorer le point selon la faction, tu peux le faire ici:
        // if (dotSR) dotSR.color = owner != null ? owner.color : Color.gray;
    }

    // Appelle ça quand la ville change de propriétaire
    public void SetOwner(string factionId)
    {
        City.ownerFactionId = factionId;
        RefreshVisual();
    }

    void OnMouseUpAsButton()
    {
        gm?.SelectCity(this);
    }
}
