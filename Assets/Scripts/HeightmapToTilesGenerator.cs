using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class HeightmapToTilesGenerator : MonoBehaviour
{
    [Header("Input")]
    public Texture2D heightmap;            // grayscale
    public TerrainTilerConfig cfg;
    public Tile obstacle;
    [Header("Outputs")]
    public Tilemap sandMap;                // couche principale (océan/sable/herbe/roche/neige OU plateau)
    public Tilemap grassMap;                // couche principale (océan/sable/herbe/roche/neige OU plateau)
    public Tilemap oceanMap;                // couche principale (océan/sable/herbe/roche/neige OU plateau)
    public Tilemap elevationMap;                // couche principale (océan/sable/herbe/roche/neige OU plateau)
    public Tilemap bridgeMap;              // ponts
    public Tilemap debugMap;               // optionnel (marquage composantes / tests)
    public Tilemap collisionMap;

    [Header("General")]
    public bool clearOnGenerate = true;
    public Vector2Int worldOrigin = Vector2Int.zero; // offset placement

    // internes
    float[,] H;
    int W, Ht;

    // Directions N/E/S/O
    static readonly Vector2Int[] DIR4 = new[]{
        new Vector2Int(0,1),  // N
        new Vector2Int(1,0),  // E
        new Vector2Int(0,-1), // S
        new Vector2Int(-1,0)  // O
    };

    [ContextMenu("Generate")]
    public void Generate()
    {
        List<Tilemap> maps = new List<Tilemap>();
        if(sandMap)
            maps.Add(sandMap);
        if(oceanMap)
            maps.Add(oceanMap);
        if(grassMap)
            maps.Add(grassMap);
        if(collisionMap)
            maps.Add(collisionMap);
        if(elevationMap)
            maps.Add(elevationMap); 
        if(bridgeMap)
            maps.Add(bridgeMap);
        if (!Validate()) return;

        if (clearOnGenerate)
        {
            foreach (Tilemap tm in maps) 
            {
                sandMap.ClearAllTiles();
            }
        }

        LoadHeight();
        Normalize01();

        // 1) Trouver le point le plus bas
        var minPt = FindGlobalMin();

        // 2) Quantifier en terrasses
        int[,] level = QuantizeTerraces(cfg.terraceStep, cfg.seaLevel);

        // 3) Construire les plateaux par niveau, en montant depuis le min (BFS 4-voisins)
        //    On segmente chaque niveau en composantes (plateaux).
        var componentsPerLevel = BuildPlateauComponents(level);

        // 4) Peindre la base (biomes) OU simplement plateau
        PaintBase(level);

        // 5) Connecter par PONTS les composantes d’un même niveau séparées par eau/creux
        BuildAndPaintBridges(level, componentsPerLevel);

        if (cfg.logStats)
            Debug.Log($"[PlateauTiler] {W}x{Ht} | levels≈{1f / cfg.terraceStep:0} | bridges: done.");
    }

    bool Validate()
    {
        if (heightmap == null || cfg == null || sandMap == null)
        {
            Debug.LogError("Config/heightmap/baseMap manquants.");
            return false;
        }
        if (cfg.paintByBiome && (cfg.oceanTile == null || cfg.grassTile == null))
            Debug.LogWarning("Tu peins par biome mais certains tiles sont null.");
        if (bridgeMap == null && (cfg.bridgeTileStraightEW != null || cfg.bridgeTileStraightNS != null))
            Debug.LogWarning("Des tiles pont sont fournis mais pas de Tilemap pont.");
        return true;
    }

    void LoadHeight()
    {
        W = heightmap.width;
        Ht = heightmap.height;
        H = new float[W, Ht];
        var cols = heightmap.GetPixels();
        for (int y = 0; y < Ht; y++)
        {
            for (int x = 0; x < W; x++)
            {
                var c = cols[y * W + x];
                // grayscale — prends la luminance
                H[x, y] = c.grayscale;
            }
        }
    }

    void Normalize01()
    {
        float mn = 1f, mx = 0f;
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < W; x++)
            { mn = Mathf.Min(mn, H[x, y]); mx = Mathf.Max(mx, H[x, y]); }
        float r = Mathf.Max(1e-6f, mx - mn);
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < W; x++)
                H[x, y] = (H[x, y] - mn) / r;
    }

    Vector2Int FindGlobalMin()
    {
        float best = 2f;
        Vector2Int p = Vector2Int.zero;
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < W; x++)
                if (H[x, y] < best) { best = H[x, y]; p = new Vector2Int(x, y); }
        return p;
    }

    // Retourne un indice de niveau (0,1,2,...) par cell, -1 pour océan
    int[,] QuantizeTerraces(float step, float sea)
    {
        int[,] L = new int[W, Ht];
        for (int y = 0; y < Ht; y++)
        {
            for (int x = 0; x < W; x++)
            {
                float h = H[x, y];
                if (h <= sea) { L[x, y] = -1; continue; }
                float above = h - sea;
                int lvl = Mathf.Max(0, Mathf.FloorToInt(above / Mathf.Max(step, 1e-6f)));
                L[x, y] = lvl;
            }
        }
        return L;
    }

    // Segmentation 4-voisins par niveau, renvoie la liste des composantes pour chaque niveau
    Dictionary<int, List<List<Vector2Int>>> BuildPlateauComponents(int[,] L)
    {
        var perLevel = new Dictionary<int, List<List<Vector2Int>>>();
        bool[,] vis = new bool[W, Ht];

        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < W; x++)
            {
                if (L[x, y] < 0 || vis[x, y]) continue;
                int lvl = L[x, y];
                var comp = new List<Vector2Int>();
                var q = new Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(x, y));
                vis[x, y] = true;

                while (q.Count > 0)
                {
                    var u = q.Dequeue();
                    comp.Add(u);
                    foreach (var d in DIR4)
                    {
                        int nx = u.x + d.x, ny = u.y + d.y;
                        if (nx < 0 || ny < 0 || nx >= W || ny >= Ht) continue;
                        if (vis[nx, ny]) continue;
                        if (L[nx, ny] == lvl)
                        {
                            vis[nx, ny] = true;
                            q.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }

                if (!perLevel.TryGetValue(lvl, out var list))
                {
                    list = new List<List<Vector2Int>>();
                    perLevel[lvl] = list;
                }
                list.Add(comp);
            }

        return perLevel;
    }

    void PaintBase(int[,] L)
    {
        // Peinture rapide (plein); transitions = à gérer via RuleTiles si tu veux les bords automatiques.
        for (int y = 0; y < Ht; y++)
            for (int x = 0; x < W; x++)
            {
                var wpos = new Vector3Int(worldOrigin.x + x, worldOrigin.y + y, 0);
                float h = H[x, y];
                sandMap.SetTile(wpos, cfg.sandTile);
                if (h <= cfg.seaLevel)
                {
                    collisionMap.SetTile(wpos, obstacle);
                    oceanMap.SetTile(wpos, cfg.oceanTile);
                    continue;
                }
                if (h <= cfg.riverLevel)
                {
                    collisionMap.SetTile(wpos, obstacle);
                    collisionMap.SetTile(wpos, cfg.oceanTile);
                    oceanMap.SetTile(wpos, cfg.riverTile);
                    continue;
                }


                if (!cfg.paintByBiome)
                {
                    grassMap.SetTile(wpos, cfg.plateauTile);
                    continue;
                }

                // Biomes simples sable/herbe/roche/neige
                // Plage = near coast ET dans beachBand
                bool nearCoast = IsNearOcean(x, y, 4);
                if (h <= cfg.seaLevel + cfg.beachBand && nearCoast)
                {
                    grassMap.SetTile(wpos, cfg.sandTile != null ? cfg.sandTile : cfg.grassTile);
                    continue;
                }

                // neige hautes altitudes
                if (h >= cfg.snowLevel && cfg.snowTile != null)
                {
                    elevationMap.SetTile(wpos, cfg.snowTile);
                    continue;
                }

                // roche rudimentaire si très au-dessus (tu peux remplacer par pente si tu veux raffiner)
                if (h >= (cfg.snowLevel - 0.1f) && cfg.rockTile != null)
                {
                    elevationMap.SetTile(wpos, cfg.rockTile);
                    continue;
                }

                // par défaut herbe
                grassMap.SetTile(wpos, cfg.grassTile);
            }
    }

    bool IsNearOcean(int x, int y, int radius)
    {
        for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= W || ny >= Ht) continue;
                if (H[nx, ny] <= cfg.seaLevel) return true;
            }
        return false;
    }

    // Construire des ponts N/E/S/O entre composantes d’un même niveau si séparées par eau/creux
    void BuildAndPaintBridges(int[,] L, Dictionary<int, List<List<Vector2Int>>> perLevel)
    {
        if (bridgeMap == null || (cfg.bridgeTileStraightEW == null && cfg.bridgeTileStraightNS == null))
            return;

        foreach (var kv in perLevel)
        {
            int lvl = kv.Key;
            var comps = kv.Value;
            if (comps.Count <= 1) continue;

            // On se contente de connecter en MST naïve: on relie chaque composante au plus proche voisin par un chemin 4-voisins
            var centroids = new Vector2Int[comps.Count];
            for (int i = 0; i < comps.Count; i++)
                centroids[i] = ApproxCentroid(comps[i]);

            var connected = new HashSet<int> { 0 };
            var edges = new List<(int a, int b)>();

            while (connected.Count < comps.Count)
            {
                float best = float.MaxValue; int ba = -1, bb = -1;
                foreach (int a in connected)
                    for (int b = 0; b < comps.Count; b++)
                    {
                        if (connected.Contains(b)) continue;
                        float d = Manhattan(centroids[a], centroids[b]);
                        if (d < best) { best = d; ba = a; bb = b; }
                    }
                if (ba == -1) break;
                edges.Add((ba, bb));
                connected.Add(bb);
            }

            foreach (var (a, b) in edges)
            {
                // chemin axis-aligné préféré (d’abord horizontal puis vertical et vice-versa), sinon BFS 4-voisins
                TryLayAxisAlignedBridge(L, lvl, centroids[a], centroids[b], out var laid);
                if (!laid)
                {
                    TryLayBFSBridge(L, lvl, centroids[a], centroids[b]);
                }
            }
        }
    }

    Vector2Int ApproxCentroid(List<Vector2Int> comp)
    {
        long sx = 0, sy = 0;
        foreach (var p in comp) { sx += p.x; sy += p.y; }
        return new Vector2Int((int)(sx / comp.Count), (int)(sy / comp.Count));
    }
    int ManhattanInt(Vector2Int a, Vector2Int b)
    => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    float Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    bool IsBridgeableCell(int x, int y, int lvl)
    {
        if (x < 0 || y < 0 || x >= W || y >= Ht) return false;
        float floor = cfg.seaLevel + lvl * cfg.terraceStep;
        // on autorise le pont sur eau/creux/terrain plus bas que le plateau courant + petite tolérance (rampe)
        return H[x, y] <= floor + cfg.bridgeRampAllowance;
    }

    bool TryLayAxisAlignedBridge(int[,] L, int lvl, Vector2Int a, Vector2Int b, out bool laid)
    {
        laid = false;
        // H → V
        if (TryLayStraight(L, lvl, new Vector2Int(a.x, a.y), new Vector2Int(b.x, a.y)))
        {
            if (TryLayStraight(L, lvl, new Vector2Int(b.x, a.y), new Vector2Int(b.x, b.y))) { laid = true; return true; }
            // rollback minimal? ici on s’en fout: on recouvrira par le 2e tracé si besoin
        }
        // V → H
        if (TryLayStraight(L, lvl, new Vector2Int(a.x, a.y), new Vector2Int(a.x, b.y)))
        {
            if (TryLayStraight(L, lvl, new Vector2Int(a.x, b.y), new Vector2Int(b.x, b.y))) { laid = true; return true; }
        }
        return false;
    }

    bool TryLayStraight(int[,] L, int lvl, Vector2Int s, Vector2Int t)
    {
        if (Manhattan(s, t) > cfg.maxBridgeLength) return false;
        if (s.x != t.x && s.y != t.y) return false; // axis-only
        int dx = Mathf.Clamp(t.x - s.x, -1, 1);
        int dy = Mathf.Clamp(t.y - s.y, -1, 1);
        var p = s;
        int steps = ManhattanInt(s, t);
        for (int i = 0; i <= steps; i++)
        {
            if (!IsBridgeableCell(p.x, p.y, lvl)) return false;
            p = new Vector2Int(p.x + dx, p.y + dy);
        }
        // peindre
        p = s;
        for (int i = 0; i <= steps; i++)
        {
            PaintBridgeCell(p, dx != 0 ? Orientation.EW : Orientation.NS);
            p = new Vector2Int(p.x + dx, p.y + dy);
        }
        return true;
    }

    bool TryLayBFSBridge(int[,] L, int lvl, Vector2Int s, Vector2Int t)
    {
        if (Manhattan(s, t) > cfg.maxBridgeLength) return false;

        var q = new Queue<Vector2Int>();
        var prev = new Dictionary<Vector2Int, Vector2Int>();
        var seen = new HashSet<Vector2Int>();
        q.Enqueue(s); seen.Add(s);

        while (q.Count > 0)
        {
            var u = q.Dequeue();
            if (u == t) break;
            foreach (var d in DIR4)
            {
                var v = new Vector2Int(u.x + d.x, u.y + d.y);
                if (v.x < 0 || v.y < 0 || v.x >= W || v.y >= Ht) continue;
                if (seen.Contains(v)) continue;
                if (!IsBridgeableCell(v.x, v.y, lvl)) continue;
                prev[v] = u;
                seen.Add(v);
                q.Enqueue(v);
                if (seen.Count > cfg.maxBridgeLength * 8) break; // garde-fou
            }
        }

        if (!prev.ContainsKey(t)) return false;

        // remonter le chemin
        var path = new List<Vector2Int>();
        var cur = t;
        path.Add(cur);
        while (cur != s)
        {
            cur = prev[cur];
            path.Add(cur);
        }
        path.Reverse();

        // peindre orientation par segment
        for (int i = 0; i < path.Count; i++)
        {
            Orientation o = Orientation.NS;
            if (i > 0)
            {
                var d = path[i] - path[i - 1];
                o = (Mathf.Abs(d.x) == 1) ? Orientation.EW : Orientation.NS;
            }
            PaintBridgeCell(path[i], o);
        }
        return true;
    }

    enum Orientation { NS, EW }

    void PaintBridgeCell(Vector2Int p, Orientation o)
    {
        if (bridgeMap == null) return;
        var cell = new Vector3Int(worldOrigin.x + p.x, worldOrigin.y + p.y, 0);
        var tile = (o == Orientation.NS) ? cfg.bridgeTileStraightNS : cfg.bridgeTileStraightEW;
        if (tile != null)
            bridgeMap.SetTile(cell, tile);

        // Optionnel: pilier si sous le pont c’est de l’eau
        if (cfg.bridgeTilePillar != null && H[p.x, p.y] <= cfg.seaLevel)
        {
            // place le pilier dans debugMap (ou même bridgeMap si tu préfères une autre couche)
            if (debugMap != null) debugMap.SetTile(cell, cfg.bridgeTilePillar);
        }
    }
}
