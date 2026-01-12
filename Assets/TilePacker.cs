using UnityEngine;

//A way to easily pack the tile data into a ulong for light storage and retrieval
public class TilePacker
{
    public int[] biomes;
    private int maxBiomes;
    public ulong packedTile;
    private int TileSize = 7; //Number of biomes per tile
    public TilePacker(int[] Biomes, int MaxBiomes)
    {
        biomes = Biomes; //The biomes of the tile
        maxBiomes = MaxBiomes; //The number of biomes in the game. So we can shave down the data size
        packedTile = Pack();
    }

    public TilePacker(ulong packedTile, int biomeCount)
    {
        this.packedTile = packedTile;
        maxBiomes = biomeCount;
        biomes = Unpack();
    }

    public void SetBiomes(int[] Biomes)
    {
        biomes = Biomes;
        packedTile = Pack();
    }
    public void SetAllBiomes(int biomeIndex)
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            biomes[i] = biomeIndex;
        }
        packedTile = Pack();
    }

    public void SetPackedTile(ulong packedTile)
    {
        this.packedTile = packedTile;
        biomes = Unpack();
    }

    public ulong Pack()
    {
        ulong packed = 0;
        //Figure out how long each biome needs to be
        int bitsPerBiome = Mathf.Max(1, Mathf.CeilToInt(Mathf.Log(maxBiomes, 2))); //Log does this for us, then we round up. We also need at least 1 bit
        if (bitsPerBiome * biomes.Length > 64)
        {
            Debug.LogError("Too many biomes to pack into a ulong!"); //Might still want to do edge blending on the fly. Because holy crap is there a lot of data here
            return 0;
        }
        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] >= maxBiomes)
            {
                Debug.LogError("Biome index out of range when packing tile data!");
                return 0;
            }
            packed |= ((ulong)biomes[i] << (i * bitsPerBiome));
        }
        return packed;
    }

    public int[] Unpack()
    {
        int bitsPerBiome = Mathf.CeilToInt(Mathf.Log(maxBiomes, 2));
        int[] unpackedBiomes = new int[TileSize];
        for (int i = 0; i < unpackedBiomes.Length; i++)
        {
            ulong mask = ((1UL << bitsPerBiome) - 1) << (i * bitsPerBiome);
            unpackedBiomes[i] = (int)((packedTile & mask) >> (i * bitsPerBiome));
        }
        return unpackedBiomes;
    }
}
