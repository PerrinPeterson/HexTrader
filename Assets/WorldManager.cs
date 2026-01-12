using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.TerrainTools;
using UnityEngine;


public enum BiomeType
{
    Water = 0,
    Grass = 1,
    Desert = 2,
    Beach = 3,
    Shallows = 4,
    Forest = 5,
    LightForest = 6,
    DeepForest = 7,
    Hills = 8,
    Moutains = 9,
    TallMountains = 10,
}
public struct TileConfig
{
    public int[] biomes; //Basically a way to easily look at the biomes present in a tile so I can decide to shift the core
    public int defaultBiome;
}


public class WorldManager : MonoBehaviour
{
    //Materials List Indexing:
    //0 = Water Base
    //1 = Grass Base
    //2 = Desert Base
    //3 = Beach
    //4 = Shallows
    //5 = Forest
    //6 = Light Forest
    //7 = Deep Forest
    //8 = Hills
    //9 = Mountains
    //10 = Tall Mountains



    public Vector2Int worldSize = new Vector2Int(50, 50);
    public HexGrid world;
    public List<Material> matList = new List<Material>();
    public Material DevMat;
    private HexCloudGen cloudGen;
    private Material[] viewBuffer = null;
    public int mountainDivisor = 10; //Higher number means less mountains
    public Vector2Int MinMaxMountainStrength = new Vector2Int(3, 8); //a mountain spawner pushes other mountains up by a random amount in this range

