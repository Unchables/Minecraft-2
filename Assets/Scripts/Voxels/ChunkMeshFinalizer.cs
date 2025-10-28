// ---- FILE: ChunkMeshFinalizerSystem.cs ----

using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Graphics; // This namespace is required
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using Material = UnityEngine.Material;

namespace Voxels
{
    // A temporary command struct to hold all the data needed for one chunk's finalization.
    // This allows us to separate reading data from writing structural changes.
    public class FinalizeMeshCommand
    {
        public Entity Entity;
        public Mesh ChunkMesh;
        public RenderMeshArray RenderMeshArray;
        public RenderMeshDescription RenderDescription;
        public MaterialMeshInfo MaterialMeshInfo;

        // We also need to own the native lists so we can dispose them later
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkMeshingSystem))]
    public partial struct ChunkMeshFinalizerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(World.DefaultGameObjectInjectionWorld.UpdateAllocator.ToAllocator);
            var material = MaterialHolder.ChunkMaterial;
            var commandList = new List<FinalizeMeshCommand>();

            int maxMeshesToCreateThisFrame = 4;
            int meshesCreatedThisFrame = 0;
            
            // =================================================================================
            //  PHASE 1: READ & PREPARE
            //  Iterate over entities, read data, and prepare commands. No structural changes here.
            // =================================================================================
            
            foreach (var (chunkPos, chunkRenderData, meshJobHandle, chunkHasMesh, chunkIsGeneratingMesh, chunkVoxels, entity) in SystemAPI
                         .Query<RefRO<ChunkPosition>, RefRO<ChunkMeshRenderData>, RefRW<MeshJobHandle>, EnabledRefRW<ChunkHasMesh>, EnabledRefRO<IsChunkMeshGenerating>, RefRO<ChunkVoxels>>()
                         .WithDisabled<ChunkHasMesh>()
                         .WithEntityAccess())
            {
                meshesCreatedThisFrame++;
                if (meshesCreatedThisFrame > maxMeshesToCreateThisFrame)
                    break;
                
                if (!meshJobHandle.ValueRO.Value.IsCompleted)
                    continue;
                
                //meshJobHandle.ValueRW.Value.Complete();
                
                var vertices = chunkRenderData.ValueRO.Vertices;
                var triangles = chunkRenderData.ValueRO.Triangles;
                var uvs = chunkRenderData.ValueRO.UVs;
                
                var mesh = new Mesh { name = "VoxelChunkMesh" };
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.ToArrayNBC(), 0, false);
                mesh.SetUVs(0, uvs.AsArray());
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                
                // Prepare all the data needed for the command
                var command = new FinalizeMeshCommand
                {
                    Entity = entity,
                    ChunkMesh = mesh,
                    RenderDescription = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true),
                    RenderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh }),
                    MaterialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0),
                    Vertices = vertices,
                    Triangles = triangles
                };
                
                commandList.Add(command);
                
                chunkHasMesh.ValueRW = true;
            }
            
            // =================================================================================
            //  PHASE 2: WRITE & EXECUTE
            //  Now that iteration is finished, we can safely perform structural changes.
            // =================================================================================
            foreach (var command in commandList)
            {
                // 1. Use the high-level utility to add all standard rendering components.
                //    This is a complex structural change that uses the EntityManager directly.
                RenderMeshUtility.AddComponents(
                    command.Entity,
                    state.EntityManager,
                    in command.RenderDescription,
                    command.RenderMeshArray,
                    command.MaterialMeshInfo);

                // 3. Queue up simple component additions and changes in the ECB.
                var physicsMaterial = Unity.Physics.Material.Default;
                var colliderBlob = Unity.Physics.MeshCollider.Create(command.ChunkMesh, CollisionFilter.Default, physicsMaterial);
                ecb.AddComponent(command.Entity, new PhysicsCollider { Value = colliderBlob });
                
                ecb.AddComponent(command.Entity, new LocalTransform
                {
                    Position = SystemAPI.GetComponent<ChunkPosition>(command.Entity).Value * 32,
                    Rotation = quaternion.identity,
                    Scale = 1
                });

                // 4. Clean up native collections now that they've been used for the mesh and collider.
                command.Vertices.Dispose();
                command.Triangles.Dispose();
            }

            // Finally, play back all the queued changes from the ECB.
            ecb.Playback(state.EntityManager);
        }
    }
}
