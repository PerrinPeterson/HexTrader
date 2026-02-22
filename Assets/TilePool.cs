using System.Collections.Generic;
using UnityEngine;

//Wrapper class to hold a tile, ease of assigning textures, because good god.
public static class TileWrapper
{
    /* 0 = Corner AB
    * 1 = Corner DE
    * 2 = Corner CD
    * 3 = Corner BC
    * 4 = Main
    * 5 = Corner EF
    * 6 = Corner FA
    * 7 = Corner FE
    * 8 = Corner ED
    * 9 = Corner DC
    * 10 = Corner CB
    * 11 = Corner BA
    * 12 = Corner AF
    * 13 = Edge E
    * 14 = Edge D
    * 15 = Edge C
    * 16 = Edge B
    * 17 = Edge A
    * 18 = Edge F
    * 
    * Side Note, the Corners are labled with the letters of the edges they are between, the first letter is the edge they're ON.
    * 
    * */
    static readonly int[] edgeIndicies = { 17, 16, 15, 14, 13, 18 };
    static readonly int[][] cornerIndicies = {
            new int[] {12, 0}, //A Corners
            new int[] {11, 3}, //B Corners
            new int[] {10, 2}, //C Corners
            new int[] {9, 1}, //D Corners
            new int[] {8, 5}, //E Corners
            new int[] {7, 6}, //F Corners
    };

    public static void SetCoreMaterial(Material mat, Material[] mats)
    {
        mats[4] = mat;
    }

    public static Material GetCoreMaterial(Material[] mats)
    {
        return mats[4];
    }

    //Set edge material, optionally set a specific corner material, or ignore corner materials.
    public static void SetEdge(Material mat, Material[] mats, int EdgeIndex, int CornerIndex = -1)
    {
        if (EdgeIndex < 0 || EdgeIndex >= 6)
            return;
        if (CornerIndex > 2 || CornerIndex < -1)
            return;

        //If the corner index is -1, we can assume we want the material across the whole edge
        if (CornerIndex != -1 && CornerIndex != 2)
        {
            mats[cornerIndicies[EdgeIndex][CornerIndex]] = mat;
            if (CornerIndex == 0)//Left corner, whole thing
                mats[cornerIndicies[(EdgeIndex + 5) % 6][1]] = mat;
            else //Right corner, whole thing
                mats[cornerIndicies[(EdgeIndex + 1) % 6][0]] = mat;

            return;
        }
        if (CornerIndex == 2) //We'll assume we just want to set the center of the edge, leaving the corners as is.
        {
            mats[edgeIndicies[EdgeIndex]] = mat;
            return;
        }
        mats[edgeIndicies[EdgeIndex]] = mat;
        mats[cornerIndicies[EdgeIndex][0]] = mat;
        mats[cornerIndicies[EdgeIndex][1]] = mat;
    }

    public static Material GetEdge(int EdgeIndex, Material[] mats, int CornerIndex = -1)
    {
        if (EdgeIndex < 0 || EdgeIndex >= 6)
            return null;
        if (CornerIndex > 1 || CornerIndex < -1)
            return null;
        if (mats == null)
            return null;
        //If the corner index is -1, we can assume we want the material across the whole edge
        if (CornerIndex != -1)
        {
            return mats[cornerIndicies[EdgeIndex][CornerIndex]];
        }
        return mats[edgeIndicies[EdgeIndex]];
    }
}


public class TilePool : MonoBehaviour
{
    public playerController player;
    public GameObject tile;
    public List<GameObject> tiles;
    public WorldManager worldManager;
    public int RenderCacheSize;
    public float tileWidth = 1.0f;
    public float tileHeight = 1.1547f;
    public Vector2 rowOffset = new Vector2(-0.5f, 0.8660254f);
    public Vector2 moveOffset = new Vector2(0, 0);
    public Vector2Int BottomLeftMapCoord = new Vector2Int(0, 0);
    public Vector3Int BottomLeftCubeCoord = new Vector3Int(0, 0, 0);
    public Vector3Int DebugTile = new Vector3Int(-1, -1, -1);

