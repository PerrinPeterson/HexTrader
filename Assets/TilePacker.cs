using UnityEngine;

//A way to easily pack the tile data into a ulong for light storage and retrieval
public class TilePacker
{
    public int[] biomes;
    public (bool isBlend, bool isSameIndex)[] edgeBlends; 
    private int maxBiomes;
    public ulong packedTile;
    public ushort cornerBlends; // 2 bits per corner, 00 = no blend, 10 = blend with the same edge index, 11 = blend with the next edge index.
    private int TileSize = 7; //Number of biomes per tile
    public TilePacker(int[] Biomes, int MaxBiomes, (bool isBlend, bool isSameIndex)[] edgeBlends)
    {
        biomes = Biomes; //The biomes of the tile
        maxBiomes = MaxBiomes; //The number of biomes in the game. So we can shave down the data size
        this.edgeBlends = edgeBlends;
        Pack();
    }

    public TilePacker(ulong packedTile, int biomeCount, ushort cornerBlends)
    {
        this.packedTile = packedTile;
        this.cornerBlends = cornerBlends;
        maxBiomes = biomeCount;
        Unpack();
    }

    public void SetBiomes(int[] Biomes)
    {
        biomes = Biomes;
        Pack();
    }
    public void SetBlends((bool isBlend, bool isSameIndex)[] edgeBlends)
    {
        this.edgeBlends = edgeBlends;
        Pack();
    }

    public void SetAllBiomes(int biomeIndex)
    {
        for (int i = 0; i < biomes.Length; i++)
        {
            biomes[i] = biomeIndex;
        }
        for (int i = 0; i < edgeBlends.Length; i++)
        {
            edgeBlends[i] = (false, false); //No blends if all biomes are the same
        }
        Pack();
    }

    public void SetPackedTile(ulong packedTile, ushort cornerBlends)
    {
        this.packedTile = packedTile;
        this.cornerBlends = cornerBlends;
        Unpack();
    }

    public void SetPackedTile(HexData data)
    {
        SetPackedTile(data.ID, data.cornerFlags);
    }

    public void Pack()
    {
        ulong packed = 0;
        ushort cornerFlags = 0;
        //Figure out how long each biome needs to be
        int bitsPerBiome = Mathf.Max(1, Mathf.CeilToInt(Mathf.Log(maxBiomes, 2))); //Log does this for us, then we round up. We also need at least 1 bit
        if (bitsPerBiome * biomes.Length > 64)
        {
            Debug.LogError("Too many biomes to pack into a ulong!"); 
            packedTile = 0;
            cornerBlends = 0;
            return;
        }
        for (int i = 0; i < biomes.Length; i++)
        {
            if (biomes[i] >= maxBiomes)
            {
                Debug.LogError("Biome index out of range when packing tile data!");
                packedTile = 0;
                cornerBlends = 0;
                return;
            }
            packed |= ((ulong)biomes[i] << (i * bitsPerBiome));
        }
        //Pack up the edge blends into a short
        for (int i = 0; i < edgeBlends.Length; i++)
        {
            if (edgeBlends[i].isBlend)
            {
                cornerFlags |= (ushort)(1 << (i * 2)); //Set the blend flag
                if (edgeBlends[i].isSameIndex)
                {
                    cornerFlags |= (ushort)(1 << (i * 2 + 1)); //Set the same index flag
                }
            }
        }

        packedTile = packed;
        cornerBlends = cornerFlags;
    }

    //New addition, we'll tack on the corners. The last 6 indexes will be the corners.
    public void Unpack()
    {
        int bitsPerBiome = Mathf.CeilToInt(Mathf.Log(maxBiomes, 2));
        int[] unpackedBiomes = new int[TileSize];

        (bool isBlend, bool isSameIndex)[] unpackedBlends = new (bool, bool)[6];

        for (int i = 0; i < unpackedBiomes.Length; i++)
        {
            ulong mask = ((1UL << bitsPerBiome) - 1) << (i * bitsPerBiome);
            unpackedBiomes[i] = (int)((packedTile & mask) >> (i * bitsPerBiome));
        }
        for (int i = 0; i < unpackedBlends.Length; i++)
        {
            ushort maskBlend = (ushort)(1u << (i * 2));
            ushort maskSame = (ushort)(1u << (i * 2 + 1));

            bool isBlend = (cornerBlends & maskBlend) != 0;
            bool isSameIndex = (cornerBlends & maskSame) != 0;

            unpackedBlends[i] = (isBlend, isSameIndex);
        }
        biomes = unpackedBiomes;
        edgeBlends = unpackedBlends;
    }
}
