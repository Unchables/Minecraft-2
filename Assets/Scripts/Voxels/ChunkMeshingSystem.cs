using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    [BurstCompile]
    [UpdateAfter(typeof(TerrainGeneratorSystem))]
    public partial struct ChunkMeshingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<WorldSettings>();
        }

        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();

            // Query for all chunks that have voxel data but do not yet have mesh data.
            foreach (var (chunkVoxels, chunkMeshRenderData, isChunkMeshGenerating, chunkHasVoxelData, meshJobHandle)
                     in SystemAPI.Query<RefRO<ChunkVoxels>, RefRW<ChunkMeshRenderData>, EnabledRefRW<IsChunkMeshGenerating>, EnabledRefRO<ChunkHasVoxelData>, RefRW<MeshJobHandle>>()
                         .WithDisabled<IsChunkMeshGenerating>())
            {
                // 1. Calculate a safe maximum capacity for our lists.
                // Heuristic: Worst case is roughly one quad per voxel in the chunk. 
                int chunkSize = worldSettings.ChunkSize;
                int chunkVolume = chunkSize * chunkSize * chunkSize;
                
                // Each quad has 4 vertices and 6 triangle indices.
                int maxVertexCapacity = (chunkVolume / 2) * 12;
                int maxTriangleCapacity = (chunkVolume / 2) * 18;

                // 2. Create the lists with the calculated capacity.
                // They use Allocator.Persistent because they need to exist outside this system
                // until the mesh is finalized.
                var vertices = new NativeList<float3>(maxVertexCapacity, Allocator.Persistent);
                var triangles = new NativeList<int>(maxTriangleCapacity, Allocator.Persistent);

                // This counter must be disposed after the job is complete.
                var vertexCounter = new NativeArray<int>(1, Allocator.TempJob);
                vertexCounter[0] = 0; // Initialize to zero
                
                var mesherJob = new MesherJob
                {
                    ChunkSize = worldSettings.ChunkSize,
                    Voxels = chunkVoxels.ValueRO.Voxels,
                    Vertices = vertices,
                    Triangles = triangles,
                    VertexIndexCounter = vertexCounter
                };
                
                var mesherHandle = mesherJob.Schedule(state.Dependency);

                // The counter is temporary and can be disposed once the job is done.
                vertexCounter.Dispose(mesherHandle);

                chunkMeshRenderData.ValueRW.Vertices = vertices;
                chunkMeshRenderData.ValueRW.Triangles = triangles;

                isChunkMeshGenerating.ValueRW = true;

                meshJobHandle.ValueRW.Value = mesherHandle;
                state.Dependency = mesherHandle;
            }
        }
    }
}