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
            int[] highDensityPointIndices = new int[points[layerIndex]];
            for (int pointIndex = 0; pointIndex < highDensityPointIndices.Length; pointIndex++)
            {
                highDensityPointIndices[pointIndex] = Random.Range(0, grid.size());
            }
            //Now, for each cell in the grid, calculate its density based on distance to each high density point


            //For now I'll try just going over every index. Might even be faster than using Vector2Ints
            float cellDensity = 0.0f;
            for (int cellIndex = 0; cellIndex < grid.size(); cellIndex++)
            {
                cellDensity = 0.0f;
                foreach (int highPointIndex in highDensityPointIndices)
                {
                    float distance = grid.GetDistance(cellIndex, highPointIndex);
                    float falloffValue = Falloff(distance, radius, pressures[layerIndex]); //Using a radius of 1/10 the grid width for now
                    cellDensity += falloffValue;
                }
                layers[layerIndex][cellIndex] = cellDensity;
            }
        }

        for (int cellIndex = 0; cellIndex < grid.size(); cellIndex++)
        {
            float finalDensity = 0.0f;
            for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                finalDensity += layers[layerIndex][cellIndex] * weights[layerIndex];
            }
            //Set the final density into the grid
            int storedValue = Mathf.RoundToInt(Mathf.Clamp(finalDensity, 0, 1)); //TODO: Revert this back to Clamp01, just for testing
            grid.SetCell(grid.IndexToCube(cellIndex), storedValue);
        }

        return grid;

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
}
