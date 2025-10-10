using UnityEngine;
using UnityEngine.Tilemaps;
public class CrossPartitionRuleTile : RuleTile<CrossPartitionRuleTile.Neighbor>
{
    public class Neighbor : RuleTile.TilingRule.Neighbor { }

    ParentChunkGridIndex FindIndex(ITilemap tilemap)
    {
        var tm = tilemap.GetComponent<Tilemap>();
        //var g = tm.GetComponent<Grid>();
        var grid  = tm.layoutGrid;

        //var g = tilemap.GetComponent<Grid>();
        return grid ? grid.GetComponentInParent<ParentChunkGridIndex>() : null;
    }

    int GetLayerId(ParentChunkGridIndex idx, ITilemap tilemap)
    {
        var tm = tilemap.GetComponent<Tilemap>();
        return tm ? ParentChunkGridIndex.ResolveLayerId(tm) : 0; // or expose a public method on the index
    }
    //position relative to grid
    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        var idx = FindIndex(tilemap);
        var layer = GetLayerId(idx, tilemap);
        var tm = tilemap.GetComponent<Tilemap>();
        var offs = rule.m_NeighborPositions;
        var expect = rule.m_Neighbors;

        var myGrid = tm.layoutGrid;
        ChunkTag tag = myGrid.GetComponent<ChunkTag>();
        if(null != tag) {
        var chunk = tag.chunk;
        }


        for (int i = 0; i < offs.Count; i++)
        {

            var tp = transform.MultiplyPoint3x4((Vector3)offs[i]);
            var p = position + new Vector3Int(Mathf.RoundToInt(tp.x), Mathf.RoundToInt(tp.y), 0);
            Vector3 fwp = myGrid.CellToWorld(p);
           
            // Ask the ROOT (parent Grid) to resolve from parent-space
            TileBase other;
            if (idx != null)
            {
                //Vector3Int cell = tm.WorldToCell(p);
                bool inside = tm.cellBounds.Contains(p);
                if (!inside)
                {

                    Vector3Int probe_world_position = new Vector3Int(Mathf.RoundToInt(fwp.x), Mathf.RoundToInt(fwp.y), 0);
                    other = idx.GetTileGlobalSameLayer(probe_world_position, layer);
                }
                else 
                {
                    other = tilemap.GetTile(p);
                }

            }
            else
            {
                other = tilemap.GetTile(p); // fallback: same tilemap only
            }

            if (!base.RuleMatch(expect[i], other)) return false;
        }
        return true;
    }

    private Grid GetGrid(ITilemap tilemap) 
    {
        var tm = tilemap.GetComponent<Tilemap>();
        //var g = tm.GetComponent<Grid>();
        var grid = tm.layoutGrid;
        return grid;
    }

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        base.RefreshTile(position, tilemap);
        var idx = FindIndex(tilemap);
        if (idx != null)
        {
            var myGrid = GetGrid(tilemap);
            var parentCell = idx.parentGrid.WorldToCell(myGrid.CellToWorld(position));
            var layer = GetLayerId(idx, tilemap);
            idx.RefreshNeighborhoodSameLayer(parentCell, layer);
        }
    }
}
