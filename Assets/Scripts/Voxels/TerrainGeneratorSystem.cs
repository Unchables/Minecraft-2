using SimplexNoise;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Voxels
{
    [UpdateAfter(typeof(ChunkLoadAndUnloader))] // Ensure Manager runs first
    public partial struct TerrainGeneratorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            
            foreach (var (chunkVoxels, chunkPosition, isTerrainGenerating, terrainJobHandleData)
                     in SystemAPI.Query<RefRO<ChunkVoxels>, RefRO<ChunkPosition>, EnabledRefRW<IsChunkTerrainGenerating>, RefRW<TerrainJobHandle>>()
                         .WithDisabled<IsChunkTerrainGenerating>()
                         .WithDisabled<ChunkHasVoxelData>())
            {
                var terrainJob = new TerrainGenerationJob
                {
                    ChunkSize = worldSettings.ChunkSize,
                    ChunkPosition = chunkPosition.ValueRO.Value,
                    TerrainHeight = worldSettings.TerrainHeight,
                    Voxels = chunkVoxels.ValueRO.Voxels
                };

                var terrainJobHandle = terrainJob.Schedule(Chunk.ChunkSize*Chunk.ChunkSize*Chunk.ChunkSize, 2048);
                terrainJobHandleData.ValueRW.Value = terrainJobHandle;
                state.Dependency = terrainJobHandle;

                isTerrainGenerating.ValueRW = true;
            }
            
            foreach (var (chunkHasVoxelData, terrainJobHandleData, isChunkTerrainGenerating)
                     in SystemAPI.Query<EnabledRefRW<ChunkHasVoxelData>, RefRW<TerrainJobHandle>, EnabledRefRO<IsChunkTerrainGenerating>>()
                         .WithDisabled<ChunkHasVoxelData>())
            {
                if (!terrainJobHandleData.ValueRO.Value.IsCompleted) continue;

                terrainJobHandleData.ValueRW.Value.Complete();
                
                chunkHasVoxelData.ValueRW = true;
            }
        }
    }
}