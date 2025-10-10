using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class ChunkGridIndex : MonoBehaviour
{
    [Header("Chunk layout (en cellules)")]
    public Vector2Int chunkSize = new Vector2Int(64, 64);  // largeur/hauteur de partition
    public Vector3Int gridOrigin = Vector3Int.zero;        // origine commune (cell coords)

    [Tooltip("Laisse vide pour auto-scan sous le Grid")]
    public List<Tilemap> chunks = new List<Tilemap>();

    // Map: coordonnée de chunk -> liste de tilemaps (1 typiquement, >1 si overlap/data-only)
    private readonly Dictionary<Vector2Int, List<Tilemap>> _index = new();

    // (optionnel) priorité d’affichage quand plusieurs chunks recouvrent la même cellule
    private readonly Dictionary<Tilemap, int> _prio = new();

    // Buffer réutilisable pour éviter les allocs
    private readonly List<Tilemap> _tmpList = new(4);

    void Awake()
    {
        if (chunks.Count == 0)
        {
            GetComponentsInChildren(true, chunks);
        }
        BuildIndex();
    }

    public void BuildIndex()
    {
        _index.Clear();
        _prio.Clear();

        foreach (var tm in chunks)
        {
            if (!tm) continue;

            // IMPORTANT: on fige les bounds au démarrage (ou appelle CompressBounds après édition)
            var b = tm.cellBounds; // min inclusif, size en cellules

            // On dérive l’intervalle de chunks couverts par ce tilemap
            var minChunk = CellToChunk(b.min);
            var maxCell = b.min + b.size - new Vector3Int(1, 1, 0);
            var maxChunk = CellToChunk(maxCell);

            for (int cy = minChunk.y; cy <= maxChunk.y; cy++)
                for (int cx = minChunk.x; cx <= maxChunk.x; cx++)
                {
                    var key = new Vector2Int(cx, cy);
                    if (!_index.TryGetValue(key, out var list))
                    {
                        list = new List<Tilemap>(1);
                        _index[key] = list;
                    }
                    list.Add(tm);
                }

            // priorité par défaut 0 (tu peux exposer un serialized dict si tu veux)
            _prio[tm] = 0;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public Vector2Int CellToChunk(Vector3Int cell)
    {
        int cx = Mathf.FloorToInt((cell.x - gridOrigin.x) / (float)chunkSize.x);
        int cy = Mathf.FloorToInt((cell.y - gridOrigin.y) / (float)chunkSize.y);
        return new Vector2Int(cx, cy);
    }

    // Récupère le tile le plus prioritaire à cette cellule (O(1) listes très petites)
    public TileBase GetTileGlobal(Vector3Int cell)
    {
        _tmpList.Clear();
        var key = CellToChunk(cell);
        if (_index.TryGetValue(key, out var list))
        {
            TileBase best = null;
            int bestP = int.MinValue;

            for (int i = 0; i < list.Count; i++)
            {
                var tm = list[i];
                if (!tm || !tm.isActiveAndEnabled) continue;

                // Filtre rapide: évite les GetTile hors bounds
                if (!tm.cellBounds.Contains(cell)) continue;

                // HasTile est parfois plus rapide selon Unity; mais ici GetTile évite un 2e lookup
                var t = tm.GetTile(cell);
                if (!t) continue;

                int p = _prio.TryGetValue(tm, out var pr) ? pr : 0;
                if (t != null && (best == null || p > bestP))
                {
                    best = t; bestP = p;
                }
            }
            return best;
        }
        return null;
    }

    // Refresh uniquement les tilemaps candidates pour cette cellule
    public void RefreshAt(Vector3Int cell)
    {
        var key = CellToChunk(cell);
        if (_index.TryGetValue(key, out var list))
        {
            for (int i = 0; i < list.Count; i++)
            {
                var tm = list[i];
                if (tm) tm.RefreshTile(cell);
            }
        }
    }

    // Refresh 8-voisinage ciblé
    public void RefreshNeighborhood(Vector3Int center)
    {
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                var c = new Vector3Int(center.x + x, center.y + y, center.z);
                RefreshAt(c);
            }
    }
}
