// ============================================================================
// File: Assets/Scripts/ChunkedTilemap/TilemapWorld.cs
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class TilemapWorld : MonoBehaviour
{
    [Header("Config")] public ChunkSettings settings;
    public Grid grid;                                        // parent Grid
    public Dictionary<Vector2Int, TilemapChunk> chunks = new();

    private readonly HashSet<TilemapChunk> _dirty = new();

    void Reset() { if (!grid) grid = GetComponent<Grid>(); }

    public void CreateWorld()
    {
        if (!settings) { Debug.LogError("Missing ChunkSettings"); return; }
        if (!grid) grid = GetComponent<Grid>();
        grid.cellLayout = settings.cellLayout;

        chunks.Clear();
        for (int y = 0; y < settings.initialChunks.y; y++)
            for (int x = 0; x < settings.initialChunks.x; x++)
                EnsureChunk(new Vector2Int(x, y));
    }

    public TilemapChunk EnsureChunk(Vector2Int c)
    {
        if (chunks.TryGetValue(c, out var existing) && existing) return existing;
        var go = new GameObject($"Chunk_{c.x}_{c.y}");
        go.transform.SetParent(transform, false);
        var tc = go.AddComponent<TilemapChunk>();
        tc.coord = c; tc.settings = settings; tc.RecomputeBounds();
        tc.EnsureLayers(grid, settings.layerNames);
        chunks[c] = tc;
        return tc;
    }

    public (TilemapChunk chunk, Vector3Int localCell) ResolveCell(Vector3Int worldCell)
    {
        int s = settings.chunkSize; int o = settings.overlap;
        int cx = Mathf.FloorToInt((float)worldCell.x / s);
        int cy = Mathf.FloorToInt((float)worldCell.y / s);
        var c = new Vector2Int(cx, cy);
        var chunk = EnsureChunk(c);
        var origin = new Vector3Int(c.x * s - o, c.y * s - o, 0);
        return (chunk, worldCell - origin);
    }

    // Write a tile to a layer, mark dirty chunk and also neighbor borders when near edges
    public void SetTile(int layerIndex, Vector3Int worldCell, TileBase tile)
    {
        var (chunk, local) = ResolveCell(worldCell);
        chunk.layers[layerIndex].SetTile(local, tile);
        MarkDirty(chunk);

        var usable = chunk.UsableBounds;
        if (worldCell.x == usable.xMin) TouchNeighbor(layerIndex, worldCell + Vector3Int.left);
        if (worldCell.x == usable.xMax - 1) TouchNeighbor(layerIndex, worldCell + Vector3Int.right);
        if (worldCell.y == usable.yMin) TouchNeighbor(layerIndex, worldCell + Vector3Int.down);
        if (worldCell.y == usable.yMax - 1) TouchNeighbor(layerIndex, worldCell + Vector3Int.up);
    }

    void TouchNeighbor(int layerIndex, Vector3Int neighborCell)
    {
        var (nChunk, nLocal) = ResolveCell(neighborCell);
        var tm = nChunk.layers[layerIndex];
        var t = tm.GetTile(nLocal); // no-op write to force dirty/refresh
        tm.SetTile(nLocal, t);
        MarkDirty(nChunk);
    }

    public void MarkDirty(TilemapChunk c) { _dirty.Add(c); }

    public void RefreshDirty()
    {
        foreach (var c in _dirty)
            foreach (var tm in c.layers) tm.RefreshAllTiles();
        _dirty.Clear();
    }
}