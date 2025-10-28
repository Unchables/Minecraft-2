using InfinityCode.UltimateEditorEnhancer.EditorMenus.Layouts;
using Player;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Voxels
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ChunkLoadAndUnloader : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AllChunks>();
            state.RequireForUpdate<WorldSettings>();
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var worldEntity = SystemAPI.GetSingletonEntity<WorldSettings>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var worldSettings = SystemAPI.GetSingleton<WorldSettings>();
            var allChunks = SystemAPI.GetSingleton<AllChunks>();
            
            int3 currentPlayerChunkPos = (int3)math.round(SystemAPI.GetComponent<LocalTransform>(playerEntity).Position / worldSettings.ChunkSize);
            
            EntityCommandBuffer ecb = new EntityCommandBuffer(Allocator.TempJob);

            int chunkLoadRadius = worldSettings.ChunkLoadRadius + 1;
            int renderChunkRadius = worldSettings.ChunkLoadRadius;
            
            // --- STEP 1: Build a HashSet of all chunks that are required ---
            var requiredChunks = new NativeList<int3>(Allocator.Temp);
            for (int x = -chunkLoadRadius; x <= chunkLoadRadius; x++)
            {
                for (int y = -chunkLoadRadius; y <= chunkLoadRadius; y++)
                {
                    for (int z = -chunkLoadRadius; z <= chunkLoadRadius; z++)
                    {
                        if (x * x + y * y + z * z > chunkLoadRadius * chunkLoadRadius) continue;
                        requiredChunks.Add(new int3(currentPlayerChunkPos.x + x, currentPlayerChunkPos.y + y, currentPlayerChunkPos.z + z));
                    }
                }
            }
            
            // --- STEP 2: Find all existing chunks and decide whether to destroy or keep them ---
            var existingChunks = new NativeHashSet<int3>(1000, Allocator.Temp);
            foreach (var (chunkPosition, generateChunkMesh, entity) in SystemAPI.Query<RefRO<ChunkPosition>, EnabledRefRW<GenerateChunkMesh>>().WithPresent<GenerateChunkMesh>().WithEntityAccess())
            {
                var pos = chunkPosition.ValueRO.Value;
                if (requiredChunks.Contains(pos))
                {
                    existingChunks.Add(pos);

                    if (!generateChunkMesh.ValueRO)
                    {
                        
                        float distanceSq = math.distancesq(pos, currentPlayerChunkPos);
                        bool generateMesh = distanceSq <= renderChunkRadius * renderChunkRadius;
                        
                        generateChunkMesh.ValueRW = generateMesh;
                    }
                }
                else
                {
                    ecb.DestroyEntity(entity);
                    allChunks.Chunks.Remove(chunkPosition.ValueRO.Value);
                }
            }

            // --- STEP 3: "Diff" the sets. Create any required chunks that don't already exist. ---
            foreach (var requiredPos in requiredChunks)
            {
                if (existingChunks.Contains(requiredPos)) continue;
                if (allChunks.Chunks.ContainsKey(requiredPos)) continue;
                
                var newChunkEntity = ecb.CreateEntity();
                ecb.SetName(newChunkEntity, requiredPos.ToString());
                
                int3 worldPosition = requiredPos * 32;
                
                ecb.AddComponent(newChunkEntity, new ChunkPosition { Value = requiredPos });
                ecb.AddComponent(newChunkEntity, new LocalTransform { Position = worldPosition, Rotation = quaternion.identity, Scale = 1 });
                ecb.AddComponent(newChunkEntity, new ChunkVoxels{ Voxels = new NativeArray<Voxel>((int)math.pow(Chunk.ChunkSize, 3), Allocator.Persistent) });
                
                ecb.AddComponent<IsChunkTerrainGenerating>(newChunkEntity);
                ecb.SetComponentEnabled<IsChunkTerrainGenerating>(newChunkEntity, false);

                ecb.AddComponent<ChunkHasVoxelData>(newChunkEntity);
                ecb.SetComponentEnabled<ChunkHasVoxelData>(newChunkEntity, false);

                ecb.AddComponent<IsChunkMeshGenerating>(newChunkEntity);
                ecb.SetComponentEnabled<IsChunkMeshGenerating>(newChunkEntity, false);

                ecb.AddComponent<ChunkHasMesh>(newChunkEntity);
                ecb.SetComponentEnabled<ChunkHasMesh>(newChunkEntity, false);

                ecb.AddComponent<ChunkAddedToAllChunks>(newChunkEntity);
                ecb.SetComponentEnabled<ChunkAddedToAllChunks>(newChunkEntity, false);

                bool generateMesh =
                    requiredPos.x * requiredPos.x + requiredPos.y * requiredPos.y + requiredPos.z * requiredPos.z <=
                    renderChunkRadius * renderChunkRadius;
                ecb.AddComponent<GenerateChunkMesh>(newChunkEntity);
                ecb.SetComponentEnabled<GenerateChunkMesh>(newChunkEntity, generateMesh);
                
                ecb.AddComponent<TerrainJobHandle>(newChunkEntity);
                ecb.AddComponent<MeshJobHandle>(newChunkEntity);
                ecb.AddComponent<ChunkMeshRenderData>(newChunkEntity);
                ecb.AddComponent(newChunkEntity, new VoxelStateMap { Map = new NativeHashMap<int, Entity>(0, Allocator.Persistent)});
            }

            ecb.Playback(state.EntityManager);
            
            foreach (var (position, voxels, isAdded) in SystemAPI.Query<RefRO<ChunkPosition>, RefRO<ChunkVoxels>, EnabledRefRW<ChunkAddedToAllChunks>>().WithDisabled<ChunkAddedToAllChunks>())
            {
                if (!isAdded.ValueRO)
                {
                    allChunks.Chunks.Add(position.ValueRO.Value, voxels.ValueRO);
                    isAdded.ValueRW = true;
                }
            }
            
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