    private Vector2Int playmatSize;
    private HexGrid playmat;
    private List<Renderer> rendererCache;
    private Material[][] matCache;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Mathf.Abs(moveOffset.x) > tileWidth || Mathf.Abs(moveOffset.y) > rowOffset.y)
        {
            Vector2 direction = new Vector2(0, 0);
            if (Mathf.Abs(moveOffset.x) > tileWidth)
            {
                direction.x = moveOffset.x > 0 ? 1 : -1;
            }
            if (Mathf.Abs(moveOffset.y) > rowOffset.y)
            {
                direction.y = moveOffset.y > 0 ? 1 : -1;
            }
            //TODO: Change this to a while loop, in case the player is moving super fast. Currently, if they move more than one tile in a frame, it will only shift once. And the map will scroll offscreen.
            shift(direction);
        }
    }

    public void MovePlaymat(Vector2 direction, float deltaTime, float speed)
    {
        Vector2 movement = -direction * speed * deltaTime;
        foreach(GameObject tile in tiles)
        {
            tile.transform.position += new Vector3(movement.x, 0, movement.y);
        }
        moveOffset += movement;
    }

    public void GeneratePool(int size)
    {
        tiles = new List<GameObject>(size);
        rendererCache = new List<Renderer>(new Renderer[playmatSize.x * playmatSize.y]);
        for (int i = 0; i < size; i++)
        {
            GameObject newTile = Instantiate(tile, transform);
            newTile.SetActive(false);
            newTile.transform.rotation = Quaternion.Euler(90, 0, 0); //orient the tile to be flat on the ground, TODO: fix this in the prefab itself
            newTile.AddComponent<TileData>();
            tiles.Add(newTile);
            rendererCache.Add(newTile.GetComponent<Renderer>());
        }

    }

    public void PlaceAroundCenter(Vector2 centerPosition, Vector2Int playMatSize, Vector2Int centerStartCoords)
    {
        BottomLeftMapCoord = new Vector2Int(centerStartCoords.x - playMatSize.x / 2, centerStartCoords.y - playMatSize.y / 2);
        playmatSize = new Vector2Int(playMatSize.x, playMatSize.y);
        playmat = new HexGrid(playmatSize.x, playmatSize.y);
        playmat.bOddR = false;


        Vector2 firstTileOffset;
        float x = centerPosition.x - (tileWidth * playMatSize.x) / 2 + tileWidth / 2;
        float y = centerPosition.y - (rowOffset.y * playMatSize.y) / 2 + rowOffset.y / 2;
        firstTileOffset = new Vector2(x, y);

        List<Vector3Int> changedTiles = new List<Vector3Int>();

        int tileIndex = 0;
        for (int row = 0; row < playMatSize.y; row++)
        {
            for (int col = 0; col < playMatSize.x; col++)
            {
                if (tileIndex >= tiles.Count)
                    return;
                Vector2 offset = firstTileOffset + new Vector2(col * tileWidth, row * rowOffset.y);
                if (row % 2 == 1)
                {
                    offset.x += rowOffset.x; //Apply offset for odd rows
                }
                tiles[tileIndex].transform.position = new Vector3(offset.x, 0, offset.y);
                tiles[tileIndex].SetActive(true);

                //TODO: This is very heavy, so get rid of this for releases.
                HexData hexData = worldManager.world.GetCellData(worldManager.world.OddRToCube(BottomLeftMapCoord.x + col, BottomLeftMapCoord.y + row));
                TilePacker tilePacker = worldManager.tilePacker;
                tilePacker.SetPackedTile(hexData); //Don't need the corner data here, so just send 0
                int[] biomes = new int[7];
                biomes = tilePacker.biomes;
                bool[] blends = new bool[6];
                bool[] sameIndex = new bool[6];
                for (int j = 0; j < 6; j++)
                {
                    blends[j] = tilePacker.edgeBlends[j].isBlend;
                    sameIndex[j] = tilePacker.edgeBlends[j].isSameIndex;
                }


                tiles[tileIndex].GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + col, BottomLeftMapCoord.y + row);
                tiles[tileIndex].GetComponent<TileData>().tileID = hexData.ID;
                tiles[tileIndex].GetComponent<TileData>().playmatPos = playmat.OddRToCube(col, row);
                tiles[tileIndex].GetComponent<TileData>().unpacked = (int[])biomes.Clone();
                tiles[tileIndex].GetComponent<TileData>().blends = (bool[])blends.Clone();
                tiles[tileIndex].GetComponent<TileData>().sameIndex = (bool[])sameIndex.Clone();


                changedTiles.Add(tiles[tileIndex].GetComponent<TileData>().gridPos);
                tileIndex++;
            }
        }
        matCache = new Material[playmatSize.x * playmatSize.y][];
        FetchTiles(changedTiles.ToArray());
    }

    //Move tiles from offscreen to onscreen
    public void shift(Vector2 direction)
    {
        int playMatWidth = playmatSize.x;
        int playMatHeight = playmatSize.y;
        List<Vector3Int> changedTiles = new List<Vector3Int>();
        if (direction.x == -1) //Move the tile from the left edge to the right edge
        {
            //Take the first tile, and every playMatSize.x tile after that, and move it tileWidth * playMatSize.x to the right
            //Then, we'll reorganize the list to reflect the new order
            moveOffset.x += tileWidth;
            BottomLeftMapCoord.x += 1;
            for (int i = 0; i < tiles.Count; i += playMatWidth)
            {
                tiles[i].transform.position += new Vector3(tileWidth * playMatWidth, 0, 0);
                GameObject movedTile = tiles[i];
                tiles.Insert((playMatWidth + i), movedTile);
                tiles.RemoveAt(i);
                //Shift the renderer cache as well
                rendererCache.Insert((playMatWidth + i), rendererCache[i]);
                rendererCache.RemoveAt(i);
                movedTile.GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + ((playMatWidth - 1)), BottomLeftMapCoord.y + (i / playMatWidth));
                changedTiles.Add(movedTile.GetComponent<TileData>().gridPos);

            }

        }
        else if (direction.x == 1)
        {
            moveOffset.x -= tileWidth;
            BottomLeftMapCoord.x -= 1;
            for (int i = playMatWidth - 1; i < tiles.Count; i += playMatWidth)
            {
                tiles[i].transform.position -= new Vector3(tileWidth * playMatWidth, 0, 0);
                GameObject movedTile = tiles[i];
                tiles.Insert(i - playMatWidth + 1, movedTile);
                tiles.RemoveAt(i + 1);
                //Shift the renderer cache as well
                rendererCache.Insert(i - playMatWidth + 1, rendererCache[i]);
                rendererCache.RemoveAt(i + 1);
                movedTile.GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x, BottomLeftMapCoord.y + (i / playMatWidth));
                changedTiles.Add(movedTile.GetComponent<TileData>().gridPos);
            }
        }
        //Same deal, but with the top/bottom rows
        //Small difference is that the rows could be missaligned if our playmat is an odd number of tiles high, so we need to account for that
        if (direction.y == 1)
        {
            int startIndex = (playMatHeight - 1) * playMatWidth;
            float additionalOffsetX = 0;
            playmat.bOddR = !playmat.bOddR; //Flip the oddR state, since we're moving the rows

            if (playMatHeight % 2 == 1)
            {
                additionalOffsetX = tiles[playMatWidth].transform.position.x - tiles[0].transform.position.x;
                if (additionalOffsetX < 0)
                {
                    additionalOffsetX = rowOffset.x;
                }
                else
                {
                    additionalOffsetX = -rowOffset.x;
                }
            }

            moveOffset.y -= rowOffset.y;
            BottomLeftMapCoord.y -= 1;
            for (int i = startIndex; i < tiles.Count; i++)
            {
                Vector2 newPosition = new Vector2(tiles[i].transform.position.x, tiles[i].transform.position.z - rowOffset.y * playMatHeight);
                if (playMatHeight % 2 == 1)
                {
                    newPosition.x += additionalOffsetX;
                }
                tiles[i].transform.position = new Vector3(newPosition.x, 0, newPosition.y);
                GameObject movedTile = tiles[i];
                tiles.Insert(i - startIndex, movedTile);
                tiles.RemoveAt(i + 1);
                //Shift the renderer cache as well
                rendererCache.Insert(i - startIndex, rendererCache[i]);
                rendererCache.RemoveAt(i + 1);
                movedTile.GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + (i - startIndex), BottomLeftMapCoord.y);
                changedTiles.Add(movedTile.GetComponent<TileData>().gridPos);

            }
        }
        else if (direction.y == -1)
        {
            int startIndex = 0;
            float additionalOffsetX = 0;
            playmat.bOddR = !playmat.bOddR; //Flip the oddR state, since we're moving the rows

            if (playMatHeight % 2 == 1)
            {
                additionalOffsetX = tiles[playMatWidth].transform.position.x - tiles[0].transform.position.x;
                if (additionalOffsetX < 0)
                {
                    additionalOffsetX = rowOffset.x;
                }
                else
                {
                    additionalOffsetX = -rowOffset.x;
                }
            }

            moveOffset.y += rowOffset.y;
            BottomLeftMapCoord.y += 1;
            for (int i = startIndex; i < playMatWidth; i++)
            {
                Vector2 newPosition = new Vector2(tiles[0].transform.position.x, tiles[0].transform.position.z + rowOffset.y * playMatHeight);
                if (playMatHeight % 2 == 1)
                {
                    newPosition.x += additionalOffsetX;
                }
                tiles[0].transform.position = new Vector3(newPosition.x, 0, newPosition.y);
                GameObject movedTile = tiles[0];
                tiles.Add(movedTile);
                tiles.RemoveAt(0);
                //Shift the renderer cache as well
                rendererCache.Add(rendererCache[0]);
                rendererCache.RemoveAt(0);
                movedTile.GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + i, BottomLeftMapCoord.y + playMatHeight - 1);
                changedTiles.Add(movedTile.GetComponent<TileData>().gridPos);
            }
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            //TODO: this is VERY heavy, get rid of this when not debugging
            tiles[i].GetComponent<TileData>().tileID = worldManager.world.GetCellID(worldManager.world.OddRToCube(BottomLeftMapCoord.x + (i % playMatWidth), BottomLeftMapCoord.y + (i / playMatWidth)));
            tiles[i].GetComponent<TileData>().playmatPos = playmat.OddRToCube(i % playMatWidth, i / playMatWidth);
            TilePacker tilePacker = worldManager.tilePacker;
            tilePacker.SetPackedTile(worldManager.world.GetCellData(worldManager.world.OddRToCube(BottomLeftMapCoord.x + (i % playMatWidth), BottomLeftMapCoord.y + (i / playMatWidth))));
            int[] biomes = new int[7];
            bool[] blends = new bool[6];
            bool[] sameIndex = new bool[6];
            for (int j = 0; j < 6; j++)
            {
                blends[j] = tilePacker.edgeBlends[j].isBlend;
                sameIndex[j] = tilePacker.edgeBlends[j].isSameIndex;
            }
            biomes = tilePacker.biomes;
            tiles[i].GetComponent<TileData>().unpacked = (int[])biomes.Clone();
            tiles[i].GetComponent<TileData>().blends = (bool[])blends.Clone();
            tiles[i].GetComponent<TileData>().sameIndex = (bool[])sameIndex.Clone();    
        }

        FetchTiles(changedTiles.ToArray());
    }


    private void FetchTiles(Vector3Int[] globalTileCoords) //Overload for specific tiles, better for scrolling
    {
        Vector3Int worldCoords = new Vector3Int();
        Material[] tileMats = worldManager.fetch(globalTileCoords);
        int playmatIndex = 0;
        TilePacker tilePacker = worldManager.tilePacker;

        for (int i = 0; i < globalTileCoords.Length; i++)
        {
            worldCoords = globalTileCoords[i];
            playmatIndex = playmat.CubeToIndex(WorldCoordsToPlaymatCoords(worldCoords));
            Renderer tileRenderer = null;
            if (playmatIndex >= 0 && playmatIndex < tiles.Count)
                tileRenderer = rendererCache[playmatIndex];
            if (tileRenderer == null)
                continue;
            Material[] sharedMats = tileRenderer.sharedMaterials;
            if (sharedMats == null)
                continue;

            if (tileMats[i * 7] == null)
            {
                tiles[playmatIndex].SetActive(false);
                matCache[playmatIndex] = null;
                continue;
            }
            else
            {
                tiles[playmatIndex].SetActive(true);
            }

            Vector3Int pos = playmat.IndexToCube(playmatIndex);
            TileWrapper.SetCoreMaterial(tileMats[i * 7], sharedMats); //Main
            HexData hexData = worldManager.world.GetCellData(worldCoords);
            tilePacker.SetPackedTile(hexData.ID, hexData.cornerFlags); 


            for (int j = 1; j < 7; j++)
            {
                TileWrapper.SetEdge(tileMats[i * 7 + j], sharedMats, j - 1, -1); //Edges A-F, whole edge
            }
            for (int j = 0; j < 6; j++)
            {

                if (tilePacker.edgeBlends[j].isBlend)
                {
                    if (!tilePacker.edgeBlends[j].isSameIndex) // (true, false)
                    {
                        TileWrapper.SetEdge(tileMats[i * 7 + 1 + ((j + 1) % 6)], sharedMats, j, 1); //Blend second corner with next edge's material
                    }
                    else //(true, true)
                    {
                        TileWrapper.SetEdge(tileMats[i * 7 + 1 + j], sharedMats, j, 1); //Blend second corner with same edge's material

                    }
                }
            }

            tileRenderer.sharedMaterials = sharedMats;
            matCache[playmatIndex] = sharedMats;
        }
        
    }
    Vector3Int WorldCoordsToPlaymatCoords(Vector3Int worldCoords)
    {
        BottomLeftCubeCoord = worldManager.world.OddRToCube(BottomLeftMapCoord.x, BottomLeftMapCoord.y);
        Vector3Int delta = new Vector3Int(worldCoords.x - BottomLeftCubeCoord.x, worldCoords.y - BottomLeftCubeCoord.y, worldCoords.z - BottomLeftCubeCoord.z);
        return delta;
    }
}
