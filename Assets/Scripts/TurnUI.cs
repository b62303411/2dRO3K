using UnityEngine;
using UnityEngine.UI;

public class TurnUI : MonoBehaviour
{
    public Text factionName;
    public Image factionColor;
    public Button endTurnButton;
    private GameManager gm;

    public void Bind(GameManager manager)
    {
        gm = manager;
        if (endTurnButton)
        {
            endTurnButton.onClick.RemoveAllListeners();
            endTurnButton.onClick.AddListener(() => gm.EndTurn());
        }
    }

    public void SetFaction(GameConfig.FactionDef faction)
    {
        if (factionName) factionName.text = $"Turn: {faction.displayName}";
        if (factionColor) factionColor.color = faction.color;
    }
}
