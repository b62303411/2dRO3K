using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "ROTK/MapData")]
public class MapData : ScriptableObject
{
    [System.Serializable]
    public class City
    {
        public string id;
        public string displayName;
        public Vector2 position;       // world coords (units)
        public string ownerFactionId;  // must match a GameConfig.FactionDef.id
        public int population = 10000; // placeholder
    }

    [System.Serializable]
    public class Road
    {
        public string fromCityId;
        public string toCityId;
        public int moveCost = 1;       // placeholder for later movement rules
    }

    public List<City> cities = new List<City>();
    public List<Road> roads = new List<Road>();

    public City GetCity(string id) => cities.Find(c => c.id == id);
}
