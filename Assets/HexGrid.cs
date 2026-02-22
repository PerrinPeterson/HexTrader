using UnityEngine;

/* Created Perrin Peterson, Copyright 2025
 * 
 * Classes and structs for world storage. Stores the tiles in packed formats using ulongs and ushorts.
 * Handles math for using a cubic coordinate system, as well as odd-r offset coordinates, and converting between them.
 * 
 */
public enum HexDirection //An easier way to convert directions to indices
{
    NE,
    E,
    SE,
    SW,
    W,
    NW
}

public struct HexData
{
    public ulong ID;
    public ushort cornerFlags; //2 flags per corner, 1 for if we're blending, and the other for if the its blending with the same edge as the corner index, or the next edge clockwise.
    //Techically, according to some sources, Unity will auto align this to the nearest multiple of 8 bytes, so this struct will be 16 bytes in size.
    //This means we could have another ushort for more data without any extra memory cost, but for now this is fine.
}

public class HexGrid
{
    // Bottom Left is (0,0)
    private int width;
    private int height;
    private int[] cells;
    private HexData[] cellsData;
    public bool bOddR = true; //Whether the grid uses odd-r offset coordinates, currently only really used for checking if a coordinate is valid
    private static readonly Vector3Int[] neighborLookups = new Vector3Int[6] { //Lookup table for getting neighboring coordinates, in cube coordinates, starting from NE and going clockwise. These are the directions you move in cube space to get to each neighbor.
            new Vector3Int(0, -1, 1), //NE
            new Vector3Int(-1, 0, 1), //E
            new Vector3Int(-1, 1, 0), //SE
            new Vector3Int(0, 1, -1), //SW
            new Vector3Int(1, 0, -1), //W
            new Vector3Int(1, -1, 0)  //NW
    };

    public HexGrid(int width, int height)
    {
        if (width < 1 || height < 1)
            throw new System.ArgumentException("Width and height must be at least 1");

        this.width = width;
        this.height = height;
        //Int storage usable for simple biome markers. This only holds one biome index, so ideal for certain layers.
        this.cells = new int[width * height];
        //Data storage
        this.cellsData = new HexData[width * height];
    }

    public int GetCell(Vector3Int cube)
    {
        int cell = -1;
        //OddR system
        if (IsValidCoordinate(cube))
            cell = cells[CubeToIndex(cube)];
        return cell;
    }

    public void SetCell(Vector3Int cube, int value)
    {
        if (IsValidCoordinate(cube))
            cells[CubeToIndex(cube)] = value;
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }

    public HexData GetCellData(Vector3Int cube)
    {
        HexData cell = new HexData();
        cell.ID = ulong.MaxValue;
        cell.cornerFlags = ushort.MaxValue;
        //OddR system
        if (IsValidCoordinate(cube))
        {
            int index = CubeToIndex(cube);
            cell.ID = cellsData[index].ID;
            cell.cornerFlags = cellsData[index].cornerFlags;
        }
        return cell;
    }

    public void SetCellBin(Vector3Int cube, HexData data)
    {
        if (IsValidCoordinate(cube))
        {
            int index = CubeToIndex(cube);
            cellsData[index].ID = data.ID;
            cellsData[index].cornerFlags = data.cornerFlags;
        }
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }

    public void SetCellBin(Vector3Int cube, ulong ID, ushort cornerFlags)
    {
        if (IsValidCoordinate(cube))
        {
            int index = CubeToIndex(cube);
            cellsData[index].ID = ID;
            cellsData[index].cornerFlags = cornerFlags;
        }
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }

    public ulong GetCellID(Vector3Int cube)
    {
        ulong cellID = ulong.MaxValue;
        //OddR system
        if (IsValidCoordinate(cube))
            cellID = cellsData[CubeToIndex(cube)].ID;
        return cellID;
    }

    public bool IsValidCoordinate(Vector3Int cube)
    {
        //New OddR system effectively;
        //N/S is -Y/+Y
        //The NE/SW Corners are +Z/-Z
        //The NW/SE Corners are +X/-X
        //It's easier to picture it as moving from the CORNER of the hexes, each one pointing to one axis direction, but impossible to move purely along one axis.
        //This means, moving from tile to tile across the edges, you move in two axes at once, the two corners that make up that edge.
        //This makes it impossible to have an all positive grid, unfortunately.
        //But, a fancy thing we can do is:
        if (cube.x + cube.y + cube.z != 0)
            return false; //All cube coords must sum to 0 to be valid.
        int row = -cube.y;
        int parity = row & 1; //0 if even, 1 if odd

        if (row < 0 || row >= height)
            return false;
        if (cube.z < 0)
            return false; //No negative Z allowed, the grid only extends rightwards in Z
        int col = 0;
        if (bOddR)
            col = cube.z - (Mathf.CeilToInt(row / 2f)); //Convert cube to OddR col
        else
            col = cube.z - (Mathf.FloorToInt(row / 2f)); //Convert cube to EvenR col

        if (col < 0 || col >= width)
            return false;

        return true;
    }

    public int GetCellCount()
    {
        return width * height;
    }

    public Vector2Int GetDimensions()
    {
        return new Vector2Int(width, height);
    }

    //Outside these functions, vector3s will be fairly nonsencical, but it's a much faster and healthier way to represent hex grids internally.
    public void GetNeighbourCoords(Vector3Int coords, ref Vector3Int[] neighbors)
    {
        for (int i = 0; i < neighbors.Length; i++)
        {
            neighbors[i] = coords + neighborLookups[i];
            if (!IsValidCoordinate(neighbors[i]))
            {
                neighbors[i].x = -1;
                neighbors[i].y = -1;
                neighbors[i].z = -1;
            }
        }
    }

    public float GetDistance(Vector3Int a, Vector3Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2f;
    }

    public float GetDirection(Vector3Int a, Vector3Int b)
    {
        Vector3 direction = new Vector3(b.x - a.x, b.y - a.y, b.z - a.z);
        return Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
    }

    public float GetDistance(int a, int b) //Indices
    {
        Vector3Int aCoord = IndexToCube(a);
        Vector3Int bCoord = IndexToCube(b);
        return GetDistance(aCoord, bCoord);
    }

    public Vector3Int IndexToCube(int index)
    {
        (int row, int col) = IndexToOddR(index);
        return OddRToCube(col, row);
    }
    public (int row, int col) IndexToOddR(int index)
    {
        int row = index / width;
        int col = index % width;
        return (row, col);
    }

    public Vector3Int OddRToCube(int col, int row)
    {
        // https://www.redblobgames.com/grids/hex-grids/ (odd-r)
        // Pretty wild stuff, basically, we convert the offset coords to cube coords
        // The "Cube" coords are a 3D coordinate system, it's a bit strange to think about, and the website has a visual explanation that works wonders
        int z = 0;
        if (bOddR)
            z = col + Mathf.CeilToInt(row / 2f);
        else
            z = col + Mathf.FloorToInt(row / 2f);
        int y = -row;
        int x = -y - z;
        return new Vector3Int(x, y, z);
    }

    public int OddRToIndex(int col, int row)
    {
        return row * width + col;
    }

    public (int row, int col) CubeToOddR(Vector3Int cube)
    {
        int row = -cube.y;
        int col = 0;
        if (bOddR)
            col = cube.z - Mathf.CeilToInt(row / 2f);
        else
            col = cube.z - Mathf.FloorToInt(row / 2f);
        return (row, col);
    }

    public int CubeToIndex(Vector3Int cube)
    {
        (int row, int column) = CubeToOddR(cube);
        return row * width + column;
    }

}
