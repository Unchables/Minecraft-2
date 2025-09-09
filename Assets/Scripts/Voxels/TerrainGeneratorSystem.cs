using SimplexNoise;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Voxels
{
    [UpdateInGroup(typeof(SimulationSystemGroup))] // Run in the presentation group, after simulation
    [UpdateAfter(typeof(ChunkLoadAndUnloader))] // Ensure Manager runs first
    public partial struct TerrainGeneratorSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainGenerationData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var terrainGenData = SystemAPI.GetSingleton<TerrainGenerationData>();
            
            foreach (var (chunkVoxels, chunkPosition, isTerrainGenerating, terrainJobHandleData)
                     in SystemAPI.Query<RefRO<ChunkVoxels>, RefRO<ChunkPosition>, EnabledRefRW<IsChunkTerrainGenerating>, RefRW<TerrainJobHandle>>()
                         .WithDisabled<IsChunkTerrainGenerating>()
                         .WithDisabled<ChunkHasVoxelData>())
            {
                var terrainJob = new TerrainGenerationJob
                {
                    ChunkPosition = chunkPosition.ValueRO.Value,
                    Voxels = chunkVoxels.ValueRO.Voxels,
                    
                    TerrainConfig = terrainGenData.TerrainConfig
                };

                var terrainJobHandle = terrainJob.Schedule(Chunk.ChunkSize*Chunk.ChunkSize*Chunk.ChunkSize, 2048, state.Dependency);
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