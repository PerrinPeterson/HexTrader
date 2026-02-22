using UnityEngine;
using System.Collections.Generic;

/* Created Perrin Peterson, Copyright 2025
 * 
 * Static class that generates a basic cloud texture, but in a hexigonal grid.
 * Mainly used for world generation.
 * 
 */

public static class HexCloudGen
{
    public static HexGrid GenHexCloud(HexGrid grid, TilePacker packer, int layerCount = 4, int radiusInCells = 0)
    {
        float[] weights;
        if (radiusInCells <= 0)
        {
            radiusInCells = grid.GetDimensions().x / 10; //Default radius to 1/10 the grid width
        }
        int gridSize = grid.GetCellCount();

        GenerateLayers(out List<float[]> layers, out weights, layerCount, radiusInCells, grid, gridSize);
        BlendLayers(layers, weights, grid, gridSize, packer, layerCount);

        return grid;
    }

    //Blends all the layers together by their specific layer weight.
    private static void BlendLayers(List<float[]> layers, float[] weights, HexGrid grid, int gridSize, TilePacker packer, int layerCount)
    {
        Vector3Int cubic = new Vector3Int();
        for (int cellIndex = 0; cellIndex < gridSize; cellIndex++)
        {
            float finalDensity = ComputeFinalDensityOfCell(cellIndex, layers, weights, layerCount);
            //Set the final density into the grid
            cubic = grid.IndexToCube(cellIndex);
            int storedValue = Mathf.RoundToInt(Mathf.Clamp(finalDensity, 0, 1));
            grid.SetCell(cubic, storedValue); //TEMP: Useful for simple grids where we only need a single int per tile.
            packer.SetAllBiomes(storedValue);
            grid.SetCellBin(cubic, packer.packedTile, packer.cornerBlends);
        }
    }

    //Adds up the density at a single point by blending the density of each layer at that point by the layer's weight.
    private static float ComputeFinalDensityOfCell(int cellIndex, List<float[]> layers, float[] weights, int layerCount)
    {
        float finalDensity = 0.0f;
        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            finalDensity += layers[layerIndex][cellIndex] * weights[layerIndex];
        }
        return finalDensity;
    }

    private static void GenerateLayers(out List<float[]> layers, out float[] weights, int layerCount, int radiusInCells, HexGrid grid, int gridSize)
    {
        //Pressures is used to determine how strong of a falloff function each layer has
        float[] pressures;
        //Each layer will also have a set of points that generate the cloud
        int[] pointsPerLayer;

        GenerateLayerVariables(layerCount, out weights, out pressures, out pointsPerLayer, grid);

        layers = new List<float[]>();
        for (int i = 0; i < layerCount; i++)
        {
            layers.Add(new float[gridSize]);
        }
        for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
        {
            //Generate the random high density points for this layer
            int[] highDensityPointIndices = GenerateRandomPoints(pointsPerLayer[layerIndex], gridSize);
            //Now, for each cell in the grid, calculate its density based on distance to each high density point
            layers[layerIndex] = BlendPoints(gridSize, highDensityPointIndices, layers[layerIndex], pressures[layerIndex], radiusInCells, grid);
        }
    }

    //Generates the weights, pressures, and points for each layer. This uses specific curves adjusted to give a good worldgen feel.
    private static void GenerateLayerVariables(int layerCount, out float[] weights, out float[] pressures, out int[] pointsPerLayer, HexGrid grid)
    {
        weights = new float[layerCount];
        pressures = new float[layerCount];
        pointsPerLayer = new int[layerCount];

        float scalar = grid.GetDimensions().x / 5;
        for (int i = 0; i < layerCount; i++)
        {
            weights[i] = 1 - (Mathf.Pow(i / (((float)(layerCount) + 0.5f) / 2), 0.5f)); //This is WAY too complicated and will be replaced with manual values later, basically gives a falloff for the weights and also lets half the layers be negative
            pressures[i] = 1.0f + i / 10.0f; //Simple increasing pressure for now
            pointsPerLayer[i] = (int)(Mathf.Round(scalar - (Mathf.Pow(i / layerCount, i) * 2))); //Again, WAY too complicated, just want less points for higher layers, but this is a fun way to do it. The first layer will actually have less points than the second layer
        }
    }

    private static int[] GenerateRandomPoints(int count, int gridSize)
    {
        int[] points = new int[count];
        for (int i = 0; i < count; i++)
        {
            points[i] = Random.Range(0, gridSize);
        }
        return points;
    }

    //Smooths the points into a cloud by using a falloff function based on distance to each high density point, and the pressure of the layer.
    private static float[] BlendPoints(int gridSize, int[] highDensityPointIndices, float[] layer, float pressure, int radiusInCells, HexGrid grid)
    {
        for (int cellIndex = 0; cellIndex < gridSize; cellIndex++)
        {
            float cellDensity = 0.0f;
            foreach (int highPointIndex in highDensityPointIndices)
            {
                float distance = grid.GetDistance(cellIndex, highPointIndex);
                float falloffValue = Falloff(distance, radiusInCells, pressure); //Using a radius of 1/10 the grid width for now
                cellDensity += falloffValue;
            }
            layer[cellIndex] = cellDensity;
        }
        return layer;
    }


    private static float Falloff(float distance, float radiusInCells, float pressure)
    {
        if (distance > 0) {
            return Mathf.Pow(Mathf.Clamp01(1 - (distance / radiusInCells)), pressure);
        }
        else
        {
            return 1.0f;
        }
    }
}
