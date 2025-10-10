using UnityEngine;
using UnityEngine.UI;

public class CityPanelUI : MonoBehaviour
{
    public Text cityName;
    public Image ownerColorSwatch;
    public Text ownerName;

    public void Show(MapData.City city, GameConfig.FactionDef owner)
    {
        if (cityName) cityName.text = city.displayName;
        if (ownerName) ownerName.text = owner != null ? owner.displayName : "Neutral";
        if (ownerColorSwatch) ownerColorSwatch.color = owner != null ? owner.color : Color.gray;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);
}
