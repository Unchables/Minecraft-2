using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Voxels
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainGeneratorSystem))]
    [UpdateAfter(typeof(WaterSimulationSystem))]
    public partial struct ChunkMeshingSystem : ISystem
    {
        // A list to track temporary NativeArrays for neighbor voxels that need to be disposed of the following frame.
        private NativeList<NativeArray<Voxel>> tempArraysToDispose;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllChunks>();
            state.RequireForUpdate<WorldSettings>();
            state.RequireForUpdate<VoxelRenderResources>();

            tempArraysToDispose = new NativeList<NativeArray<Voxel>>(128, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Ensure all tracked temporary arrays are disposed of when the system is destroyed.
            foreach (var array in tempArraysToDispose)
            {
                if (array.IsCreated) array.Dispose();
            }
            if (tempArraysToDispose.IsCreated) tempArraysToDispose.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Dispose of temporary arrays that were created in the previous frame.
            foreach (var array in tempArraysToDispose)
            {
                if (array.IsCreated) array.Dispose();
            }
            tempArraysToDispose.Clear();

            var allChunks = SystemAPI.GetSingleton<AllChunks>();
            var renderResources = SystemAPI.GetSingleton<VoxelRenderResources>();

            // Process all chunks that are marked as dirty and are not currently being meshed.
            foreach (var (chunkVoxels, chunkPosition, chunkMeshRenderData, isChunkMeshGenerating, chunkDirty, meshJobHandle, isWaterMeshGenerating)
                     in SystemAPI.Query<RefRO<ChunkVoxels>, RefRO<ChunkPosition>, MeshRenderData, EnabledRefRW<IsChunkMeshGenerating>, EnabledRefRW<ChunkDirty>, RefRW<MeshJobHandle>, EnabledRefRW<IsChunkWaterMeshGenerating>>()
                         .WithAll<GenerateChunkMesh, ChunkDirty, ChunkHasVoxelData>()
                         .WithDisabled<IsChunkMeshGenerating, IsChunkWaterMeshGenerating>())
            {
                chunkDirty.ValueRW = false; // Mark the chunk as no longer dirty.

                // Pre-allocate mesh data with a sensible capacity.
                const int chunkVolume = 32 * 32 * 32;
                const int maxVertexCapacity = (chunkVolume / 2) * 12;
                const int maxTriangleCapacity = (chunkVolume / 2) * 18;

                var vertices = new NativeList<float3>(maxVertexCapacity, Allocator.Persistent);
                var triangles = new NativeList<int>(maxTriangleCapacity, Allocator.Persistent);
                var uvs0 = new NativeList<float2>(maxVertexCapacity, Allocator.Persistent);
                var uvs1 = new NativeList<float2>(maxVertexCapacity, Allocator.Persistent);

                // Get voxel data from neighboring chunks for seamless mesh generation.
                var leftVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(-1, 0, 0));
                var rightVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(1, 0, 0));
                var forwardVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 0, 1));
                var backVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 0, -1));
                var upVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 1, 0));
                var downVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, -1, 0));

                // --- Schedule Terrain Meshing Job ---
                var mesherJob = new MesherJob
                {
                    ChunkSize = 32,
                    AtlasSizeInTiles = renderResources.AtlasSizeInTiles,
                    BlockTypeData = renderResources.BlockTypeData,
                    Voxels = chunkVoxels.ValueRO.Voxels,
                    LeftVoxels = leftVoxels,
                    RightVoxels = rightVoxels,
                    DownVoxels = downVoxels,
                    UpVoxels = upVoxels,
                    BackVoxels = backVoxels,
                    ForwardVoxels = forwardVoxels,
                    Vertices = vertices,
                    Triangles = triangles,
                    UVs0 = uvs0,
                    UVs1 = uvs1
                };

                var mesherHandle = mesherJob.Schedule(state.Dependency);

                chunkMeshRenderData.ChunkMeshRenderData.ValueRW.Vertices = vertices;
                chunkMeshRenderData.ChunkMeshRenderData.ValueRW.Triangles = triangles;
                chunkMeshRenderData.ChunkMeshRenderData.ValueRW.UVs0 = uvs0;
                chunkMeshRenderData.ChunkMeshRenderData.ValueRW.UVs1 = uvs1;
                isChunkMeshGenerating.ValueRW = true;

                // --- Schedule Water Meshing Job ---
                var verticesWater = new NativeList<float3>(maxVertexCapacity, Allocator.Persistent);
                var trianglesWater = new NativeList<int>(maxTriangleCapacity, Allocator.Persistent);
                var uvsWater = new NativeList<float2>(maxVertexCapacity, Allocator.Persistent);

                var waterMesherJob = new WaterMesherJob
                {
                    ChunkSize = 32,
                    Voxels = chunkVoxels.ValueRO.Voxels,
                    LeftVoxels = leftVoxels,
                    RightVoxels = rightVoxels,
                    DownVoxels = downVoxels,
                    UpVoxels = upVoxels,
                    BackVoxels = backVoxels,
                    ForwardVoxels = forwardVoxels,
                    Vertices = verticesWater,
                    Triangles = trianglesWater,
                    UVs = uvsWater
                };

                var waterMesherHandle = waterMesherJob.Schedule(state.Dependency);

                chunkMeshRenderData.ChunkWaterMeshRenderData.ValueRW.Vertices = verticesWater;
                chunkMeshRenderData.ChunkWaterMeshRenderData.ValueRW.Triangles = trianglesWater;
                chunkMeshRenderData.ChunkWaterMeshRenderData.ValueRW.UVs = uvsWater;
                isWaterMeshGenerating.ValueRW = true;

                // Store both job handles and combine them with the system's dependency.
                meshJobHandle.ValueRW.TerrainMeshHandle = mesherHandle;
                meshJobHandle.ValueRW.WaterMeshHandle = waterMesherHandle;
                
                state.Dependency = JobHandle.CombineDependencies(mesherHandle, waterMesherHandle);
            }
        }

        /// <summary>
        /// Retrieves the voxel data of a neighboring chunk.
        /// If the neighbor does not exist, it returns a temporary empty array and tracks it for later disposal.
        /// </summary>
        private NativeArray<Voxel> GetNeighborVoxels(in AllChunks allChunks, int3 neighborPos)
        {
            if (allChunks.Chunks.TryGetValue(neighborPos, out var neighbor))
            {
                return neighbor.Voxels;
            }
            else
            {
                // Create a temporary, empty array to avoid null references in the job.
                var tempArray = new NativeArray<Voxel>(0, Allocator.TempJob);
                tempArraysToDispose.Add(tempArray); // Track for disposal in the next frame.
                return tempArray;
            }
        }
    }
}