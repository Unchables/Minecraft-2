using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile]
    public partial struct MesherJob : IJob
    {
        // --- Input Data ---
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int AtlasSizeInTiles;

        // Voxel Data
        [ReadOnly] public NativeArray<Voxel> Voxels;
        [ReadOnly] public NativeArray<Voxel> LeftVoxels;   // -X
        [ReadOnly] public NativeArray<Voxel> RightVoxels;  // +X
        [ReadOnly] public NativeArray<Voxel> DownVoxels;   // -Y
        [ReadOnly] public NativeArray<Voxel> UpVoxels;     // +Y
        [ReadOnly] public NativeArray<Voxel> BackVoxels;   // -Z
        [ReadOnly] public NativeArray<Voxel> ForwardVoxels;// +Z
        
        // Texture Atlas Mapping Data
        [ReadOnly] public NativeArray<BlockTextureData> BlockTypeData;

        // --- Output Data ---
        public NativeList<float3> Vertices;
        [WriteOnly] public NativeList<int> Triangles;
        [WriteOnly] public NativeList<float2> UVs;
        
        /// <summary>
        /// This method is executed for every single voxel in the chunk in parallel.
        /// </summary>
        public void Execute()
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        var index = GetIndexFromCoords(x, y, z);
                        
                        ushort blockID = Voxels[index].GetBlockID();
                        
                        if (blockID == AirID.Value) continue;
                        
                        if(IsFaceVisible(x, y + 1, z)) AddFaceData(new int3(x, y, z), 0, blockID);
                        if(IsFaceVisible(x, y - 1, z)) AddFaceData(new int3(x, y, z), 1, blockID);
                        if(IsFaceVisible(x - 1, y, z)) AddFaceData(new int3(x, y, z), 2, blockID);
                        if(IsFaceVisible(x + 1, y, z)) AddFaceData(new int3(x, y, z), 3, blockID);
                        if(IsFaceVisible(x, y, z + 1)) AddFaceData(new int3(x, y, z), 4, blockID);
                        if(IsFaceVisible(x, y, z - 1)) AddFaceData(new int3(x, y, z), 5, blockID);
                    }
                }
            }
        }
        
        /// <summary>
        /// Checks if a face is visible by checking the block in the given direction.
        /// Handles checks inside the current chunk, in neighbor chunks, and at world boundaries.
        /// </summary>
        private bool IsFaceVisible(int x, int y, int z)
        {
            if (x < 0)
            {
                if (LeftVoxels.Length == 0) return true;
                return LeftVoxels[GetIndexFromCoords(ChunkSize - 1, y, z)].GetBlockID() == AirID.Value;
            }
            if (x >= ChunkSize)
            {
                if (RightVoxels.Length == 0) return true;
                return RightVoxels[GetIndexFromCoords(0, y, z)].GetBlockID() == AirID.Value;
            }
            if (y < 0)
            {
                if (DownVoxels.Length == 0) return true;
                return DownVoxels[GetIndexFromCoords(x, ChunkSize - 1, z)].GetBlockID() == AirID.Value;
            }
            if (y >= ChunkSize)
            {
                if (UpVoxels.Length == 0) return true;
                return UpVoxels[GetIndexFromCoords(x, 0, z)].GetBlockID() == AirID.Value;
            }
            if (z < 0)
            {
                if (BackVoxels.Length == 0) return true;
                return BackVoxels[GetIndexFromCoords(x, y, ChunkSize - 1)].GetBlockID() == AirID.Value;
            }
            if (z >= ChunkSize)
            {
                if (ForwardVoxels.Length == 0) return true;
                return ForwardVoxels[GetIndexFromCoords(x, y, 0)].GetBlockID() == AirID.Value;
            }
            
            // Neighbor is inside the current chunk
            return Voxels[GetIndexFromCoords(x, y, z)].GetBlockID() == AirID.Value;
        }

        /// <summary>
        /// Adds the vertex, triangle, and UV data for a single visible face.
        /// Must be called from an unsafe context.
        /// </summary>
        private unsafe void AddFaceData(int3 position, int faceIndex, ushort blockID)
        {
            // --- UV Calculation ---
            BlockTextureData blockTextures = BlockTypeData[blockID];
            BlockFaceTextures faceTexture;

            switch (faceIndex)
            {
                case 0:  faceTexture = blockTextures.Top;    break; // Top
                case 1:  faceTexture = blockTextures.Bottom; break; // Bottom
                default: faceTexture = blockTextures.Side;   break; // Sides
            }

            float tileSize = 1.0f / AtlasSizeInTiles;
            float uvX = faceTexture.TileX * tileSize;
            float uvY = faceTexture.TileY * tileSize;

            // --- 2. Define the four corners of the tile in the atlas ---
            var uv00 = new float2(uvX + tileSize, uvY);      // Bottom-Left UV
            var uv10 = new float2(uvX, uvY);                 // Bottom-Right UV
            var uv01 = new float2(uvX, uvY + tileSize);      // Top-Left UV
            var uv11 = new float2(uvX + tileSize, uvY + tileSize); // Top-Right UV
            
            int x = position.x;
            int y = position.y;
            int z = position.z;
            
            if (faceIndex == 0) // Top Face
            {
                Vertices.Add(new float3(x,     y + 1, z    )); UVs.Add(uv00);
                Vertices.Add(new float3(x,     y + 1, z + 1)); UVs.Add(uv10);
                Vertices.Add(new float3(x + 1, y + 1, z + 1)); UVs.Add(uv01);
                Vertices.Add(new float3(x + 1, y + 1, z    )); UVs.Add(uv11);
            }

            if (faceIndex == 1) // Bottom Face
            {
                Vertices.Add(new float3(x,     y, z    )); UVs.Add(uv00);
                Vertices.Add(new float3(x + 1, y, z    )); UVs.Add(uv10);
                Vertices.Add(new float3(x + 1, y, z + 1)); UVs.Add(uv01);
                Vertices.Add(new float3(x,     y, z + 1)); UVs.Add(uv11);
            }

            if (faceIndex == 2) // Left Face
            {
                Vertices.Add(new float3(x, y,     z    )); UVs.Add(uv00);
                Vertices.Add(new float3(x, y,     z + 1)); UVs.Add(uv10);
                Vertices.Add(new float3(x, y + 1, z + 1)); UVs.Add(uv01);
                Vertices.Add(new float3(x, y + 1, z    )); UVs.Add(uv11);
            }

            if (faceIndex == 3) // Right Face
            {
                Vertices.Add(new float3(x + 1, y,     z + 1)); UVs.Add(uv00);
                Vertices.Add(new float3(x + 1, y,     z    )); UVs.Add(uv10);
                Vertices.Add(new float3(x + 1, y + 1, z    )); UVs.Add(uv01);
                Vertices.Add(new float3(x + 1, y + 1, z + 1)); UVs.Add(uv11);
            }

            if (faceIndex == 4) // Front Face
            {
                Vertices.Add(new float3(x,     y,     z + 1)); UVs.Add(uv00);
                Vertices.Add(new float3(x + 1, y,     z + 1)); UVs.Add(uv10);
                Vertices.Add(new float3(x + 1, y + 1, z + 1)); UVs.Add(uv01);
                Vertices.Add(new float3(x,     y + 1, z + 1)); UVs.Add(uv11);
            }

            if (faceIndex == 5) // Back Face
            {
                Vertices.Add(new float3(x + 1, y,     z    )); UVs.Add(uv00);
                Vertices.Add(new float3(x,     y,     z    )); UVs.Add(uv10);
                Vertices.Add(new float3(x,     y + 1, z    )); UVs.Add(uv01);
                Vertices.Add(new float3(x + 1, y + 1, z    )); UVs.Add(uv11);
                
            }
            AddTriangleIndices();
        }
        [BurstCompile]
        private void AddTriangleIndices()
        {
            int vertCount = Vertices.Length;

            // First triangle
            Triangles.Add(vertCount - 4);
            Triangles.Add(vertCount - 3);
            Triangles.Add(vertCount - 2);

            // Second triangle
            Triangles.Add(vertCount - 4);
            Triangles.Add(vertCount - 2);
            Triangles.Add(vertCount - 1);
        }
        private int GetIndexFromCoords(int x, int y, int z)
        {
            return x + ChunkSize * (y + ChunkSize * z);
        }
    }
}

