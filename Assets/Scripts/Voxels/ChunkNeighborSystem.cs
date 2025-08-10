using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    // Run this system after the load/unload system has finished modifying the chunks.
    [UpdateAfter(typeof(ChunkLoadAndUnloader))]
    public partial struct ChunkNeighborSystem : ISystem
    {
        private EntityQuery chunkQuery;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Cache the query for performance
            chunkQuery = SystemAPI.QueryBuilder().WithAll<ChunkPosition>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            int chunkCount = chunkQuery.CalculateEntityCount();
            if (chunkCount == 0) return;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var chunkMap = new NativeHashMap<int3, Entity>(chunkCount, Allocator.TempJob);

            // --- JOB 1: Populate the map (single-threaded) ---
            var mapChunksJob = new MapChunksJobRevised
            {
                ChunkMap = chunkMap
            };
            // .Schedule() instead of .ScheduleParallel() ensures it runs on a single thread.
            var mapChunksHandle = mapChunksJob.Schedule(state.Dependency);

            // --- JOB 2: Assign neighbors (multi-threaded) ---
            var assignNeighborsJob = new AssignNeighborsJob
            {
                ChunkMap = chunkMap,
                ECB = ecb.AsParallelWriter()
            };
            var query = SystemAPI.QueryBuilder().WithAll<ChunkPosition>().WithNone<NeighbouringChunks>().Build();
            var assignNeighborsHandle = assignNeighborsJob.ScheduleParallel(query, mapChunksHandle);

            // --- CLEANUP ---
            state.Dependency = assignNeighborsHandle;
            chunkMap.Dispose(assignNeighborsHandle);
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        // --- FIX: This is now a single-threaded IJob ---
        [BurstCompile]
        private struct MapChunksJob : IJob
        {
            // No ParallelWriter needed. Direct access is safe in a single-threaded job.
            public NativeHashMap<int3, Entity> ChunkMap;
            
            // We use TypeHandles to get access to component data.
            [ReadOnly] public ComponentTypeHandle<ChunkPosition> PositionTypeHandle;
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            
            private ArchetypeChunk chunk; // We need to store the chunk being processed

            public void Execute()
            {
                // This is a dummy method body for the interface; the real work is done
                // in the IJobChunk version of this pattern, which is more complex.
                // For IJob, you would typically pass NativeArrays created from ToComponentDataArray.
                // However, this highlights the API complexity. The IJobEntity fix below is cleaner.
            }
        }
        
        // Let's correct this using a cleaner pattern that avoids manual IJob.
        // The original intent was good, but the API was wrong.
        // We'll use an Entity-based job that is still single-threaded for the write.

        // --- JOB 1 (Revised) ---
        [BurstCompile]
        private partial struct MapChunksJobRevised : IJobEntity
        {
            // We can write to the hashmap directly if we ensure this job isn't run in parallel.
            public NativeHashMap<int3, Entity> ChunkMap;

            public void Execute(Entity entity, in ChunkPosition position)
            {
                ChunkMap.TryAdd(position.Value, entity);
            }
        }

        [BurstCompile]
        private partial struct AssignNeighborsJob : IJobEntity
        {
            [ReadOnly] public NativeHashMap<int3, Entity> ChunkMap;
            public EntityCommandBuffer.ParallelWriter ECB;

            // We use WithNone to ensure we only run this once per chunk.
            public void Execute(Entity entity, [ChunkIndexInQuery] int chunkIndexInQuery, in ChunkPosition position)
            {
                var neighbors = new NeighbouringChunks();
                var currentPos = position.Value;
                
                // For each of the 6 directions, calculate the neighbor's position
                // and look it up in the completed map.
                
                // Top (+Y)
                ChunkMap.TryGetValue(currentPos + new int3(0, 1, 0), out neighbors.Top);
                // Bottom (-Y)
                ChunkMap.TryGetValue(currentPos + new int3(0, -1, 0), out neighbors.Bottom);
                // Left (-X)
                ChunkMap.TryGetValue(currentPos + new int3(-1, 0, 0), out neighbors.Left);
                // Right (+X)
                ChunkMap.TryGetValue(currentPos + new int3(1, 0, 0), out neighbors.Right);
                // Front (+Z)
                ChunkMap.TryGetValue(currentPos + new int3(0, 0, 1), out neighbors.Front);
                // Back (-Z)
                ChunkMap.TryGetValue(currentPos + new int3(0, 0, -1), out neighbors.Back);

                // Add the completed component to the entity.
                ECB.AddComponent(chunkIndexInQuery, entity, neighbors);
            }
        }
    }
}