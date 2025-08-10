using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe; // Required for GetUnsafePtr
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
        
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<int> VertexIndexCounter;
        
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
                        
                        // Check each face of the voxel for visibility
                        NativeArray<bool> facesVisible = new NativeArray<bool>(6, Allocator.Temp);
                        
                        // Check visibility for each face
                        facesVisible[0] = IsFaceVisible(x, y + 1, z); // Top
                        facesVisible[1] = IsFaceVisible(x, y - 1, z); // Bottom
                        facesVisible[2] = IsFaceVisible(x - 1, y, z); // Left
                        facesVisible[3] = IsFaceVisible(x + 1, y, z); // Right
                        facesVisible[4] = IsFaceVisible(x, y, z + 1); // Front
                        facesVisible[5] = IsFaceVisible(x, y, z - 1); // Back
                        
                        for (int i = 0; i < facesVisible.Length; i++)
                        {
                            if (facesVisible[i])
                                AddFaceData(x, y, z, i); // Method to add mesh data for the visible face
                        }
                    }
                }
            }
        }
        private bool IsFaceVisible(int x, int y, int z)
        {
            // Check if the neighboring voxel in the given direction is inactive or out of bounds
            if (x < 0 || x >= ChunkSize || y < 0 || y >= ChunkSize || z < 0 || z >= ChunkSize)
                return true; // Face is at the boundary of the chunk
            return (BlockType)Voxels[GetIndexFromCoords(x, y, z)].GetBlockID() == BlockType.Air;
        }
        private void AddFaceData(int x, int y, int z, int faceIndex)
        {
            // Based on faceIndex, determine vertices and triangles
            // Add vertices and triangles for the visible face
            // Calculate and add corresponding UVs

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