    public bool debugMode = false;
    public TilePacker tilePacker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    public void Init()
    {
        world = new HexGrid(worldSize.x, worldSize.y);
        cloudGen = transform.GetComponent<HexCloudGen>();
        tilePacker = new TilePacker(new int[7], matList.Count());
        GenerateWorld();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void GenerateWorld()
    {
        //temperary, just make each tile a grass tile for now
        //In future these will be a lot more complex, for now we'll just hard code.
        //Water = 0, Grass = 1

        world = cloudGen.GenHexCloud(world, tilePacker);
        //Now we clean up the coast line, adding in beaches and shallows
        //If 4 or more adjacent tiles are different biomes, convert the tile to the border biome
        world = CleanCoastline(world);

        //Next we add forests, as well as any other surface biomes that can be sort of carved through by mountains and Valleys
        world = AddForest(world);

        world = AddMountains(world);

        //Apply the edge cases
        world = ApplyEdges(world);

        // world = CleanEdges(world); //Final pass to apply edge blending between biomes
    }

    public void Regen()
    {
        Init();
    }

    private HexGrid CleanCoastline(HexGrid grid)
    {
        Vector3Int[] neighbors = new Vector3Int[6];
        Vector3Int cubeCoord = new Vector3Int();
        TileConfig tileConfig = new TileConfig();
        tileConfig.biomes = new int[matList.Count];
        for (int cellIndex = 0; cellIndex < grid.size(); cellIndex++)
        {
            cubeCoord = grid.IndexToCube(cellIndex);
            int cell = grid.GetCell(cubeCoord);


            if (cell == -1)
                continue; //Invalid cell
            grid.GetNeighbourCoords(cubeCoord, ref neighbors);

            Array.Clear(tileConfig.biomes, 0, tileConfig.biomes.Length);
            tileConfig.defaultBiome = cell;
            tileConfig.biomes[cell] = 6; //Start with 6 counts for the default biome
            for (int n = 0; n < neighbors.Length; n++)
            {
                Vector3Int nCoord = neighbors[n];
                int nCellValue = grid.GetCell(nCoord);
                if (nCellValue != cell && nCellValue != -1)
                {
                    tileConfig.biomes[nCellValue] += 1;
                    tileConfig.biomes[tileConfig.defaultBiome] -= 1;
                }
            }
            //Stack the biomes that count towards the same weight (For example, Forest and Deep Forest both contribute to making something a deep forest)
            tileConfig.biomes[(int)BiomeType.Forest] += tileConfig.biomes[(int)BiomeType.DeepForest]; //Forest gets Deep Forest counts
            //Now check if any biome has more than 4 counts, and if it is, convert to the border tile type.
            for (int b = 0; b < tileConfig.biomes.Length; b++)
            {
                if (tileConfig.biomes[b] >= 4)
                {
                    //Convert to border tile
                    switch (cell)
                    {
                        case (int)BiomeType.Water: //Water
                            switch (b)
                            {
                                case (int)BiomeType.Grass: //Grass
                                    grid.SetCell(cubeCoord, (int)BiomeType.Shallows); //Shallows
                                    tilePacker.SetAllBiomes((int)BiomeType.Shallows);
                                    grid.SetCellBin(cubeCoord, tilePacker.packedTile);
                                    break;
                            }
                            break;
                        case (int)BiomeType.Grass: //Grass
                            switch (b)
                            {
                                case (int)BiomeType.Water: //Water
                                    grid.SetCell(cubeCoord, (int)BiomeType.Beach); //Beach
                                    tilePacker.SetAllBiomes((int)BiomeType.Beach);
                                    grid.SetCellBin(cubeCoord, tilePacker.packedTile);
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        return grid;
    }

    private HexGrid AddForest(HexGrid grid)
    {
        //We'll use another cloud gen for this, but with some random pressures and weights, and then we'll use the main grid as a mask to only add forests to grass tiles
        HexGrid forestCloud = cloudGen.GenHexCloud(new HexGrid(grid.dimensions().x, grid.dimensions().y), tilePacker, false, 12, 20);
        Vector3Int cellCoord = new Vector3Int();
        for (int cellIndex = 0; cellIndex < grid.size(); cellIndex++)
        {
            cellCoord = grid.IndexToCube(cellIndex);
            BiomeType cellValue = (BiomeType)grid.GetCell(cellCoord);
            float forestValue = forestCloud.GetCell(cellCoord);
            if (cellValue == BiomeType.Grass) //Only add forests to grass tiles for now
            {
                if (forestValue > 0.5f) //Threshold for forest density
                {
                    //Convert to forest tile
                    grid.SetCell(cellCoord, (int)BiomeType.Forest); //Assuming 5 is the forest tile index
                    tilePacker.SetAllBiomes((int)BiomeType.Forest);
                    grid.SetCellBin(cellCoord, tilePacker.packedTile);
                }
            }
        }
        for (int cellIndex = 0; (cellIndex < grid.size()); cellIndex++)
        {
            cellCoord = grid.IndexToCube(cellIndex);
            BiomeType cellValue = (BiomeType)grid.GetCell(cellCoord);
            if (cellValue == BiomeType.Forest) //Forest tile
            {
                Vector3Int[] neighbors = new Vector3Int[6];
                grid.GetNeighbourCoords(cellCoord, ref neighbors);
                for (int n = 0; n < neighbors.Length; n++)
                {
                    Vector3Int nCoord = neighbors[n];
                    BiomeType nCellValue = (BiomeType)grid.GetCell(nCoord);
                    if (nCellValue == BiomeType.Beach || nCellValue == BiomeType.Shallows || nCellValue == BiomeType.Water)
                    {
                        //Convert to light forest
                        grid.SetCell(cellCoord, (int)BiomeType.LightForest); //Assuming 6 is the light forest tile index
                        tilePacker.SetAllBiomes((int)BiomeType.LightForest);
                        grid.SetCellBin(cellCoord, tilePacker.packedTile);
                        break;
                    }
                }
            }
        }

        //Last Pass, any forest tile surrounded on all sides by Forest or Deep Forest has a chance to become Deep Forest, we'll do this 6 times, but each time we'll increase the random chance for deep forest conversion based on the number of adjacent deep forests
        //ie; Pass 1 = 5% chance for any forest tile surrounded by forest to become deep forest
        //Pass 2 = 4% chance + 5% per adjacent deep forest
        //Pass 3 = 3% chance + 8% per adjacent deep forest
        //Pass 4 = 2% chance + 10% per adjacent deep forest
        //Pass 5 = 1% chance + 15% per adjacent deep forest
        //Pass 6 = 0% chance + 20% per adjacent deep forest, meaning any forest tile surrounded by deep forest becomes deep forest

        for (int pass = 0; pass < 6; pass++)
        {
            int baseChance = Mathf.Max(10 - pass * 2, 0); //Decreasing base chance each pass
            int perDeepForestChance = Mathf.RoundToInt(0 + Mathf.Pow(pass, 1.75f)); //Increasing chance per adjacent deep forest each pass

            for (int cellIndex = 0; (cellIndex < grid.size()); cellIndex++)
            {
                cellCoord = grid.IndexToCube(cellIndex);
                BiomeType cellValue = (BiomeType)grid.GetCell(cellCoord);
                if (cellValue == BiomeType.Forest)
                {
                    //Check neighbours to make sure they are all forest or deep forest
                    //This prevents edge forests from becoming deep forests naturally. This is more realistic, as deep forests tend to be in the interior of large forested areas
                    Vector3Int[] neighbors = new Vector3Int[6];
                    grid.GetNeighbourCoords(cellCoord, ref neighbors);
                    bool allForest = true;
                    int adjacentDeepForests = 0;
                    for (int n = 0; n < neighbors.Length; n++)
                    {
                        Vector3Int nCoord = neighbors[n];
                        BiomeType nCellValue = (BiomeType)grid.GetCell(nCoord);
                        if (nCellValue != BiomeType.Forest && nCellValue != BiomeType.DeepForest) //Not Forest, or Deep Forest
                        {
                            allForest = false;
                            break;
                        }
                        if (nCellValue == BiomeType.DeepForest)
                        {
                            adjacentDeepForests += 1;
                        }
                    }
                    if (allForest)
                    {
                        int chance = baseChance + (adjacentDeepForests * perDeepForestChance);
                        int roll = UnityEngine.Random.Range(0, 100);
                        if (roll < chance)
                        {
                            //Convert to deep forest
                            grid.SetCell(cellCoord, (int)BiomeType.DeepForest);
                            tilePacker.SetAllBiomes((int)BiomeType.DeepForest);
                            grid.SetCellBin(cellCoord, tilePacker.packedTile);
                        }
                    }
                }
            }
        }

        return grid;
    }

    private HexGrid ApplyEdges(HexGrid grid)
    {
        int[] cell = new int[7]; //Base biome + 6 edges
        Vector3Int[] neighbours = new Vector3Int[6];
        //The replacement for what we do in the fetch function, but done at world gen time instead of every frame
        for (int i = 0; i < grid.size(); i++)
        {
            Vector3Int cellCoord = grid.IndexToCube(i);
            ulong packedTile = grid.GetCellBin(cellCoord);
            tilePacker.SetPackedTile(packedTile);
            cell = tilePacker.biomes;

            //Now we do the edge replacements
            BiomeType biomeType = (BiomeType)cell[0];
            if (biomeType != BiomeType.Desert) //Skip the desert for now
            {
                world.GetNeighbourCoords(cellCoord, ref neighbours);
                for (int n = 0; n < neighbours.Length; n++)
                {
                    Vector3Int nCoord = neighbours[n];
                    ulong nPackedTile = world.GetCellBin(nCoord);
                    tilePacker.SetPackedTile(nPackedTile);
                    int nCellValue = tilePacker.biomes[0];
                    if (nCellValue == -1)
                        continue;
                    BiomeType nBiomeType = (BiomeType)nCellValue;
                    if (nBiomeType != biomeType)
                    {
                        //These are the edge cases, and where we add more edges as the world gets more biomes
                        switch (biomeType)
                        {
                            case BiomeType.Water:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Grass:
                                        cell[n + 1] = (int)BiomeType.Shallows;
                                        break;
                                    case BiomeType.LightForest:
                                        cell[n + 1] = (int)BiomeType.Shallows;
                                        break;
                                    case BiomeType.Beach:
                                        cell[n + 1] = (int)BiomeType.Shallows;
                                        break;
                                }
                                break;
                            case BiomeType.Grass:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Water:
                                        cell[n + 1] = (int)BiomeType.Beach;
                                        break;
                                    case BiomeType.Beach:
                                        cell[n + 1] = (int)BiomeType.Beach;
                                        break;
                                }
                                break;
                            case BiomeType.Beach:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Water:
                                        cell[n + 1] = (int)BiomeType.Shallows;
                                        break;
                                    case BiomeType.Shallows:
                                        cell[n + 1] = (int)BiomeType.Shallows;
                                        break;
                                }
                                break;
                            case BiomeType.Shallows:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Grass:
                                        cell[n + 1] = (int)BiomeType.Beach;
                                        break;
                                }
                                break;
                            case BiomeType.Forest:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Grass:
                                        cell[n + 1] = (int)BiomeType.LightForest;
                                        break;
                                }
                                break;
                            case BiomeType.LightForest:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Water:
                                        cell[n + 1] = (int)BiomeType.Beach;
                                        break;
                                    case BiomeType.Grass:
                                        cell[n + 1] = (int)BiomeType.Grass;
                                        break;
                                    case BiomeType.Shallows:
                                        cell[n + 1] = (int)BiomeType.Beach;
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
            tilePacker.SetBiomes(cell);
            grid.SetCellBin(cellCoord, tilePacker.packedTile);
        }
        return grid;
    }

    private HexGrid AddMountains(HexGrid grid)
    {
        //going to be a fun one. So the big thing with mountain ranges is they tend to be more or less a line than a blob.
        //So lets try that. we'll pick a random point on land, we'll pick a random direction vector, and we'll just give the mountain range a "strength". this strength will decrease over time, but in a nutshell, it'll push mountains in each direction
        //and pass on a portion of it's strength to the next tile. This strength will be increased based on the direction, so the mountain range will tend to go in a line, but can have some variation.
        
        
        
        
        return grid;
    }
    public Material[] fetch(Vector2Int bottomLeft, Vector2Int size)
    {
        if (viewBuffer == null || viewBuffer.Length != size.x * size.y * 7)
        {
            viewBuffer = new Material[size.x * size.y * 7];
        }
        int cellValue = -1;
        Vector3Int cellCoord = new Vector3Int();
        Vector3Int[] neighbours = new Vector3Int[6];
        int baseMatIndex = 0;
        for (int y = bottomLeft.y; y < bottomLeft.y + size.y; y++)
        {
            for (int x = bottomLeft.x; x < bottomLeft.x + size.x; x++)
            {
                cellCoord = world.OddRToCube(x, y);
                cellValue = world.GetCell(cellCoord);
                //int cellValue = world.GetCell(x, y);
                baseMatIndex = ((y - bottomLeft.y) * size.x + (x - bottomLeft.x)) * 7;
                //For each tile, the base material is first, then the 6 edge materials clockwise starting from NE
                //Material[] mats = new Material[7];
                if (cellValue == -1)
                {
                    //Invalid cell
                    for (int i = 0; i < 7; i++)
                    {
                        viewBuffer[baseMatIndex + i] = null;
                    }
                    continue;
                }
                for (int i = 0; i < 7; i++)
                {
                    viewBuffer[baseMatIndex + i] = matList[cellValue]; //Default the whole tile to the base material
                }

                //TODO: Do this in world gen instead, so We don't have to do these lookups every time the tile materials are fetched
                //Lookup border tiles. Then we can set the edge materials accordingly
                BiomeType biomeType = (BiomeType)cellValue;
                if (biomeType != BiomeType.Desert) //Skip the desert for now
                {
                    world.GetNeighbourCoords(cellCoord, ref neighbours);
                    for (int n = 0; n < neighbours.Length; n++)
                    {
                        Vector3Int nCoord = neighbours[n];
                        int nCellValue = world.GetCell(nCoord);
                        if (nCellValue == -1)
                            continue;
                        BiomeType nBiomeType = (BiomeType)nCellValue;
                        if (nBiomeType != biomeType)
                        {
                            //These are the edge cases, and where we add more edges as the world gets more biomes
                            switch (biomeType)
                            {
                                case BiomeType.Water:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Grass:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Shallows];
                                            break;
                                        case BiomeType.LightForest:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Shallows];
                                            break;
                                        case BiomeType.Beach:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Shallows];
                                            break;
                                    }
                                    break;
                                case BiomeType.Grass:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Water:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Beach];
                                            break;
                                        case BiomeType.Beach:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Beach];
                                            break;
                                    }
                                    break;
                                case BiomeType.Beach:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Water:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Shallows];
                                            break;
                                        case BiomeType.Shallows:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Shallows];
                                            break;
                                    }
                                    break;
                                case BiomeType.Shallows:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Grass:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Beach];
                                            break;
                                    }
                                    break;
                                case BiomeType.Forest:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Grass:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.LightForest];
                                            break;
                                    }
                                    break;
                                case BiomeType.LightForest:
                                    switch (nBiomeType)
                                    {
                                        case BiomeType.Water:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Beach];
                                            break;
                                        case BiomeType.Grass:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Grass];
                                            break;
                                        case BiomeType.Shallows:
                                            viewBuffer[baseMatIndex + n + 1] = matList[(int)BiomeType.Beach];
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }
        return viewBuffer;
    }

    public Material[] fetch(Vector3Int[] coords) //Fetch by list of coordinates, lighter for scrolling.
    {
        if (viewBuffer == null || viewBuffer.Length != coords.Length * 7)
        {
            viewBuffer = new Material[coords.Length * 7];
        }

        ulong packedTile = 0;
        int baseMatIndex = 0;
        for (int i = 0; i < coords.Length; i++)
        {
            baseMatIndex = i * 7;
            packedTile = world.GetCellBin(coords[i]);
            if (packedTile == ulong.MaxValue)
            {
                //Invalid cell
                for (int j = 0; j < 7; j++)
                {
                    viewBuffer[baseMatIndex + j] = null;
                }
                continue;
            }
            tilePacker.SetPackedTile(packedTile);

            for (int j = 0; j < 7; j++)
            {
                viewBuffer[baseMatIndex + j] = matList[tilePacker.biomes[j]];
            }
        }
        return viewBuffer;
    }


}
