using Unity.Collections;

namespace Voxels
{
    // Must be blittable - no managed types (like strings or lists)
    public struct TerrainConfig
    {
        public NoiseSettings NoiseSettings;
        public NativeArray<BlockNoise> BlockNoises;
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
    public struct BlockNoise
    {
        public ushort BlockID;
        public float MinThreshold;
    }

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