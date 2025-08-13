using System.Collections.Generic;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Voxels
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ChunkLoadAndUnloader : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<AllChunks>();
            state.RequireForUpdate<WorldSettings>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<LastPlayerChunkCoord>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            //var allChunks = SystemAPI.GetSingleton<AllChunks>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            
            ref var lastPlayerChunkCoord = ref SystemAPI.GetComponentRW<LastPlayerChunkCoord>(playerEntity).ValueRW;
            int3 currentPlayerChunkPos = (int3)math.round(SystemAPI.GetComponent<LocalTransform>(playerEntity).Position / worldSettings.ChunkSize);
            
            /*if (currentPlayerChunkPos.Equals(lastPlayerChunkCoord.ChunkCoord))
                return;*/
            
            lastPlayerChunkCoord.ChunkCoord = currentPlayerChunkPos;
            
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            // --- STEP 1: Build a HashSet of all chunks that are required ---
            var requiredChunks = new NativeList<int3>(Allocator.Temp);
            for (int x = -worldSettings.ChunkLoadRadius; x <= worldSettings.ChunkLoadRadius; x++)
            {
                for (int y = -worldSettings.ChunkLoadRadius; y <= worldSettings.ChunkLoadRadius; y++)
                {
                    for (int z = -worldSettings.ChunkLoadRadius; z <= worldSettings.ChunkLoadRadius; z++)
                    {
                        if (x * x + y * y + z * z > worldSettings.ChunkLoadRadius * worldSettings.ChunkLoadRadius) continue;
                        requiredChunks.Add(new int3(currentPlayerChunkPos.x + x, currentPlayerChunkPos.y + y, currentPlayerChunkPos.z + z));
                    }
                }
            }
            
            // --- STEP 2: Find all existing chunks and decide whether to destroy or keep them ---
            var existingChunks = new NativeHashSet<int3>(1000, Allocator.Temp);
            foreach (var (chunkPosition, entity) in SystemAPI.Query<RefRO<ChunkPosition>>().WithEntityAccess())
            {
                var pos = chunkPosition.ValueRO.Value;
                if (requiredChunks.Contains(pos))
                {
                    existingChunks.Add(pos);
                }
                else
                {
                    ecb.DestroyEntity(entity);
                }
            }

            // --- STEP 3: "Diff" the sets. Create any required chunks that don't already exist. ---
            foreach (var requiredPos in requiredChunks)
            {
                if (existingChunks.Contains(requiredPos)) continue;
                
                var newChunkEntity = ecb.CreateEntity();
                
                int3 worldPosition = requiredPos * 32;
                
                ecb.AddComponent(newChunkEntity, new ChunkPosition { Value = requiredPos });
                ecb.AddComponent(newChunkEntity, new LocalTransform { Position = worldPosition, Rotation = quaternion.identity, Scale = 1 });
                ecb.AddComponent(newChunkEntity, new ChunkVoxels{ Voxels = new NativeArray<Voxel>(32 * 32 * 32, Allocator.Persistent) });
                    
                ecb.AddComponent<IsChunkTerrainGenerating>(newChunkEntity);
                ecb.SetComponentEnabled<IsChunkTerrainGenerating>(newChunkEntity, false);

                ecb.AddComponent<ChunkHasVoxelData>(newChunkEntity);
                ecb.SetComponentEnabled<ChunkHasVoxelData>(newChunkEntity, false);

                ecb.AddComponent<IsChunkMeshGenerating>(newChunkEntity);
                ecb.SetComponentEnabled<IsChunkMeshGenerating>(newChunkEntity, false);

                ecb.AddComponent<ChunkHasMesh>(newChunkEntity);
                ecb.SetComponentEnabled<ChunkHasMesh>(newChunkEntity, false);
                    
                //ecb.AddBuffer<NeighbouringChunks>(newChunkEntity);
                
                ecb.AddComponent<TerrainJobHandle>(newChunkEntity);
                ecb.AddComponent<MeshJobHandle>(newChunkEntity);
                ecb.AddComponent<ChunkMeshRenderData>(newChunkEntity);
                ecb.AddComponent(newChunkEntity, new VoxelStateMap { Map = new NativeHashMap<int, Entity>(0, Allocator.Persistent)});

                /*NativeList<int3> neighbourPositions = new NativeList<int3>(6, Allocator.Temp);
                neighbourPositions.Add(requiredPos + new int3(1, 0, 0));
                neighbourPositions.Add(requiredPos + new int3(-1, 0, 0));
                neighbourPositions.Add(requiredPos + new int3(0, 1, 0));
                neighbourPositions.Add(requiredPos + new int3(0, -1, 0));
                neighbourPositions.Add(requiredPos + new int3(0, 0, 1));
                neighbourPositions.Add(requiredPos + new int3(0, 0, -1));

                foreach (var pos in neighbourPositions)
                {
                    if (allChunks.Chunks.TryGetValue(pos, out var neighbour))
                    {
                        ecb.AppendToBuffer(neighbour, new NeighbouringChunks { Neighbour = newChunkEntity, Position = -pos});
                    }
                }
                
                allChunks.Chunks.Add(requiredPos, newChunkEntity);*/
            }
            
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            requiredChunks.Dispose();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            foreach (var (chunkVoxels, voxelStateMap) in SystemAPI.Query<RefRW<ChunkVoxels>, RefRW<VoxelStateMap>>())
            {
                chunkVoxels.ValueRW.Voxels.Dispose();
                voxelStateMap.ValueRW.Map.Dispose();
            }
        }
    }
    
}