/*using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile]
    public partial struct MesherJob : IJob
    {
        [ReadOnly] public int ChunkSize;
        
        [ReadOnly] public NativeArray<Voxel> Voxels;
        
        [ReadOnly] public NativeArray<Voxel> LeftVoxels;
        [ReadOnly] public NativeArray<Voxel> RightVoxels;
        [ReadOnly] public NativeArray<Voxel> ForwardVoxels;
        [ReadOnly] public NativeArray<Voxel> BackVoxels;
        [ReadOnly] public NativeArray<Voxel> UpVoxels;
        [ReadOnly] public NativeArray<Voxel> DownVoxels;
        
        [ReadOnly] public NativeArray<BlockTextureData> BlockTypeData;
        [ReadOnly] public int AtlasSizeInTiles;
        
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        public NativeList<float2> UVs;
        
        [BurstCompile]
        public void Execute()
        {
            for (int x = 0; x < ChunkSize; x++)
            {
                for (int y = 0; y < ChunkSize; y++)
                {
                    for (int z = 0; z < ChunkSize; z++)
                    {
                        var index = GetIndexFromCoords(x, y, z);
                        if ((BlockType)Voxels[index].GetBlockID() == BlockType.Air)
                            continue;
                        
                        if(IsFaceVisible(x, y + 1, z)) AddFaceData(x, y, z, 0);
                        if(IsFaceVisible(x, y - 1, z)) AddFaceData(x, y, z, 1);
                        if(IsFaceVisible(x - 1, y, z)) AddFaceData(x, y, z, 2);
                        if(IsFaceVisible(x + 1, y, z)) AddFaceData(x, y, z, 3);
                        if(IsFaceVisible(x, y, z + 1)) AddFaceData(x, y, z, 4);
                        if(IsFaceVisible(x, y, z - 1)) AddFaceData(x, y, z, 5);
                    }
                }
            }
        }
        [BurstCompile]
        private bool IsFaceVisible(int x, int y, int z)
        {
            if (x < 0)
            {
                if (LeftVoxels.Length == 0) return true;
                return (BlockType)LeftVoxels[GetIndexFromCoords(ChunkSize - 1, y, z)].GetBlockID() == BlockType.Air;
            }
            if (x >= ChunkSize)
            {
                if (RightVoxels.Length == 0) return true;
                return (BlockType)RightVoxels[GetIndexFromCoords(0, y, z)].GetBlockID() == BlockType.Air;
            }
            if (y < 0)
            {
                if (DownVoxels.Length == 0) return true;
                return (BlockType)DownVoxels[GetIndexFromCoords(x, ChunkSize - 1, z)].GetBlockID() == BlockType.Air;
            }
            if (y >= ChunkSize)
            {
                if (UpVoxels.Length == 0) return true;
                return (BlockType)UpVoxels[GetIndexFromCoords(x, 0, z)].GetBlockID() == BlockType.Air;
            }
            if (z < 0)
            {
                if (BackVoxels.Length == 0) return true;
                return (BlockType)BackVoxels[GetIndexFromCoords(x, y, ChunkSize - 1)].GetBlockID() == BlockType.Air;
            }
            if (z >= ChunkSize)
            {
                if (ForwardVoxels.Length == 0) return true;
                return (BlockType)ForwardVoxels[GetIndexFromCoords(x, y, 0)].GetBlockID() == BlockType.Air;
            }
            
            return (BlockType)Voxels[GetIndexFromCoords(x, y, z)].GetBlockID() == BlockType.Air; // voxel is inside this chunk
        }
        [BurstCompile]
        private void AddFaceData(int x, int y, int z, int faceIndex)
        {
            // Based on faceIndex, determine vertices and triangles
            // Add vertices and triangles for the visible face
            // Calculate and add corresponding UVs
            
            BlockTextureData blockTextures = BlockTypeData[blockID];
            BlockFaceTextures faceTexture;

            if (faceIndex == 0) // Top Face
            {
                Vertices.Add(new float3(x,     y + 1, z    ));
                Vertices.Add(new float3(x,     y + 1, z + 1)); 
                Vertices.Add(new float3(x + 1, y + 1, z + 1));
                Vertices.Add(new float3(x + 1, y + 1, z    )); 
            }

            if (faceIndex == 1) // Bottom Face
            {
                Vertices.Add(new float3(x,     y, z    ));
                Vertices.Add(new float3(x + 1, y, z    )); 
                Vertices.Add(new float3(x + 1, y, z + 1));
                Vertices.Add(new float3(x,     y, z + 1)); 
            }

            if (faceIndex == 2) // Left Face
            {
                Vertices.Add(new float3(x, y,     z    ));
                Vertices.Add(new float3(x, y,     z + 1));
                Vertices.Add(new float3(x, y + 1, z + 1));
                Vertices.Add(new float3(x, y + 1, z    ));
            }

            if (faceIndex == 3) // Right Face
            {
                Vertices.Add(new float3(x + 1, y,     z + 1));
                Vertices.Add(new float3(x + 1, y,     z    ));
                Vertices.Add(new float3(x + 1, y + 1, z    ));
                Vertices.Add(new float3(x + 1, y + 1, z + 1));
            }

            if (faceIndex == 4) // Front Face
            {
                Vertices.Add(new float3(x,     y,     z + 1));
                Vertices.Add(new float3(x + 1, y,     z + 1));
                Vertices.Add(new float3(x + 1, y + 1, z + 1));
                Vertices.Add(new float3(x,     y + 1, z + 1));
            }

            if (faceIndex == 5) // Back Face
            {
                Vertices.Add(new float3(x + 1, y,     z    ));
                Vertices.Add(new float3(x,     y,     z    ));
                Vertices.Add(new float3(x,     y + 1, z    ));
                Vertices.Add(new float3(x + 1, y + 1, z    ));
                
            }
            AddTriangleIndices();
        }
        [BurstCompile]
        private void AddTriangleIndices()
        {
            int vertCount = Vertices.Length;

            // First triangle
            Triangles.Add(vertCount - 4);
            Triangles.Add(vertCount - 3);
            Triangles.Add(vertCount - 2);

            // Second triangle
            Triangles.Add(vertCount - 4);
            Triangles.Add(vertCount - 2);
            Triangles.Add(vertCount - 1);
        }

        [BurstCompile]
        private int GetIndexFromCoords(int x, int y, int z)
        {
            return x + ChunkSize * (y + ChunkSize * z);
        }
    }
}*/