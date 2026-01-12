using UnityEngine;

enum HexDirection //An easier way to convert directions to indices
{
    NE,
    E,
    SE,
    SW,
    W,
    NW
}


public class HexGrid
{
    // Bottom Left is (0,0)
    private int width;
    private int height;
    //TODO: reprisent each hex in binary, for light storage and effeciency
    private int[] cells;
    private ulong[] cellsBin;
    public bool oddR = true; //Whether the grid uses odd-r offset coordinates, currently only really used for checking if a coordinate is valid
    private Vector3Int[] neighborLookups = new Vector3Int[6] {
            new Vector3Int(0, -1, 1), //NE
            new Vector3Int(-1, 0, 1), //E
            new Vector3Int(-1, 1, 0), //SE
            new Vector3Int(0, 1, -1), //SW
            new Vector3Int(1, 0, -1), //W
            new Vector3Int(1, -1, 0)  //NW
    };
    public HexGrid(int width, int height, bool wide = true) //TODO: remember what wide was for?
    {
        if (width < 1 || height < 1)
            throw new System.ArgumentException("Width and height must be at least 1");

        this.width = width;
        this.height = height;
        this.cells = new int[width * height];
        this.cellsBin = new ulong[width * height];
    }
    public int GetCell(Vector3Int coord)
    {
        int cell = -1;
        //OddR system
        if (IsValidCoordinate(coord))
            cell = cells[OddRToIndex(coord)];
        return cell;
    }

    public void SetCell(Vector3Int coord, int value)
    {
        if (IsValidCoordinate(coord))
            cells[OddRToIndex(coord)] = value;
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }
    public ulong GetCellBin(Vector3Int coord)
    {
        ulong cell = ulong.MaxValue;
        //OddR system
        if (IsValidCoordinate(coord))
            cell = cellsBin[OddRToIndex(coord)];
        return cell;
    }
    public void SetCellBin(Vector3Int coord, ulong value)
    {
        if (IsValidCoordinate(coord))
            cellsBin[OddRToIndex(coord)] = value;
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }

    public bool IsValidCoordinate(Vector3Int coord)
    {
        //New OddR system effectively;
        //N/S is -Y/+Y
        //The NE/SW Corners are +Z/-Z
        //The NW/SE Corners are +X/-X
        //It's easier to picture it as moving from the CORNER of the hexes, each one pointing to one axis direction, but impossible to move purely along one axis.
        //This means, moving from tile to tile across the edges, you move in two axes at once, the two corners that make up that edge.
        //This makes it impossible to have an all positive grid, unfortunately.
        //But, a fancy thing we can do is:
        if (coord.x + coord.y + coord.z != 0)
            return false; //All cube coords must sum to 0 to be valid.
        int row = -coord.y;
        int parity = row & 1; //0 if even, 1 if odd

        if (row < 0 || row >= height)
            return false;
        if (coord.z < 0)
            return false; //No negative Z allowed, the grid only extends rightwards in Z
        int col = 0;
        if (oddR)
            col = coord.z - (Mathf.CeilToInt(row / 2f)); //Convert cube to OddR col
        else
            col = coord.z - (Mathf.FloorToInt(row / 2f)); //Convert cube to EvenR col

        if (col < 0 || col >= width)
            return false;

        return true;
    }

    public int OddRToIndex(Vector3Int coord)
    {
        int row = -coord.y;
        int col = 0;
        if (oddR)
            col = coord.z - (Mathf.CeilToInt(row / 2f));
        else
            col = coord.z - (Mathf.FloorToInt(row / 2f));
        return row * width + col;

    }
    public int size()
    {
        return width * height;
    }
    public Vector2Int dimensions()
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
    public float GetDistance(int a, int b) //Indeces
    {
        Vector3Int aCoord = IndexToOddR(a);
        Vector3Int bCoord = IndexToOddR(b);
        return GetDistance(aCoord, bCoord);
    }

    public Vector3Int IndexToCube(int index)
    {
        //if (index == 39999)
        //    Debug.Log("Here");
        int row = index / width;
        int col = index % width;
        return OddRToCube(col, row);
    }
    public Vector3Int IndexToOddR(int index)
    {
        int row = index / width;
        int col = index % width;
        int z = 0;
        if (oddR)
            z = col + Mathf.CeilToInt(row / 2f);
        else
            z = col + Mathf.FloorToInt(row / 2f);
        int y = -row;
        int x = -y - z;
        return new Vector3Int(x, y, z);
    }
    public Vector3Int OddRToCube(int col, int row)
    {
        // https://www.redblobgames.com/grids/hex-grids/ (odd-r)
        // Pretty wild stuff, basically, we convert the offset coords to cube coords
        // The "Cube" coords are a 3D coordinate system, it's a bit strange to think about, and the website has a visual explanation that works wonders
        int z = 0;
        if (oddR)
            z = col + Mathf.CeilToInt(row / 2f);
        else
            z = col + Mathf.FloorToInt(row / 2f);
        int y = -row;
        int x = -y - z;
        return new Vector3Int(x, y, z);
    }


}
