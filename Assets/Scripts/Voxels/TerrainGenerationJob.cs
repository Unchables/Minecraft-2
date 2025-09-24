using SimplexNoise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Voxels
{
    [BurstCompile]
    public partial struct TerrainGenerationJob : IJob
    {
        [ReadOnly] public int3 ChunkPosition;
            
        [ReadOnly] public TerrainConfig TerrainConfig;
        
        public NativeArray<Voxel> Voxels;
        
        public void Execute()
        {
            BaseTerrainGen();

            TreeGen();
        }

        private void BaseTerrainGen()
        {
            for (int index = 0; index < Voxels.Length; index++)
            {
                int x = index % Chunk.ChunkSize;
                int y = (index / Chunk.ChunkSize) % Chunk.ChunkSize;
                int z = index / (Chunk.ChunkSize * Chunk.ChunkSize);

                int worldX = ChunkPosition.x * Chunk.ChunkSize + x;
                int worldY = ChunkPosition.y * Chunk.ChunkSize + y;
                int worldZ = ChunkPosition.z * Chunk.ChunkSize + z;
                
                var block = GetBaseTerrainBlockID(worldX, worldY, worldZ);

                var voxel = new Voxel();
                voxel.SetBlockID(block);
                Voxels[index] = voxel;
            }
        }
        
        /// <summary>
        /// Pass 2: Finds all potential tree spawn points in a padded area around the chunk,
        /// generates those trees, and places any parts that fall inside the current chunk's bounds.
        /// </summary>
        private void TreeGen()
        {
            var treeSettings = TerrainConfig.TreeSettings;
            var treeNoiseSettings = TerrainConfig.TreePlacementNoise;

            // The search radius must be large enough to catch any tree whose leaves could reach us.
            int searchRadius = treeSettings.MaxLeafRadius;
            
            // Iterate over a wider area that includes the search radius.
            for (int x = -searchRadius; x < Chunk.ChunkSize + searchRadius; x++)
            {
                for (int z = -searchRadius; z < Chunk.ChunkSize + searchRadius; z++)
                {
                    int worldX = ChunkPosition.x * Chunk.ChunkSize + x;
                    int worldZ = ChunkPosition.z * Chunk.ChunkSize + z;

                    // Use Worley noise to find sparse, deterministic spawn points.
                    float worleyValue = Noise.WorleyNoise2D(
                        worldX * treeNoiseSettings.Frequency, 
                        worldZ * treeNoiseSettings.Frequency, 
                        treeNoiseSettings.Seed);
                    
                    if (worleyValue > treeSettings.SpawnRate)
                    {
                        continue;
                    }
                    
                    // We found a potential spawn point. Now, find its actual surface height.
                    var chunkWorldYPos = ChunkPosition.y * Chunk.ChunkSize;
                    int surfaceY = GetSurfaceHeight(worldX, worldZ, math.max(chunkWorldYPos - Chunk.ChunkSize, TerrainConfig.TreeSettings.MinYLevel), chunkWorldYPos + Chunk.ChunkSize * 2);

                    if (surfaceY < treeSettings.MinYLevel) continue;
                    
                    // Check if the block at that surface is one trees can grow on.
                    ushort surfaceBlockID = GetBaseTerrainBlockID(worldX, surfaceY, worldZ);
                    if (surfaceBlockID != treeSettings.SurfaceBlockID)
                    {
                        continue;
                    }
                    
                    // We have a valid spawn point! Generate the full tree.
                    // The tree's base will be on top of the surface block.
                    var treeBasePos = new int3(worldX, surfaceY + 1, worldZ);
                    
                    uint seed = (uint)(worldX * 13 + worldZ * 29);
                    var random = new Random(seed == 0 ? 1u : seed);
                    
                    PlaceTree(treeBasePos, ref random);
                }
            }
        }

        /// <summary>
        /// Generates a full tree in world coordinates and calls SetBlockIfInChunk for each block.
        /// </summary>
        private void PlaceTree(int3 treeBasePos, ref Random random)
        {
            var treeSettings = TerrainConfig.TreeSettings;
            int trunkHeight = random.NextInt(treeSettings.MinTrunkHeight, treeSettings.MaxTrunkHeight + 1);

            // Place Trunk
            for (int i = 0; i < trunkHeight; i++)
            {
                SetBlockIfInChunk(treeBasePos.x, treeBasePos.y + i, treeBasePos.z, treeSettings.LogBlockID);
            }

            // Place Leaves
            int topOfTrunkY = treeBasePos.y + trunkHeight - 1;
            int leafRadius = random.NextInt(treeSettings.MaxLeafRadius - 1, treeSettings.MaxLeafRadius + 1); // Add some radius variety

            for (int lx = -leafRadius; lx <= leafRadius; lx++)
            {
                for (int ly = -leafRadius; ly <= leafRadius; ly++)
                {
                    for (int lz = -leafRadius; lz <= leafRadius; lz++)
                    {
                        // Carve a roughly spherical leaf canopy
                        if (lx * lx + ly * ly + lz * lz >= leafRadius * leafRadius) continue;
                        
                        int leafX = treeBasePos.x + lx;
                        int leafY = topOfTrunkY + ly;
                        int leafZ = treeBasePos.z + lz;
                        
                        // Only place leaves in spots that would have been air.
                        if (GetBaseTerrainBlockID(leafX, leafY, leafZ) == AirID.Value)
                        {
                            SetBlockIfInChunk(leafX, leafY, leafZ, treeSettings.LeafBlockID);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Helper that places a block only if its world coordinates fall within this job's chunk.
        /// </summary>
        private void SetBlockIfInChunk(int worldX, int worldY, int worldZ, ushort blockID)
        {
            // This is a fast way to check if a world coordinate is within this chunk's volume
            if (worldX < ChunkPosition.x * Chunk.ChunkSize || worldX >= (ChunkPosition.x + 1) * Chunk.ChunkSize ||
                worldY < ChunkPosition.y * Chunk.ChunkSize || worldY >= (ChunkPosition.y + 1) * Chunk.ChunkSize ||
                worldZ < ChunkPosition.z * Chunk.ChunkSize || worldZ >= (ChunkPosition.z + 1) * Chunk.ChunkSize)
            {
                return;
            }

            // Convert world to local chunk coords
            int localX = worldX - ChunkPosition.x * Chunk.ChunkSize;
            int localY = worldY - ChunkPosition.y * Chunk.ChunkSize;
            int localZ = worldZ - ChunkPosition.z * Chunk.ChunkSize;
            
            int index = GetIndexFromCoords(localX, localY, localZ);
            
            var voxel = new Voxel();
            voxel.SetBlockID(blockID);
            Voxels[index] = voxel;
        }
        
        /// <summary>
        /// This is the "wasted computation". It re-runs the base terrain noise logic for a single point.
        /// This is what makes the system deterministic without needing neighbor data.
        /// </summary>
        private ushort GetBaseTerrainBlockID(int worldX, int worldY, int worldZ)
        {
            var noiseSettings = TerrainConfig.TerrainNoise;
            
            float totalNoise = 0;
            float amplitude = noiseSettings.Amplitude;
            float frequency = noiseSettings.Frequency;
            
            for (int i = 0; i < noiseSettings.Octaves; i++)
            {
                float3 position = new float3(worldX, worldY, worldZ) * frequency;
                float value = (Noise.GradientNoise3D(position.x, position.y, position.z, noiseSettings.Seed) + 1) * 0.5f;
                totalNoise += value * amplitude;

                frequency *= noiseSettings.Lacunarity;
                amplitude *= noiseSettings.Persistence;
            }

            if (worldY > 0)
            {
                totalNoise -= worldY * 0.05f;
            }
            
            totalNoise = math.max(totalNoise, 0);
                
            ushort blockToPlace = AirID.Value;
            // Loop through baked data (assumed to be sorted highest threshold to lowest)
            for (int i = 0; i < TerrainConfig.BlockNoises.Length; i++)
            {
                if (totalNoise >= TerrainConfig.BlockNoises[i].MinThreshold)
                {
                    blockToPlace = TerrainConfig.BlockNoises[i].BlockID;
                    // Break after finding the FIRST valid block.
                    break; 
                }
            }
            
            return blockToPlace;
        }
        
        /// <summary>
        /// Finds the Y coordinate of the highest non-air block in a given (X, Z) column.
        /// Scans downwards from a safe, absolute height to work for any world coordinate.
        /// </summary>
        private int GetSurfaceHeight(int worldX, int worldZ, int minY = int.MinValue, int maxY = int.MinValue)
        {
            if (minY == int.MinValue) minY = -Chunk.ChunkSize;
            if (maxY == int.MinValue) maxY = Chunk.ChunkSize * 3;
            
            for (int y = maxY; y > minY; y--) 
            {
                if (GetBaseTerrainBlockID(worldX, y, worldZ) != AirID.Value)
                {
                    // We found the first solid block from the top down. This is the surface.
                    return y;
                }
            }
    
            return int.MinValue; // Return minValue if no surface was found within the scan range.
        }

        private int GetIndexFromCoords(int x, int y, int z)
        {
            return x + Chunk.ChunkSize * (y + Chunk.ChunkSize * z);
        }
    }
}