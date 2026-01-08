using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Analytics;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

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

    //Set edge material, optionally set a specific corner material
    public static void SetEdge(Material mat, Material[] mats, int EdgeIndex, int CornerIndex = -1)
    {
        if (EdgeIndex < 0 || EdgeIndex >= 6)
            return;
        if (CornerIndex > 1 || CornerIndex < -1)
            return;

        //If the corner index is -1, we can assume we want the material across the whole edge
        if (CornerIndex != -1)
        {
            mats[cornerIndicies[EdgeIndex][CornerIndex]] = mat;
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
    public float tileWidth = 1.0f;
    public float tileHeight = 1.1547f;
    public Vector2 rowOffset = new Vector2(-0.5f, 0.8660254f);
    public Vector2 moveOffset = new Vector2(0, 0);
    private Vector2Int playmatSize;
    public Vector2Int BottomLeftMapCoord = new Vector2Int(0, 0);
    private int BottomLeftMapIndex;
    public Vector3Int BottomLeftCubeCoord = new Vector3Int(0, 0, 0);
    public WorldManager worldManager;
    private HexGrid playmat;
    private Vector3Int[] neighborTilesTable;
    private List<Renderer> rendererCache;
    private Material[][] matCache;
    public int RenderCacheSize;
    public Vector3Int DebugTile = new Vector3Int(-1, -1, -1);
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
        playmat.oddR = false;


        Vector2 firstTileOffset;
        float x = centerPosition.x - (tileWidth * playMatSize.x) / 2 + tileWidth / 2;
        float y = centerPosition.y - (rowOffset.y * playMatSize.y) / 2 + rowOffset.y / 2;
        firstTileOffset = new Vector2(x, y);


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

                tiles[tileIndex].GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + col, BottomLeftMapCoord.y + row);
                tiles[tileIndex].GetComponent<TileData>().playmatPos = playmat.OddRToCube(col, row);
                tileIndex++;
            }
        }
        neighborTilesTable = new Vector3Int[playmatSize.x * playmatSize.y * 6];
        matCache = new Material[playmatSize.x * playmatSize.y][];
        FetchTiles();
    }

    //Move tiles from offscreen to onscreen
    public void shift(Vector2 direction)
    {
        //NEW ADDITION: We're going to reorder the array of renderers, so we don't have to keep refreshing the cache.
        int playMatWidth = playmatSize.x;
        int playMatHeight = playmatSize.y;
        if (direction.x == -1) //Move the tile from the left edge to the right edge
        {
            //Take the first tile, and every playMatSize.x tile after that, and move it tileWidth * playMatSize.x to the right
            //Then, we'll reorganize the list to reflect the new order
         
            for (int i = 0; i < tiles.Count; i += playMatWidth)
            {
                tiles[i].transform.position += new Vector3(tileWidth * playMatWidth, 0, 0);
                GameObject movedTile = tiles[i];
                tiles.Insert((playMatWidth + i), movedTile);
                tiles.RemoveAt(i);
                //Shift the renderer cache as well
                rendererCache.Insert((playMatWidth + i), rendererCache[i]);
                rendererCache.RemoveAt(i);
            }
            moveOffset.x += tileWidth;
            BottomLeftMapCoord.x += 1;

        }
        else if (direction.x == 1)
        {
            for (int i = playMatWidth - 1; i < tiles.Count; i += playMatWidth)
            {
                tiles[i].transform.position -= new Vector3(tileWidth * playMatWidth, 0, 0);
                GameObject movedTile = tiles[i];
                tiles.Insert(i - playMatWidth + 1, movedTile);
                tiles.RemoveAt(i + 1);
                //Shift the renderer cache as well
                rendererCache.Insert(i - playMatWidth + 1, rendererCache[i]);
                rendererCache.RemoveAt(i + 1);
            }
            moveOffset.x -= tileWidth;
            BottomLeftMapCoord.x -= 1;
        }
        //Same deal, but with the top/bottom rows
        //Small difference is that the rows could be missaligned if our playmat is an odd number of tiles high, so we need to account for that
        if (direction.y == 1)
        {
            int startIndex = (playMatHeight - 1) * playMatWidth;
            float additionalOffsetX = 0;
            playmat.oddR = !playmat.oddR; //Flip the oddR state, since we're moving the rows

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

            }
            moveOffset.y -= rowOffset.y;
            BottomLeftMapCoord.y -= 1;
        }
        else if (direction.y == -1)
        {
            int startIndex = 0;
            float additionalOffsetX = 0;
            playmat.oddR = !playmat.oddR; //Flip the oddR state, since we're moving the rows

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
            }
            moveOffset.y += rowOffset.y;
            BottomLeftMapCoord.y += 1;
        }
        for (int i = 0; i < tiles.Count; i++)
        {
            tiles[i].GetComponent<TileData>().gridPos = worldManager.world.OddRToCube(BottomLeftMapCoord.x + (i % playMatWidth), BottomLeftMapCoord.y + (i / playMatWidth));
            tiles[i].GetComponent<TileData>().playmatPos = playmat.OddRToCube(i % playMatWidth, i / playMatWidth);
        }

        FetchTiles();
    }

    private void FetchTiles()
    {
        Material[] tileMats = worldManager.fetch(BottomLeftMapCoord, playmatSize);
        bool debugMode = worldManager.debugMode;

        for (int i = 0; i < tiles.Count; i++)
        {
            int matIndex = i * 7;
            if (tileMats[matIndex] == null)
            {
                tiles[i].SetActive(false);
                matCache[i] = null;
                continue;
            }
            else
            {
                tiles[i].SetActive(true);
                if (debugMode)
                {
                    //Create text to show the cell value
                    GameObject textObj = tiles[i].transform.Find("CellValueText")?.gameObject;
                    if (textObj == null)
                    {
                        textObj = new GameObject("CellValueText");
                        textObj.transform.SetParent(tiles[i].transform);
                        textObj.transform.localPosition = new Vector3(0, 0.1f, 0);
                        textObj.transform.localRotation = Quaternion.Euler(0, 0, 0);
                        TextMesh textMesh = textObj.AddComponent<TextMesh>();
                        textMesh.characterSize = 1.0f;
                        textMesh.anchor = TextAnchor.MiddleCenter;
                        textMesh.color = Color.white;
                    }
                    TextMesh existingTextMesh = textObj.GetComponent<TextMesh>();
                    existingTextMesh.fontSize = 1;
                    int cellX = BottomLeftMapCoord.x + (i % playmatSize.x);
                    int cellY = BottomLeftMapCoord.y + (i / playmatSize.x);
                    Vector3Int vector3Int = worldManager.world.OddRToCube(cellY, cellX);
                    existingTextMesh.text = vector3Int.ToString();
                }
                else
                {
                    Transform textTransform = tiles[i].transform.Find("CellValueText");
                    if (textTransform != null)
                    {
                        Destroy(textTransform.gameObject);
                    }
                }
            }
            Renderer tileRenderer = rendererCache[i];
            Material[] sharedMats = tileRenderer.sharedMaterials;

            Vector3Int pos = playmat.IndexToCube(i);

            TileWrapper.SetCoreMaterial(tileMats[matIndex], sharedMats); //Main
            TileWrapper.SetEdge(tileMats[matIndex + 1], sharedMats, 0); //Edge A
            TileWrapper.SetEdge(tileMats[matIndex + 2], sharedMats, 1); //Edge B
            TileWrapper.SetEdge(tileMats[matIndex + 3], sharedMats, 2); //Edge C
            TileWrapper.SetEdge(tileMats[matIndex + 4], sharedMats, 3); //Edge D
            TileWrapper.SetEdge(tileMats[matIndex + 5], sharedMats, 4); //Edge E
            TileWrapper.SetEdge(tileMats[matIndex + 6], sharedMats, 5); //Edge F
            tileRenderer.sharedMaterials = sharedMats;
            matCache[i] = sharedMats;
        }
        RenderCacheSize = rendererCache.Count;

        Vector2Int cellRowCol = new Vector2Int(0, 0);
        Vector3Int cellCoord = new Vector3Int(0, 0, 0);
        Vector3Int[] neighborCoords = new Vector3Int[6];
        for (int i = 0; i < tiles.Count; i++)
        {
            cellRowCol.x = BottomLeftMapCoord.x + (i % playmatSize.x);
            cellRowCol.y = BottomLeftMapCoord.y + (i / playmatSize.x);
            cellCoord = worldManager.world.OddRToCube(cellRowCol.x, cellRowCol.y);
            worldManager.world.GetNeighbourCoords(cellCoord, ref neighborCoords);
            neighborTilesTable[i * 6 + 0] = neighborCoords[0]; //NE
            neighborTilesTable[i * 6 + 1] = neighborCoords[1]; //E
            neighborTilesTable[i * 6 + 2] = neighborCoords[2]; //SE
            neighborTilesTable[i * 6 + 3] = neighborCoords[3]; //SW
            neighborTilesTable[i * 6 + 4] = neighborCoords[4]; //W
            neighborTilesTable[i * 6 + 5] = neighborCoords[5]; //NW
        }

        //Second loop, blend the tiles. For example, if Tile A is up and the the right of B, and the C edge is beach on A, and the B edge is beach on B, we can set the corner of the edge between them to beach as well, so the beach is continuous.
        for (int i = 0; i < tiles.Count; i++)
        {
            int neighbor1Index = 5; //The top left neighbor, we'll need to handle wrapping
            int neighbor2Index = 1; //The right neighbor
            int neighbor1CheckIndex = 1; //The edge index to check on neighbor 1
            int neighbor2CheckIndex = 5; //The edge index to check on neighbor 2


            Renderer tileRenderer = rendererCache[i];
            if (tileRenderer == null)
                continue;
            Material[] tileMatsCurrent = matCache[i];
            if (tileMatsCurrent == null)
                continue;
            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                Vector3Int neighborCoord = neighborTilesTable[i * 6 + neighbor1Index]; //The coord of the neighbour in the world
                Vector3Int neighborOneLocal = new Vector3Int(-1, -1, -1);
                if (!(neighborCoord.x == -1 && neighborCoord.y == -1 && neighborCoord.z == -1))
                    neighborOneLocal = WorldCoordsToPlaymatCoords(neighborCoord);

                neighborCoord = neighborTilesTable[i * 6 + neighbor2Index];
                Vector3Int neighborTwoLocal = new Vector3Int(-1, -1, -1);
                if (!(neighborCoord.x == -1 && neighborCoord.y == -1 && neighborCoord.z == -1))
                    neighborTwoLocal = WorldCoordsToPlaymatCoords(neighborCoord);




                //For each edge, I basically need to check the neighbor to each side of it, instead of straight across. The NE edge, for example, needs to check the E and NW neighbors. This is very specific, and might just be easiest to do a switch case, but we'll try with logic for now
                //This is tricky, because I need the actual tile next to us, not just the data of said tile, because I need to compare materials.

                Renderer neighbor1Tile = null;
                Renderer neighbor2Tile = null;
                int neighbor1LocalIndex = 0; //Index in the matCache and the rendererCache
                int neighbor2LocalIndex = 0; //Index in the matCache and the rendererCache
                if (neighborOneLocal != new Vector3Int(-1, -1, -1)) //Invalid coord
                {
                    neighbor1LocalIndex = playmat.OddRToIndex(neighborOneLocal);
                }
                if (neighborTwoLocal != new Vector3Int(-1, -1, -1))
                {
                    neighbor2LocalIndex = playmat.OddRToIndex(neighborTwoLocal);
                }
                if (playmat.IsValidCoordinate(neighborOneLocal))
                    neighbor1Tile = rendererCache[neighbor1LocalIndex];
                if (playmat.IsValidCoordinate(neighborTwoLocal))
                    neighbor2Tile = rendererCache[neighbor2LocalIndex];
                //A little gross, but I can add a GetTileAtCoord function later to the TilePool to make this cleaner
                Material currentEdgeMat = TileWrapper.GetEdge(edgeIndex, tileMatsCurrent);
                if (neighbor1Tile != null)
                {
                    Material neighbor1EdgeMat = TileWrapper.GetEdge(neighbor1CheckIndex, matCache[neighbor1LocalIndex]);
                    if (neighbor1EdgeMat == currentEdgeMat)
                    {
                        //Set corner material
                        TileWrapper.SetEdge(currentEdgeMat, tileMatsCurrent, neighbor1Index, 1); //Corner on current tile
                    }
                }
                if (neighbor2Tile != null)
                {
                    Material neighbor2EdgeMat = TileWrapper.GetEdge(neighbor2CheckIndex, matCache[neighbor2LocalIndex]);
                    if (neighbor2EdgeMat != null && neighbor2EdgeMat == currentEdgeMat)
                    {
                        //Set corner material
                        TileWrapper.SetEdge(currentEdgeMat, tileMatsCurrent, neighbor2Index, 0); //Corner on current tile
                    }
                }
                //Increment
                neighbor1Index = (neighbor1Index + 1) % 6;
                neighbor2Index = (neighbor2Index + 1) % 6;
                neighbor1CheckIndex = (neighbor1CheckIndex + 1) % 6;
                neighbor2CheckIndex = (neighbor2CheckIndex + 1) % 6;
            }
            tileRenderer.sharedMaterials = tileMatsCurrent;
        }
    }
    Vector3Int WorldCoordsToPlaymatCoords(Vector3Int worldCoords)
    {
        BottomLeftCubeCoord = worldManager.world.OddRToCube(BottomLeftMapCoord.x, BottomLeftMapCoord.y);
        Vector3Int delta = new Vector3Int(worldCoords.x - BottomLeftCubeCoord.x, worldCoords.y - BottomLeftCubeCoord.y, worldCoords.z - BottomLeftCubeCoord.z);
        return delta;

    }
}
