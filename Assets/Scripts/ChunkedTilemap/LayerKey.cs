using System;
using UnityEngine;
struct LayerKey : IEquatable<LayerKey>
{
    public Vector2Int chunk;
    public int layerId;

    public LayerKey(Vector2Int c, int l) { chunk = c; layerId = l; }

    public bool Equals(LayerKey other) =>
        chunk.x == other.chunk.x && chunk.y == other.chunk.y && layerId == other.layerId;

    public override bool Equals(object obj) => obj is LayerKey o && Equals(o);

    public override int GetHashCode()
    {
        // Fast, stable hash (pairwise mix)
        unchecked
        {
            int h = 17;
            h = h * 31 + chunk.x;
            h = h * 31 + chunk.y;
            h = h * 31 + layerId;
            return h;
        }
    }
}
