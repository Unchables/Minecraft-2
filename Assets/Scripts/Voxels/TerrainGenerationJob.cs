using SimplexNoise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels
{
    [BurstCompile]
    public partial struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkPosition;
            
        [ReadOnly] public TerrainConfig TerrainConfig;
        
        public NativeArray<Voxel> Voxels;
        
        [BurstCompile]
        public void Execute(int index)
        {
            NoiseSettings noiseSettings = TerrainConfig.NoiseSettings;
            // Calculate local-to-chunk 3D coordinates from the 1D index
            int x = index % Chunk.ChunkSize;
            int y = (index / Chunk.ChunkSize) % Chunk.ChunkSize;
            int z = index / (Chunk.ChunkSize * Chunk.ChunkSize);
                
            // Calculate the absolute world coordinates of the voxel
            int worldX = ChunkPosition.x * Chunk.ChunkSize + x;
            int worldY = ChunkPosition.y * Chunk.ChunkSize + y;
            int worldZ = ChunkPosition.z * Chunk.ChunkSize + z;
                
            float totalNoise = 0;
            float amplitude = noiseSettings.Amplitude;
            float frequency = noiseSettings.Frequency;
            
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                float3 position = new float3(worldX, worldY, worldZ) * noiseSettings.Frequency;
                float value = (Noise.GradientNoise3D(position.x, position.y, position.z, noiseSettings.Seed) + 1) * 0.5f;
                totalNoise += value * noiseSettings.Amplitude;

                frequency *= noiseSettings.Lacunarity;
                amplitude *= noiseSettings.Persistence;
            }

            if (worldY > 0)
            {
                totalNoise -= worldY * 0.05f;
            }
            
            totalNoise = Mathf.Max(totalNoise, 0);
                
            ushort blockToPlace = 1;
            for (int i = 0; i < TerrainConfig.BlockNoises.Length; i++)
            {
                if (totalNoise >= TerrainConfig.BlockNoises[i].MinThreshold)
                {
                    blockToPlace = TerrainConfig.BlockNoises[i].BlockID;
                }
                else
                {
                    break;
                }
            }
                
            var voxel = new Voxel();
            voxel.SetBlockID(blockToPlace);
            Voxels[index] = voxel;
        }
    }
}