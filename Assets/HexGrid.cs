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
    public HexGrid(int width, int height, bool wide = true) //TODO: remember what wide was for?
    {
        if (width < 1 || height < 1)
            throw new System.ArgumentException("Width and height must be at least 1");

        this.width = width;
        this.height = height;
        this.cells = new int[width * height];
    }
    public int GetCell(int x, int y)
    {
        if (!IsValidCoordinate(x, y))
            return -1;
        else
            return cells[y * width + x];
    }
    public int GetCell(Vector2Int coord)
    {
        if (!IsValidCoordinate(coord.x, coord.y))
            return -1;
        else
            return cells[coord.y * width + coord.x];
    }
    public void SetCell(int x, int y, int value)
    {
        if (IsValidCoordinate(x, y))
            cells[y * width + x] = value;
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }
    public void SetCell(Vector2Int coord, int value)
    {
        if (IsValidCoordinate(coord.x, coord.y))
            cells[coord.y * width + coord.x] = value;
        else
            throw new System.IndexOutOfRangeException("Invalid hex grid coordinates");
    }
    private bool IsValidCoordinate(int x, int y)
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }
    public int size()
    {
        return width * height;
    }
    public Vector2Int dimensions()
    {
        return new Vector2Int(width, height);
    }

    public void GetNeighbourCoords(Vector2Int coords, ref Vector2Int[] neighbors)
    {
        //-1, -1 for out of bounds
        int x = coords.x;
        int y = coords.y;
        bool isOddRow = (y % 2) == 1;
        neighbors[1].x = x + 1; neighbors[1].y = y; //E
        neighbors[0].x = x + (isOddRow ? 1 : 0); neighbors[0].y = y + 1; //NE
        neighbors[5].x = x + (isOddRow ? 0 : -1); neighbors[5].y = y + 1; //NW
        neighbors[4].x = x - 1; neighbors[4].y = y; //W
        neighbors[3].x = x + (isOddRow ? 0 : -1); neighbors[3].y = y - 1; //SW
        neighbors[2].x = x + (isOddRow ? 1 : 0); neighbors[2].y = y - 1; //SE
        for (int i = 0; i < neighbors.Length; i++)
        {
            if (!IsValidCoordinate(neighbors[i].x, neighbors[i].y))
            {
                neighbors[i].x = -1;
                neighbors[i].y = -1;
            }
        }
    }

}
