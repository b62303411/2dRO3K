using UnityEngine;

using System.Collections.Generic;
public class GameManager : MonoBehaviour
{
   
    public GameConfig config;
    public MapData mapData;
    public MapRenderer mapRenderer;
    public CityPanelUI cityPanelUI;
    public TurnUI turnUI;

    public List<GameConfig.FactionDef> turnOrder;
    
    public int turnIndex = 0;

    private Dictionary<string, GameConfig.FactionDef> factionsById;



    public GameConfig.FactionDef CurrentFaction;

    void OnEnable()
    {
        CurrentFaction  = turnOrder[turnIndex];
        if (turnOrder == null) turnOrder = new List<GameConfig.FactionDef>();
    }

    void Awake()
    {
        if (!config || !mapData) Debug.LogError("Assign GameConfig and MapData on GameManager.");
        factionsById = new Dictionary<string, GameConfig.FactionDef>();
        foreach (var f in config.factions) factionsById[f.id] = f;

        // Simple turn order = order in config (you can shuffle/seed this later)
        turnOrder = new List<GameConfig.FactionDef>(config.factions);
    }

    void Start()
    {
        mapRenderer.Build(mapData, this, config);
        if (cityPanelUI) cityPanelUI.Hide();
        if (turnUI) turnUI.Bind(this);
        NotifyTurnChanged();
    }

    public GameConfig.FactionDef GetFaction(string id)
    {
        return factionsById != null && factionsById.TryGetValue(id, out var f) ? f : null;
    }

    public void SelectCity(CityView cityView)
    {
        var city = cityView.City;
        var owner = GetFaction(city.ownerFactionId);
        if (cityPanelUI) cityPanelUI.Show(city, owner);
    }

    public void EndTurn()
    {
        turnIndex = (turnIndex + 1) % turnOrder.Count;
        NotifyTurnChanged();
        // Later: trigger per-turn production, AI actions, etc.
    }

    private void NotifyTurnChanged()
    {
        if (turnUI) turnUI.SetFaction(CurrentFaction);
    }
}
