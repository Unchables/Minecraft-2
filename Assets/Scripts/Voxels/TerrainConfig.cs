using Unity.Collections;

namespace Voxels
{
    // Must be blittable - no managed types (like strings or lists)
    public struct TerrainConfig
    {
        public NoiseSettings TerrainNoise;
        public NativeArray<BlockNoise> BlockNoises;
        
        public TreeSettings TreeSettings;
        public NoiseSettings TreePlacementNoise;
    }
    public struct NoiseSettings
    {
        public int Seed;
        public float Frequency;
        public int Octaves;
        public float Lacunarity;
        public float Persistence;
        public float Amplitude;
    }

    [System.Serializable]
    public struct TreeSettings
    {
        public int MinYLevel;
        public ushort LogBlockID;
        public ushort LeafBlockID;
        public ushort SurfaceBlockID; // The block trees can grow on (e.g., Grass)

        public int MinTrunkHeight;
        public int MaxTrunkHeight;
        
        public int MaxLeafRadius;

        // A value from 0 to 1. 0.01 = 1% chance a valid spot will grow a tree.
        public float SpawnRate;
    }
    
    [System.Serializable]
    public struct BlockNoise
    {
        public ushort BlockID;
        public float MinThreshold;
    }

    // not implementing yet
    public struct BiomeData
    {
        public float minTemp, maxTemp;
        public float minHumidity, maxHumidity;
        public float minAltitude, maxAltitude;

        public NoiseSettings heightNoise;
        public float terrainAmplitude;
    
        public ushort surfaceBlockID;
        public ushort subSurfaceBlockID;
        public int subSurfaceDepth;
        public ushort mainBlockID;
    }
}