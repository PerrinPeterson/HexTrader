using UnityEngine;
using System.Collections.Generic;

public class HexCloudGen : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public HexGrid GenHexCloud(HexGrid grid, bool continuous = false, int numLayers = 4, int radius = 0)
    {
        //Going to be a bit different than the cloud gen using a pixel based grid, but the idea is similar
        //Continuous will be implemented later
        if (radius <= 0)
        {
            radius = grid.dimensions().x / 10; //Default radius to 1/10 the grid width
        }

        List<float[]> layers = new List<float[]>();

        for (int i = 0; i < numLayers; i++)
        {
            layers.Add(new float[grid.size()]);
        }

        //TODO: Make a config struct or something so I can pass in parameters for each layer

        //Weights is used to multiply the final value of each layer, so each layer has a different impact on the final cloud
        float[] weights = new float[numLayers];
        //Pressures is used to determine how strong of a falloff function each layer has
        float[] pressures = new float[numLayers];
        //Each layer will also have a set of points that generate the cloud
        int[] points = new int[numLayers];

        for (int i = 0; i < layers.Count; i++)
        {
            weights[i] = 1 - (Mathf.Pow(i / (((float)(layers.Count) + 0.5f) / 2), 0.5f)); //This is WAY to complicated and will be replaced with manual values later, basically gives a falloff for the weights and also lets half the layers be negative
            pressures[i] = 1.0f + i / 10.0f; //Simple increasing pressure for now
            points[i] = (int)(Mathf.Round(grid.dimensions().x / 5 - (Mathf.Pow(i / numLayers, i) * 2))); //Again, WAY to complicated, just want less points for higher layers, but this is a fun way to do it. The first layer will actually have less points than the second layer
        }

        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            //Generate the random high density points for this layer
            Vector2Int[] highDensityPoints = new Vector2Int[points[layerIndex]];
            for (int pointIndex = 0; pointIndex < highDensityPoints.Length; pointIndex++)
            {
                highDensityPoints[pointIndex] = new Vector2Int(Random.Range(0, grid.dimensions().x), Random.Range(0, grid.dimensions().y));
            }
            //Now, for each cell in the grid, calculate its density based on distance to each high density point
            for (int x = 0; x < grid.dimensions().x; x++)
            {
                for (int y = 0; y < grid.dimensions().y; y++)
                {
                    Vector2Int cellCoord = new Vector2Int(x, y);
                    float cellDensity = 0.0f;
                    foreach (Vector2Int highPoint in highDensityPoints)
                    {
                        float distance = CalculateDistance(cellCoord, highPoint);
                        float falloffValue = Falloff(distance, radius, pressures[layerIndex]); //Using a radius of 1/10 the grid width for now
                        cellDensity += falloffValue;
                    }
                    layers[layerIndex][y * grid.dimensions().x + x] = cellDensity;
                }
            }
        }

        //Now combine the layers into the final grid
        for (int x = 0; x < grid.dimensions().x; x++)
        {
            for (int y = 0; y < grid.dimensions().y; y++)
            {
                float finalDensity = 0.0f;
                for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
                {
                    finalDensity += layers[layerIndex][y * grid.dimensions().x + x] * weights[layerIndex];
                }
                //Set the final density into the grid
                int storedValue = Mathf.RoundToInt(Mathf.Clamp(finalDensity, 0, 1)); //TODO: Revert this back to Clamp01, just for testing
                grid.SetCell(x, y, storedValue);
            }
        }

        return grid;

    }

    public float CalculateDistance(Vector2Int a, Vector2Int b)
    {
        //Using the cube coordinate system to calculate distance now, to try and solve the zipper artifact
        OddRToCube(a, out int ax, out int ay, out int az);
        OddRToCube(b, out int bx, out int by, out int bz);

        int dx = Mathf.Abs(ax - bx);
        int dy = Mathf.Abs(ay - by);
        int dz = Mathf.Abs(az - bz);

        // Hex distance in steps
        return (dx + dy + dz) * 0.5f;
    }

    public Vector3 calculateDirection(Vector2Int a, Vector2Int b)
    {
        //Calculate the direction from a to b in cube coordinates, then convert back to odd-r offset
        OddRToCube(a, out int ax, out int ay, out int az);
        OddRToCube(b, out int bx, out int by, out int bz);
        int dx = bx - ax;
        int dy = by - ay;
        int dz = bz - az;
        //Convert back to odd-r offset
        int col = ax + (az - (az & 1)) / 2 + dx;
        int row = az + dz;
        return new Vector3(col, 0, row);
    }

    private float Falloff(float distance, float radius, float pressure)
    {
        if (distance > 0) {
            return Mathf.Pow(Mathf.Clamp01(1 - (distance / radius)), pressure);
        }
        else
        {
            return 1.0f;
        }
    }

    // odd-r offset (pointy top) -> cube
    static void OddRToCube(Vector2Int o, out int x, out int y, out int z)
    {
        // https://www.redblobgames.com/grids/hex-grids/ (odd-r)
        // Pretty wild stuff, basically, we convert the offset coords to cube coords
        // The "Cube" coords are a 3D coordinate system, it's a bit strange to think about, and the website has a visual explanation that works wonders
        x = o.x - ((o.y - (o.y & 1)) / 2);
        z = o.y;
        y = -x - z;
    }
}
