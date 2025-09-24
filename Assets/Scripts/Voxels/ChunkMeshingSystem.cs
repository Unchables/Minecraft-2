using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))] // Run in the presentation group, after simulation
    [UpdateAfter(typeof(TerrainGeneratorSystem))]
    public partial struct ChunkMeshingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllChunks>();
            state.RequireForUpdate<WorldSettings>();
            state.RequireForUpdate<VoxelRenderResources>(); // Needs the atlas data
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var allChunks = SystemAPI.GetSingleton<AllChunks>();

            // Get the singleton that holds our atlas and texture data
            var renderResources = SystemAPI.GetSingleton<VoxelRenderResources>();

            var jobHandles = new NativeList<JobHandle>(Allocator.Temp);
            
            // Query for all chunks that have voxel data but do not yet have mesh data.
            foreach (var (chunkVoxels, chunkPosition, chunkMeshRenderData, isChunkMeshGenerating, chunkHasVoxelData,
                         meshJobHandle, generateChunkMesh)
                     in SystemAPI
                         .Query<RefRO<ChunkVoxels>, RefRO<ChunkPosition>, RefRW<ChunkMeshRenderData>,
                             EnabledRefRW<IsChunkMeshGenerating>, EnabledRefRO<ChunkHasVoxelData>,
                             RefRW<MeshJobHandle>, EnabledRefRO<GenerateChunkMesh>>()
                         .WithDisabled<IsChunkMeshGenerating>())
            {
                // --- 2. Create and Allocate Mesh Data Lists ---
                int chunkVolume = 32 * 32 * 32; // Assuming ChunkSize of 32
                int maxVertexCapacity = (chunkVolume / 2) * 12; // Safer capacity
                int maxTriangleCapacity = (chunkVolume / 2) * 18;

                var vertices = new NativeList<float3>(maxVertexCapacity, Allocator.Persistent);
                var triangles = new NativeList<int>(maxTriangleCapacity, Allocator.Persistent);
                var uvs = new NativeList<float2>(maxVertexCapacity, Allocator.Persistent);
                
                var vertexCounter = new NativeArray<int>(1, Allocator.TempJob);
                vertexCounter[0] = 0; // CRITICAL: Initialize the counter to zero
                
                NativeArray<Voxel> leftVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(-1, 0, 0), out ChunkVoxels lVoxels)
                        ? lVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);
            
                NativeArray<Voxel> rightVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(1, 0, 0), out ChunkVoxels rVoxels)
                        ? rVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);
            
                NativeArray<Voxel> forwardVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(0, 0, 1), out ChunkVoxels fVoxels)
                        ? fVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);
            
                NativeArray<Voxel> backVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(0, 0, -1), out ChunkVoxels bVoxels)
                        ? bVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);
            
                NativeArray<Voxel> upVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(0, 1, 0), out ChunkVoxels uVoxels)
                        ? uVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);
            
                NativeArray<Voxel> downVoxels = 
                    allChunks.Chunks.TryGetValue(chunkPosition.ValueRO.Value + new int3(0, -1, 0), out ChunkVoxels dVoxels)
                        ? dVoxels.Voxels : new NativeArray<Voxel>(0, Allocator.TempJob);

                // --- 3. Populate and Schedule the MesherJob ---
                var mesherJob = new MesherJob
                {
                    ChunkSize = 32, // Pass this in from WorldSettings if dynamic
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
                    UVs = uvs
                };
                
                // Schedule the MesherJob as an IJob
                var mesherHandle = mesherJob.Schedule(state.Dependency);
                
                chunkMeshRenderData.ValueRW.Vertices = vertices;
                chunkMeshRenderData.ValueRW.Triangles = triangles;
                chunkMeshRenderData.ValueRW.UVs = uvs;
                
                isChunkMeshGenerating.ValueRW = true;
                
                meshJobHandle.ValueRW.Value = mesherHandle;
                jobHandles.Add(mesherHandle);
                
                vertexCounter.Dispose(mesherHandle);
            }
            
            state.Dependency = JobHandle.CombineDependencies(jobHandles.AsArray());
        }
    }
}