using UnityEngine;
using System.Collections.Generic;


[CreateAssetMenu(menuName = "ROTK/GameConfig")]
public class GameConfig : ScriptableObject
{
    [System.Serializable]
    public class FactionDef
    {
        public string id;
        public string displayName;
        public Color color = Color.white;
    }

    public List<FactionDef> factions = new List<FactionDef>();

    public FactionDef GetFaction(string id) => factions.Find(f => f.id == id);
}
