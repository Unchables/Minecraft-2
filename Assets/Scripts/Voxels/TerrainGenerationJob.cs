using SimplexNoise;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile]
    public partial struct TerrainGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int3 ChunkPosition;
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int TerrainHeight;
            
        //[NoAlias][WriteOnly][NativeDisableParallelForRestriction]
        public NativeArray<Voxel> Voxels;
        
        public void Execute(int index)
        {
            // Calculate local-to-chunk 3D coordinates from the 1D index
            int x = index % 32;
            int y = (index / 32) % 32;
            int z = index / (32 * 32);
                
            // Calculate the absolute world coordinates of the voxel
            int worldX = ChunkPosition.x * 32 + x;
            int worldY = ChunkPosition.y * 32 + y;
            int worldZ = ChunkPosition.z * 32 + z;
                
            float noiseFrequency = 0.01f;
            float value = Noise.GetSimplexNoise(worldX, worldY, worldZ, noiseFrequency);
                
            //float density = value - (worldY / 64.0f);
                
            BlockType blockToPlace;
            if (value > 150)
            {
                blockToPlace = BlockType.Stone;
            }
            else
            {
                blockToPlace = BlockType.Air;
            }
                
            var voxel = new Voxel();
            voxel.SetBlockID((ushort)blockToPlace);
            Voxels[index] = voxel;
        }
    }
}