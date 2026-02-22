using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using UnityEngine;
using UnityEngine.UIElements;


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
    public int[] biomes; //Basically a way to easily look at the biomes present in a tile so I can debug
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
    private Material[] viewBuffer = null;
    public int mountainDivisor = 10; //Higher number means less mountains
    public Vector2Int MinMaxMountainStrength = new Vector2Int(3, 8); //a mountain spawner pushes other mountains up by a random amount in this range
    public int lakeDensity = 5; //Higher number means more lakes

    //Direction Stuff
    /*
     * Edge     Corner      Corner Offset       PrimeAxis
     * A        AB          (-0.5, -0.5, 0.5)   3
     * B        BC          (-0.5, 0.5, 0.5)    -1
     * C        CD          (-0.5, 0.5, -0.5)   2
     * D        DE          (0.5, 0.5, -0.5)    -3
     * E        EF          (0.5, -0.5, -0.5)   1
     * F        FA          (0.5, -0.5, 0.5)    -2
     * 
     * To convert from PrimeAxis to a direction vector;
     * vector = [0, 0, 0]
     * vector[abs(PrimeAxis) - 1] = Math.sign(PrimeAxis)
     */

    private Vector3[] CornerOffsets = new Vector3[6] {
        new Vector3(-0.5f, -0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, 0.5f),
        new Vector3(-0.5f, 0.5f, -0.5f),
        new Vector3(0.5f, 0.5f, -0.5f),
        new Vector3(0.5f, -0.5f, -0.5f),
        new Vector3(0.5f, -0.5f, 0.5f)
    };

    private int[] PrimeAxis = new int[6] {
        3,
        -1,
        2,
        -3,
        1,
        -2
    };



    public bool debugMode = false;
    public TilePacker tilePacker;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }

    public void Init()
    {
        world = new HexGrid(worldSize.x, worldSize.y);
        tilePacker = new TilePacker(new int[7], matList.Count(), new (bool, bool)[7]);
        GenerateWorld();
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void GenerateWorld()
    {

        UnityEngine.Random.InitState(12345);
        //temperary, just make each tile a grass tile for now
        //In future these will be a lot more complex, for now we'll just hard code.
        //Water = 0, Grass = 1

        world = HexCloudGen.GenHexCloud(world, tilePacker);
        //Now we clean up the coast line, adding in beaches and shallows
        //If 4 or more adjacent tiles are different biomes, convert the tile to the border biome
        world = CleanCoastline(world);

        //Next we add forests, as well as any other surface biomes that can be sort of carved through by mountains and Valleys
        world = AddForest(world);

        world = AddMountains(world);

        //Apply the edge cases
        world = ApplyEdges(world);

        world = Blend(world); //Blending pass for the corners, all biomes.

        world = AddRiversLakes(world);

        world = Blend(world, coreBlackList: new[] { BiomeType.Water }, edgeWhiteList: new[] {BiomeType.Water});

    }

    public void Regen()
    {
        Init();
    }

    //Replaces water and grass tiles that are mostly surrouded with tiles of the other type. This makes coastlines feel a bit softer.
    private HexGrid CleanCoastline(HexGrid grid)
    {
        Vector3Int[] neighbors = new Vector3Int[6];
        Vector3Int cubeCoord = new Vector3Int();
        TileConfig tileConfig = new TileConfig();
        tileConfig.biomes = new int[matList.Count];
        for (int cellIndex = 0; cellIndex < grid.GetCellCount(); cellIndex++)
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
                                    grid.SetCellBin(cubeCoord, tilePacker.packedTile, 0);
                                    break;
                            }
                            break;
                        case (int)BiomeType.Grass: //Grass
                            switch (b)
                            {
                                case (int)BiomeType.Water: //Water
                                    grid.SetCell(cubeCoord, (int)BiomeType.Beach); //Beach
                                    tilePacker.SetAllBiomes((int)BiomeType.Beach);
                                    grid.SetCellBin(cubeCoord, tilePacker.packedTile, 0);
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
        HexGrid forestCloud = HexCloudGen.GenHexCloud(new HexGrid(grid.GetDimensions().x, grid.GetDimensions().y), tilePacker, 12, 20);
        Vector3Int cellCoord = new Vector3Int();
        for (int cellIndex = 0; cellIndex < grid.GetCellCount(); cellIndex++)
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
                    grid.SetCellBin(cellCoord, tilePacker.packedTile, tilePacker.cornerBlends);
                }
            }
        }
        for (int cellIndex = 0; (cellIndex < grid.GetCellCount()); cellIndex++)
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
                        grid.SetCellBin(cellCoord, tilePacker.packedTile, tilePacker.cornerBlends);
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

            for (int cellIndex = 0; (cellIndex < grid.GetCellCount()); cellIndex++)
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
                            grid.SetCellBin(cellCoord, tilePacker.packedTile, tilePacker.cornerBlends);
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
        for (int i = 0; i < grid.GetCellCount(); i++)
        {
            Vector3Int cellCoord = grid.IndexToCube(i);
            //ulong packedTile = grid.(cellCoord);
            HexData tile = grid.GetCellData(cellCoord);
            //ulong packedTile = tile.ID;
            //ushort cornerFlags = tile.cornerFlags;
            tilePacker.SetPackedTile(tile);
            cell = tilePacker.biomes;

            //Now we do the edge replacements
            BiomeType biomeType = (BiomeType)cell[0];
            if (biomeType != BiomeType.Desert) //Skip the desert for now
            {
                world.GetNeighbourCoords(cellCoord, ref neighbours);
                for (int n = 0; n < neighbours.Length; n++)
                {
                    Vector3Int nCoord = neighbours[n];
                    //ulong nPackedTile = world.GetCellBin(nCoord);
                    HexData nTile = world.GetCellData(nCoord);
                    //tilePacker.SetPackedTile(nPackedTile);
                    tilePacker.SetPackedTile(nTile);
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
                                    case BiomeType.Water:
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
                            case BiomeType.DeepForest:
                                switch (nBiomeType)
                                {
                                    case BiomeType.Water:
                                        cell[n + 1] = (int)BiomeType.Forest;
                                        break;
                                }
                                break;
                        }
                    }
                }
            }
            tilePacker.SetBiomes(cell);
            //grid.SetCellBin(cellCoord, tilePacker.packedTile);
            grid.SetCellBin(cellCoord, tilePacker.packedTile, tilePacker.cornerBlends);
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

    private HexGrid AddRiversLakes(HexGrid grid)
    {
        //Start by picking random places to put lakes. These will be clusters of water tiles surrounded by land.
        //We'll aim for x lakes per 1000 tiles, so lakeDensity controls how many lakes we try to spawn
        int lakeSpawns = Mathf.FloorToInt(world.GetCellCount() / 1000) * lakeDensity;
        List<int> viableLakeSpotsBuffer = new List<int>();
        HexGrid riverLakesLayer = new HexGrid(grid.GetDimensions().x, grid.GetDimensions().y);
        Vector3Int[] neighbors = new Vector3Int[6];

        //We'll convert the grid to a more usable format for this. If we add more ground types that can be a lake, we need to update here.
        Vector3Int cellCoord = new Vector3Int();
        for (int cellIndex = 0; cellIndex < grid.GetCellCount(); cellIndex++)
        {
            cellCoord = grid.IndexToCube(cellIndex);
            BiomeType cellValue = (BiomeType)grid.GetCell(cellCoord);
            if (cellValue == BiomeType.Grass || cellValue == BiomeType.Forest || cellValue == BiomeType.LightForest || cellValue == BiomeType.DeepForest)
            {
                //NeighborCheck, lakes shouldn't spawn on coasts
                grid.GetNeighbourCoords(cellCoord, ref neighbors);
                bool adjacentToWater = false;
                for (int n = 0; n < neighbors.Length; n++)
                {
                    Vector3Int nCoord = neighbors[n];
                    BiomeType nCellValue = (BiomeType)grid.GetCell(nCoord);
                    if (nCellValue == BiomeType.Water || nCellValue == BiomeType.Beach || nCellValue == BiomeType.Shallows)
                    {
                        adjacentToWater = true;
                        break;
                    }
                }
                if (!adjacentToWater)
                {
                    riverLakesLayer.SetCell(cellCoord, 1); //Land
                    viableLakeSpotsBuffer.Add(cellIndex);
                }
                else
                {
                    riverLakesLayer.SetCell(cellCoord, 0); //Water or other
                }

            }
            else
            {
                riverLakesLayer.SetCell(cellCoord, 0); //Water or other
            }
        }
        //Now we spawn the lakes, we'll set them to 2 in the riverLakesLayer
        for (int i = 0; i < lakeSpawns; i++)
        {
            if (viableLakeSpotsBuffer.Count == 0)
                break; //No more viable spots
            int randomIndex = UnityEngine.Random.Range(0, viableLakeSpotsBuffer.Count);
            int lakeCellIndex = viableLakeSpotsBuffer[randomIndex];
            cellCoord = grid.IndexToCube(lakeCellIndex);
            riverLakesLayer.SetCell(cellCoord, 2); //Lake
            viableLakeSpotsBuffer.RemoveAt(randomIndex);

            //We generate a list of spots we can expand to, and this list will control if/when the lake has grown to the extents.
            List<Vector3Int> expansionSpots = new List<Vector3Int>();
            riverLakesLayer.GetNeighbourCoords(cellCoord, ref neighbors);
            for (int n = 0; n < neighbors.Length; n++)
            {
                Vector3Int nCoord = neighbors[n];
                int nCellValue = riverLakesLayer.GetCell(nCoord);
                if (nCellValue == 1) //Land, can expand into
                {
                    expansionSpots.Add(nCoord);
                }
            }

            //Lets expand it a bit by looking up the neighbors and seeing if they're allowed, to a max of lakeSpawns, because it seems like a decent limit
            for (int n = 0; n < lakeSpawns; n++)
            {
                if (expansionSpots.Count == 0)
                    break;
                if (UnityEngine.Random.Range(0f, 1f) < 0.1f) //10% chance to stop expanding each iteration
                    break;


                riverLakesLayer.GetNeighbourCoords(cellCoord, ref neighbors);
                int randomExpansionIndex = UnityEngine.Random.Range(0, expansionSpots.Count);
                Vector3Int expandCoord = expansionSpots[randomExpansionIndex];
                riverLakesLayer.SetCell(expandCoord, 2); //Lake
                while (expansionSpots.Contains(expandCoord))
                    expansionSpots.Remove(expandCoord);// A bit gross, but it's during load. If we need to we can optimize later
                viableLakeSpotsBuffer.Remove(grid.CubeToIndex(expandCoord));

                //Add new expansion spots
                riverLakesLayer.GetNeighbourCoords(expandCoord, ref neighbors);
                for (int nn = 0; nn < neighbors.Length; nn++)
                {
                    Vector3Int nnCoord = neighbors[nn];
                    int nnCellValue = riverLakesLayer.GetCell(nnCoord);
                    if (nnCellValue == 1) //Land, can expand into
                    {
                        //Gives a natural boost to tiles that have been added to the list multiple times, less likely to form strange shapes while still giving islands a chance to form
                        expansionSpots.Add(nnCoord);
                    }
                }
            }


            //Remove the expansion points from the potential lake spots, because they'd spawn overlapping other lakes.
            for (int n = 0; n < expansionSpots.Count; n++)
            {
                Vector3Int lCoord = expansionSpots[n];
                int lCellIndex = grid.CubeToIndex(lCoord);
                if (viableLakeSpotsBuffer.Contains(lCellIndex))
                {
                    viableLakeSpotsBuffer.Remove(lCellIndex);
                }
            }
        }


        //Now for Rivers. We'll start my making a list of river spawn locations. These are lakes that are adjacent to land tiles.
        List<Vector3Int> RiverSpawnLocations = new List<Vector3Int>();
        for (int lakeIndex = 0; lakeIndex < riverLakesLayer.GetCellCount(); lakeIndex++)
        {
            cellCoord = riverLakesLayer.IndexToCube(lakeIndex);
            int cellValue = riverLakesLayer.GetCell(cellCoord);
            if (cellValue == 2) //Lake
            {
                //Check neighbors for land
                riverLakesLayer.GetNeighbourCoords(cellCoord, ref neighbors);
                for (int n = 0; n < neighbors.Length; n++)
                {
                    Vector3Int nCoord = neighbors[n];
                    int nCellValue = riverLakesLayer.GetCell(nCoord);
                    if (nCellValue == 1) //Land
                    {
                        RiverSpawnLocations.Add(cellCoord);
                        break;
                    }
                }
            }
        }


        //Now we'll start spawning rivers.
        Vector3Int riverCarver = new Vector3Int(); //Basically the spot that the river is currently at. It'll travel along the edges of biomes until it stops.
        int cornerIndex = 0;

        for (int i = 0; i < lakeSpawns; i++)//We'll, again, try to spawn as many rivers as lakes
        {
            if (RiverSpawnLocations.Count == 0)
                break; //No more viable river spawns
            int randomIndex = UnityEngine.Random.Range(0, RiverSpawnLocations.Count);
            Vector3Int lakeCoord = RiverSpawnLocations[randomIndex];
            RiverSpawnLocations.RemoveAt(randomIndex);
            //Pick a random edge to start the river from
            riverLakesLayer.GetNeighbourCoords(lakeCoord, ref neighbors);
            List<int> viableCorners = new List<int>();
            for (int n = 0; n < neighbors.Length; n++)
            {
                Vector3Int nCoord = neighbors[n];
                Vector3Int n2Coord = neighbors[(n + 1) % neighbors.Length];
                int nCellValue = riverLakesLayer.GetCell(nCoord);
                int n2CellValue = riverLakesLayer.GetCell(n2Coord);
                if (nCellValue == 1 && n2CellValue == 1) //Land
                {
                    viableCorners.Add(n);
                }
            }
            if (viableCorners.Count == 0)
            {
                RiverSpawnLocations.Remove(lakeCoord);
                continue; //No viable corners
            }

            int randomCornerIndex = UnityEngine.Random.Range(0, viableCorners.Count); //The corners are the furthest point clockwise of the edge.
            cornerIndex = viableCorners[randomCornerIndex];
            List<Vector3Int> riverPath = new List<Vector3Int>(); //Keep track of the river.
            List<int> riverPathCorners = new List<int>(); //Keep track of the corners used.

            Vector3 direction = new Vector3();
            //Get the prime axis
            direction[Math.Abs(PrimeAxis[cornerIndex]) - 1] = Mathf.Sign(PrimeAxis[cornerIndex]);
            //Set one of the other two axis to a value between 0 and -direction[Math.Abs(PrimeAxis[cornerIndex] - 1)]
            int otherAxis = UnityEngine.Random.Range(0, 3);
            if (otherAxis == Math.Abs(PrimeAxis[cornerIndex] - 1))
                otherAxis = (otherAxis + 1) % 3; //Not ideal, but probably fine for now
            direction[otherAxis] = UnityEngine.Random.Range(0f, -(direction[Math.Abs(PrimeAxis[cornerIndex]) - 1]));



            riverCarver = lakeCoord;
            riverPath.Clear();
            riverPathCorners.Clear();
            riverPath.Add(riverCarver);
            riverPathCorners.Add(cornerIndex);

            bool riverActive = true;

            while (riverActive)
            {
                //We should Only be in here if the two tiles are valid for carving a river through.
                //Get the neighbors of the river carver, which will be cornerIndex and cornerIndex + 1
                grid.GetNeighbourCoords(riverCarver, ref neighbors);
                Vector3Int nACoord = neighbors[cornerIndex];
                Vector3Int nBCoord = neighbors[(cornerIndex + 1) % neighbors.Length];
                riverActive = false;

                Vector3 HomeCornerPos = riverCarver + CornerOffsets[cornerIndex];
                Vector3[] nACorners = new Vector3[6];
                Vector3[] nBCorners = new Vector3[6];

                for (int c = 0; c < 6; c++)
                {
                    nACorners[c] = nACoord + CornerOffsets[c];
                    nBCorners[c] = nBCoord + CornerOffsets[c];
                }

                Vector3[] nACornerDirs = new Vector3[6];
                Vector3[] nBCornerDirs = new Vector3[6];

                for (int c = 0; c < 6; c++)
                {
                    nACornerDirs[c] = (nACorners[c] - HomeCornerPos);
                    nBCornerDirs[c] = (nBCorners[c] - HomeCornerPos);
                }

                float[] Similarities = new float[12]; // All 12 corners, 6 from each neighbor, so we can do a random choice between the percentage matches

                for (int c = 0; c < 6; c++)
                {
                    Similarities[c] = Vector3.Dot(direction.normalized, nACornerDirs[c].normalized) - 0.05f; //A small reduction to add randomization 
                    if (Similarities[c] < 0)
                        Similarities[c] = 0;
                    Similarities[c + 6] = Vector3.Dot(direction.normalized, nBCornerDirs[c].normalized) - 0.05f;
                    if (Similarities[c + 6] < 0)
                        Similarities[c + 6] = 0;
                }

                float totalSimilarity = 0f;
                for (int s = 0; s < Similarities.Length; s++)
                {
                    totalSimilarity += Similarities[s];
                }

                float roll = UnityEngine.Random.Range(0f, totalSimilarity);
                float runningTotal = 0f;
                Vector3Int homeTile = riverCarver;
                //int homeCorner = cornerIndex;
                int enteranceCorner = cornerIndex; //The corner that the river leaves the current tile from

                //Kinda Gross, if we want to optimise, look here
                for (int s = 0; s < Similarities.Length; s++)
                {
                    runningTotal += Similarities[s];
                    if (roll <= runningTotal)
                    {
                        //We have a winner
                        if (s < 6)
                        {
                            //Neighbor A
                            riverCarver = nACoord;
                            enteranceCorner = (cornerIndex + 2) % 6; //The river leaves the current tile 2 corners clockwise from where it entered
                            if (enteranceCorner < 0)
                                enteranceCorner += 6;
                            cornerIndex = s;
                        }
                        else
                        {
                            //Neighbor B
                            riverCarver = nBCoord;
                            enteranceCorner = (cornerIndex + 4) % 6; //The river leaves the current tile 2 corners counter-clockwise from where it entered
                            if (enteranceCorner < 0)
                                enteranceCorner += 6;
                            cornerIndex = s - 6;
                        }
                        if (!grid.IsValidCoordinate(riverCarver))
                        {
                            riverActive = false;
                            break;
                        }

                        //Connect the homeCorner to the offRampCorner by setting all the edges between them to water
                        int clockwiseDistance = (cornerIndex - enteranceCorner + 6) % 6;
                        int counterclockwiseDistance = (enteranceCorner - cornerIndex + 6) % 6;

                        //Determine the faster direction
                        bool clockwise = true;
                        if (counterclockwiseDistance < clockwiseDistance)
                        {
                            clockwise = false;
                        }
                        else if (counterclockwiseDistance == clockwiseDistance)
                        {
                            if (UnityEngine.Random.value < 0.5f)
                                clockwise = false;
                        }

                        //Temporarily force all rivers to be clockwise for testing
                        //clockwise = true;
                        //grid.GetNeighbourCoords(homeTile, ref neighbors);

                        //Carve the river on the home tile
                        if (clockwise)
                        {
                            int cIndex = enteranceCorner; //For testing, I'm swapping the home and offRamp corners
                            tilePacker.SetPackedTile(grid.GetCellData(riverCarver));
                            int[] biomes = (int[])tilePacker.biomes.Clone();
                            (bool, bool)[] cornerFlags = ((bool, bool)[])tilePacker.edgeBlends.Clone();
                            while (cIndex != cornerIndex) 
                            {
                                biomes[((cIndex + 1) % 6) + 1] = (int)BiomeType.Water; //Set the edge to water
                                cornerFlags[((cIndex + 1) % 6)] = (false, false);
                                cIndex = (cIndex + 1) % 6;
                            }
                            tilePacker.SetBiomes(biomes);
                            tilePacker.SetBlends(cornerFlags);
                            grid.SetCellBin(riverCarver, tilePacker.packedTile, tilePacker.cornerBlends);
                        }
                        else
                        {
                            int cIndex = enteranceCorner;
                            tilePacker.SetPackedTile(grid.GetCellData(riverCarver));
                            int[] biomes = (int[])tilePacker.biomes.Clone();
                            (bool, bool)[] cornerFlags = ((bool, bool)[])tilePacker.edgeBlends.Clone();
                            while (cIndex != cornerIndex)
                            {
                                biomes[((cIndex + 6) % 6) + 1] = (int)BiomeType.Water; //Set the edge to water
                                cornerFlags[((cIndex + 1) % 6)] = (false, false);
                                cIndex = (cIndex + 5) % 6;
                            }
                            tilePacker.SetBiomes(biomes);
                            tilePacker.SetBlends(cornerFlags);
                            grid.SetCellBin(riverCarver, tilePacker.packedTile, tilePacker.cornerBlends);
                        }
                        riverPath.Add(riverCarver);
                        riverPathCorners.Add(cornerIndex);
                        //Check if the new river carver position is valid
                        riverLakesLayer.GetNeighbourCoords(riverCarver, ref neighbors);
                        //If either of the two tiles the rivercarver sits between are water, we stop the river
                        Vector3Int checkNACoord = neighbors[cornerIndex];
                        Vector3Int checkNBCoord = neighbors[(cornerIndex + 1) % neighbors.Length];
                        int nACellValue = riverLakesLayer.GetCell(checkNACoord);
                        int nBCellValue = riverLakesLayer.GetCell(checkNBCoord);

                        if (nACellValue == 0 || nBCellValue == 0) //Water or other
                        {
                            riverActive = false;
                            break;
                        }
                        else if (UnityEngine.Random.value > 0.95)
                        {
                            riverActive = false; //5% chance to stop the river each step
                            break;
                        }
                        else
                        {
                            riverActive = true;
                        }
                        break;
                    }
                }
            }
            //for (int r = 0; r < riverPath.Count; r++)
            //{
            //    Debug.Log("River Tile: " + riverPath[r] + " via corner " + riverPathCorners[r]);
            //}

            //riverActive = false; //Test to see if it works so far
        }


        //For the time being, lets see what this looks like. Combine the layers, anything labled as 2 becomes a water tile in the main grid
        for (int cellIndex = 0; cellIndex < grid.GetCellCount(); cellIndex++)
        {
            cellCoord = grid.IndexToCube(cellIndex);
            int lakeCellValue = riverLakesLayer.GetCell(cellCoord);
            if (lakeCellValue == 2) //Lake
            {
                grid.SetCell(cellCoord, (int)BiomeType.Water);
                tilePacker.SetAllBiomes((int)BiomeType.Water);
                grid.SetCellBin(cellCoord, tilePacker.packedTile, tilePacker.cornerBlends);
            }
        }


        return grid;
    }

    //A one and done version of the blending logic, so I'm not repeating code everywhere.
    //We'll use one or the other list, not both. If there's no whitelist, we allow all except what's in the blacklist.
    private HexGrid Blend(HexGrid grid, BiomeType[] coreBlackList = null, BiomeType[] edgeBlackList = null, BiomeType[] coreWhiteList = null, BiomeType[] edgeWhiteList = null)
    {
        for (int cellIndex = 0; cellIndex < grid.GetCellCount(); cellIndex++)
        {
            Vector3Int coords = grid.IndexToCube(cellIndex);
            tilePacker.SetPackedTile(grid.GetCellData(coords));
            int[] mainBiomes = (int[])tilePacker.biomes.Clone();
            Vector3Int bpCoords = new Vector3Int(-11, -68, 79);
            int bp = 0;
            if (coords == bpCoords)
            {
                bp = 1;
            }


            //White & Blacklisting
            if (coreWhiteList == null)
            {
                if (coreBlackList != null)
                {
                    if (coreBlackList.Contains((BiomeType)mainBiomes[0]))
                    {
                        continue;
                    }
                }
            }
            //Otherwise, if both are null, we allow everything.
            else
            {
                if (coreWhiteList.Length != 0)
                {
                    if (coreBlackList != null)
                    {
                        if (coreBlackList.Contains((BiomeType)mainBiomes[0]) || !coreWhiteList.Contains((BiomeType)mainBiomes[0]))
                        {
                            continue;
                        }
                    }
                    else if (!coreWhiteList.Contains((BiomeType)mainBiomes[0]))
                    {
                        continue;
                    }
                }
            }

            Vector3Int[] neighborCoords = new Vector3Int[6];

            int cellVal = grid.GetCell(coords);
            if (cellVal == -1)
                continue;

            grid.GetNeighbourCoords(coords, ref neighborCoords);


            int neighbor1Index = 5; //The top left neighbor, we'll need to handle wrapping
            int neighbor2Index = 1; //The right neighbor
            int neighbor1CheckIndex = 1; //The edge index to check on neighbor 1
            int neighbor2CheckIndex = 5; //The edge index to check on neighbor 2

            (bool blend, bool sameIndex)[] mainCornerBlends = ((bool blend, bool sameIndex)[])tilePacker.edgeBlends.Clone();

            for (int edgeIndex = 0; edgeIndex < 6; edgeIndex++)
            {
                if (edgeIndex == 4)
                    bp = 2;
                //White & Blacklisting
                bool pass = false;
                if (edgeWhiteList == null)
                {
                    if (edgeBlackList != null)
                    {
                        if (edgeBlackList.Contains((BiomeType)mainBiomes[edgeIndex + 1]))
                        {
                            pass = true;
                        }
                    }
                }
                //Otherwise, if both are null, we allow everything.
                else
                {
                    if (edgeWhiteList.Length != 0)
                    {
                        if (edgeBlackList != null)
                        {
                            if (edgeBlackList.Contains((BiomeType)mainBiomes[edgeIndex + 1]) || !edgeWhiteList.Contains((BiomeType)mainBiomes[edgeIndex + 1]))
                            {
                                pass = true;
                            }
                        }
                        else if (!edgeWhiteList.Contains((BiomeType)mainBiomes[edgeIndex + 1]))
                        {
                            pass = true;
                        }
                    }
                }

                if (pass)
                {
                    neighbor1Index = (neighbor1Index + 1) % 6;
                    neighbor2Index = (neighbor2Index + 1) % 6;
                    neighbor1CheckIndex = (neighbor1CheckIndex + 1) % 6;
                    neighbor2CheckIndex = (neighbor2CheckIndex + 1) % 6;
                    continue;
                }

                Vector3Int neighborCoord = neighborCoords[neighbor1Index]; //The coord of the neighbour in the world
                HexData hexData = grid.GetCellData(neighborCoord);
                int[] neighbor1Biomes = new int[7];
                ulong neighborTileID = hexData.ID;
                if (neighborTileID != ulong.MaxValue)
                {
                    tilePacker.SetPackedTile(hexData);
                    neighbor1Biomes = (int[])tilePacker.biomes.Clone();
                }
                else
                    neighbor1Biomes = null;

                neighborCoord = neighborCoords[neighbor2Index];
                hexData = grid.GetCellData(neighborCoord);
                int[] neighbor2Biomes = new int[7];
                neighborTileID = hexData.ID;
                if (neighborTileID != ulong.MaxValue)
                {
                    tilePacker.SetPackedTile(hexData);
                    neighbor2Biomes = (int[])tilePacker.biomes.Clone();
                }
                else
                    neighbor2Biomes = null;

                bool areConnectedEdgesTheSame = true; //If the two edges are the same, and we're meant to blend, don't bother.
                int edgeCheck = (edgeIndex + 3) % 6;
                int neighborCheckIndex = edgeIndex;
                neighborCoord = neighborCoords[neighborCheckIndex];
                int[] neighborBiomes = new int[7];
                //hexData = grid.GetCellData(neighborCoord);
                if (hexData.ID != ulong.MaxValue)
                {
                    tilePacker.SetPackedTile(hexData);
                    neighborBiomes = (int[])tilePacker.biomes.Clone();
                }
                else
                    neighborBiomes = null;



                if (neighborBiomes != null)
                    areConnectedEdgesTheSame = (mainBiomes[edgeIndex + 1] == neighborBiomes[edgeCheck + 1]); //Have to add one because of the core
                areConnectedEdgesTheSame = false;
                if (neighbor1Biomes != null)
                {
                    /* If:
                    * Edge Biome == Neighbor1 Edge check Biome, OR Edge Biome == Neighbor1 Edge check Biome + 1
                    * AND the connected edges aren't the same
                    * AND the two edges sharing the corner aren't the same biome.
                    */
                    if (((mainBiomes[edgeIndex + 1] == neighbor1Biomes[neighbor1CheckIndex + 1]) || mainBiomes[edgeIndex + 1] == neighbor1Biomes[((neighbor1CheckIndex + 1) % 6) + 1]) && !areConnectedEdgesTheSame && (mainBiomes[edgeIndex + 1] != mainBiomes[(edgeIndex + 5) % 6]))
                    {
                        mainCornerBlends[neighbor1Index] = (true, false); //blend the left corner. We need to blend a different index because it's technically assigned to a different index, but it blends with OUR index.
                    }
                }
                if (neighbor2Biomes != null)
                {
                    /* If:
                    * Edge Biome == Neighbor2 Edge check Biome, OR Edge Biome == Neighbor2 Edge check Biome - 1
                    * AND the connected edges aren't the same
                    * AND the two edges sharing the corner aren't the same biome.
                    */
                    if (((mainBiomes[edgeIndex + 1] == neighbor2Biomes[neighbor2CheckIndex + 1]) || mainBiomes[edgeIndex + 1] == neighbor2Biomes[((neighbor2CheckIndex + 5) % 6) + 1]) && !areConnectedEdgesTheSame && (mainBiomes[edgeIndex + 1] != mainBiomes[(edgeIndex + 1) % 6 + 1]))
                    {
                        mainCornerBlends[edgeIndex] = (true, true); //blend the right corner. (For my own peace of mind, (true, false) is translates to (true = yes blend, false = same index as edge)
                    }
                }

                //Increment
                neighbor1Index = (neighbor1Index + 1) % 6;
                neighbor2Index = (neighbor2Index + 1) % 6;
                neighbor1CheckIndex = (neighbor1CheckIndex + 1) % 6;
                neighbor2CheckIndex = (neighbor2CheckIndex + 1) % 6;

            }

            //Update the tile with the new corner blends
            tilePacker.SetBlends(mainCornerBlends);
            tilePacker.SetBiomes(mainBiomes);
            grid.SetCellBin(coords, tilePacker.packedTile, tilePacker.cornerBlends);
        }
        return grid;
    }

    public Material[] fetch(Vector3Int[] coords) //Fetch by list of coordinates, lighter for scrolling.
    {
        if (viewBuffer == null || viewBuffer.Length != coords.Length * 7)
        {
            viewBuffer = new Material[coords.Length * 7];
        }

        HexData packedTile = new HexData();
        int baseMatIndex = 0;
        for (int i = 0; i < coords.Length; i++)
        {
            baseMatIndex = i * 7;
            packedTile = world.GetCellData(coords[i]);
            if (packedTile.ID == ulong.MaxValue)
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
