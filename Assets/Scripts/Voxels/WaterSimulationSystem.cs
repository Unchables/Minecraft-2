using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Voxels;

namespace Voxels
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainGeneratorSystem))]
    public partial struct WaterSimulationSystem : ISystem
    {
        // --- CHANGE 1: The list is now a member of the system struct ---
        // This allows it to persist between frames.
        private NativeList<NativeArray<Voxel>> tempArraysToDispose;
        private const float TICK_INTERVAL = 1.0f / 1.0f; 

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllChunks>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();

            // --- ENSURE THE TIMER SINGLETON EXISTS ---
            // If the singleton doesn't exist, create an entity to hold it.
            if (!SystemAPI.HasSingleton<WaterSimulationTimer>())
            {
                var singletonEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(singletonEntity, new WaterSimulationTimer { LastTickTime = 0 });
            }
            
            // We use a persistent allocator because the list lives with the system.
            tempArraysToDispose = new NativeList<NativeArray<Voxel>>(128, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            // Clean up any remaining arrays and the list itself when the world is destroyed.
            foreach (var array in tempArraysToDispose)
            {
                if (array.IsCreated) array.Dispose();
            }
            if (tempArraysToDispose.IsCreated) tempArraysToDispose.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // --- CHANGE 2: Dispose of arrays from the PREVIOUS frame ---
            // This is safe because all jobs from the last frame are now complete.
            foreach (var array in tempArraysToDispose)
            {
                array.Dispose();
            }
            tempArraysToDispose.Clear(); // Clear the list for the current frame.
            
            var currentTime = SystemAPI.Time.ElapsedTime;
            var timer = SystemAPI.GetSingletonRW<WaterSimulationTimer>();
            
            // If not enough time has passed since the last tick, exit immediately.
            if (currentTime - timer.ValueRO.LastTickTime < TICK_INTERVAL)
            {
                return; // Skip the update this frame.
            }

            // --- NEW: Create a concurrent queue for water transfers ---
            // A NativeQueue is safe for multiple parallel jobs to write to.
            var waterTransfers = new NativeQueue<WaterTransfer>(Allocator.TempJob);
            
            var allChunks = SystemAPI.GetSingleton<AllChunks>();
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            JobHandle combinedHandle = state.Dependency;
            int chunkIndexCounter = 0;

            // We need a lookup to get ChunkVoxels data from an entity handle in the second job.
            var chunkVoxelsLookup = SystemAPI.GetComponentLookup<ChunkVoxels>(false); // false = read/write access
            
            foreach (var (chunkVoxels, chunkPosition, entity) in SystemAPI
                         .Query<RefRW<ChunkVoxels>, RefRO<ChunkPosition>>()
                         .WithEntityAccess())
            {
                var leftVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(-1, 0, 0), ref tempArraysToDispose);
                var rightVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(1, 0, 0), ref tempArraysToDispose);
                var forwardVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 0, 1), ref tempArraysToDispose);
                var backVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 0, -1), ref tempArraysToDispose);
                var upVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, 1, 0), ref tempArraysToDispose);
                var downVoxels = GetNeighborVoxels(allChunks, chunkPosition.ValueRO.Value + new int3(0, -1, 0), ref tempArraysToDispose);

                // Fetch neighbor ENTITY (for writing/marking dirty)
                allChunks.Entites.TryGetValue(chunkPosition.ValueRO.Value + new int3(0, -1, 0), out var downNeighborEntity);
                
                var waterJob = new WaterSimulationJob
                {
                    // Pass the main voxel array (read-write)
                    ChunkVoxels = chunkVoxels.ValueRW.Voxels,
                    
                    // Pass neighbor arrays (read-only)
                    DownVoxels = downVoxels,

                    // Pass neighbor entities
                    DownNeighborEntity = downNeighborEntity,

                    ECB = ecb,
                    Entity = entity,
                    ChunkIndex = chunkIndexCounter,
                    ChunkSize = Chunk.ChunkSize,

                    // Pass the queue for writing transfers
                    WaterTransfers = waterTransfers.AsParallelWriter()
                };

                combinedHandle = waterJob.Schedule(combinedHandle);
                chunkIndexCounter++;
            }
            
            // --- NEW: Schedule the second job to apply the transfers ---
            var applyTransfersJob = new ApplyWaterTransfersJob
            {
                Transfers = waterTransfers,
                ChunkVoxelsLookup = chunkVoxelsLookup
            };
            
            // --- CHANGE 3: The problematic Dispose job is now GONE ---
            state.Dependency = combinedHandle;
            
            timer.ValueRW.LastTickTime = currentTime;
        }

        // Helper function is now simpler as it doesn't need to return anything
        private NativeArray<Voxel> GetNeighborVoxels(AllChunks allChunks, int3 neighborPos, ref NativeList<NativeArray<Voxel>> disposalList)
        {
            if (allChunks.Chunks.TryGetValue(neighborPos, out var neighbor))
            {
                return neighbor.Voxels;
            }
            else
            {
                var tempArray = new NativeArray<Voxel>(0, Allocator.TempJob);
                // We add the temporary array to the system's list, to be disposed next frame.
                disposalList.Add(tempArray);
                return tempArray;
            }
        }
    }

    [BurstCompile]
    public partial struct WaterSimulationJob : IJob // Changed from IJobEntity to IJob
    {
        public NativeArray<Voxel> ChunkVoxels;

        // We only need the neighbor's voxel data for reading.
        [ReadOnly] public NativeArray<Voxel> DownVoxels;

        // We need the neighbor's entity handle to create a transfer command.
        [ReadOnly] public Entity DownNeighborEntity;
        
        public EntityCommandBuffer.ParallelWriter ECB;
        public Entity Entity;
        public int ChunkIndex;
        [ReadOnly] public int ChunkSize;

        // The job now writes to the queue instead of trying to access neighbor memory directly.
        public NativeQueue<WaterTransfer>.ParallelWriter WaterTransfers;

        public const byte MAX_WATER_LEVEL = 7;

        public void Execute()
        {
            var originalVoxels = new NativeArray<Voxel>(ChunkVoxels, Allocator.Temp);
            bool chunkHasChanged = false;

            for (int i = 0; i < ChunkVoxels.Length; i++)
            {
                Voxel currentVoxel = originalVoxels[i];
                byte currentLevel = currentVoxel.GetWaterLevel();
                if (currentLevel == 0) continue;

                var currentCoords = GetLocalCoords(i);

                // --- FLOW DOWN (CROSS-CHUNK LOGIC) ---
                if (currentCoords.y == 0) // We are at the bottom of the chunk
                {
                    // Check if the neighbor below exists and we can access its data
                    if (DownNeighborEntity != Entity.Null && DownVoxels.IsCreated && DownVoxels.Length > 0)
                    {
                        int belowIndexInNeighbor = GetIndexFromCoords(currentCoords.x, ChunkSize - 1, currentCoords.z);
                        Voxel voxelBelow = DownVoxels[belowIndexInNeighbor];

                        if (!voxelBelow.IsSolid() && voxelBelow.GetWaterLevel() < MAX_WATER_LEVEL)
                        {
                            byte belowLevel = voxelBelow.GetWaterLevel();
                            byte amountToMove = (byte)math.min(currentLevel, MAX_WATER_LEVEL - belowLevel);

                            if (amountToMove > 0)
                            {
                                // 1. Remove water from this chunk's voxel
                                Voxel modifiedVoxel = ChunkVoxels[i];
                                modifiedVoxel.SetWaterLevel((byte)(currentLevel - amountToMove));
                                ChunkVoxels[i] = modifiedVoxel;
                                chunkHasChanged = true;

                                // 2. Enqueue a transfer command to add water to the neighbor
                                WaterTransfers.Enqueue(new WaterTransfer
                                {
                                    TargetChunkEntity = DownNeighborEntity,
                                    TargetVoxelIndex = belowIndexInNeighbor,
                                    Amount = amountToMove
                                });
                                
                                // 3. Mark the neighbor chunk as dirty so it will be remeshed
                                ECB.SetComponentEnabled<ChunkDirty>(ChunkIndex, DownNeighborEntity, true);

                                continue; // Water has moved, process next voxel
                            }
                        }
                    }
                }
                // --- FLOW DOWN (INTRA-CHUNK LOGIC - UNCHANGED) ---
                else 
                {
                    int belowIndex = GetIndexFromCoords(currentCoords.x, currentCoords.y - 1, currentCoords.z);
                    Voxel voxelBelow = originalVoxels[belowIndex];

                    if (!voxelBelow.IsSolid() && voxelBelow.GetWaterLevel() < MAX_WATER_LEVEL)
                    {
                        byte belowLevel = voxelBelow.GetWaterLevel();
                        byte amountToMove = (byte)math.min(currentLevel, MAX_WATER_LEVEL - belowLevel);
                        if (amountToMove > 0)
                        {
                            // --- FIX: REMOVED ALL SetBlockID CALLS ---
                            Voxel modifiedVoxel = ChunkVoxels[i];
                            modifiedVoxel.SetWaterLevel((byte)(currentLevel - amountToMove));
                            ChunkVoxels[i] = modifiedVoxel;

                            Voxel modifiedBelow = ChunkVoxels[belowIndex];
                            modifiedBelow.SetWaterLevel((byte)(belowLevel + amountToMove));
                            ChunkVoxels[belowIndex] = modifiedBelow;

                            chunkHasChanged = true;
                            continue;
                        }
                    }
                }
            }

            if (chunkHasChanged)
            {
                ECB.SetComponentEnabled<ChunkDirty>(ChunkIndex, Entity, true);
            }

            originalVoxels.Dispose();
        }

        // --- Helper functions for coordinate and index conversion ---
        private int3 GetLocalCoords(int index)
        {
            int y = index / (ChunkSize * ChunkSize);
            int temp = index - (y * ChunkSize * ChunkSize);
            int z = temp / ChunkSize;
            int x = temp % ChunkSize;
            return new int3(x, y, z);
        }

        private int GetIndexFromCoords(int x, int y, int z)
        {
            return x + z * ChunkSize + y * ChunkSize * ChunkSize;
        }
    }
    
    [BurstCompile]
    public partial struct ApplyWaterTransfersJob : IJob
    {
        public NativeQueue<WaterTransfer> Transfers;
        
        // This allows us to write to the ChunkVoxels component of ANY entity.
        [NativeDisableParallelForRestriction]
        public ComponentLookup<ChunkVoxels> ChunkVoxelsLookup;

        public void Execute()
        {
            // Process every transfer command in the queue.
            while (Transfers.TryDequeue(out var transfer))
            {
                // Check if the target entity is valid and has the component
                if (ChunkVoxelsLookup.HasComponent(transfer.TargetChunkEntity))
                {
                    var targetVoxels = ChunkVoxelsLookup[transfer.TargetChunkEntity].Voxels;
                    
                    // This is the receiving end of the water flow
                    Voxel targetVoxel = targetVoxels[transfer.TargetVoxelIndex];
                    byte newLevel = (byte)math.min(targetVoxel.GetWaterLevel() + transfer.Amount, WaterSimulationJob.MAX_WATER_LEVEL);
                    targetVoxel.SetWaterLevel(newLevel);
                    targetVoxels[transfer.TargetVoxelIndex] = targetVoxel;
                }
            }
        }
    }
    
    // A command to move a certain amount of water to a specific voxel in a target chunk entity.
    public struct WaterTransfer
    {
        public Entity TargetChunkEntity;
        public int TargetVoxelIndex;
        public byte Amount;
    }
}