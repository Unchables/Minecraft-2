using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms; // Required for RenderMesh, RenderBounds etc.
using UnityEngine; // Required for creating Mesh objects
using UnityEngine.Rendering;
using MeshCollider = UnityEngine.MeshCollider; // Required for SubMeshDescriptor

namespace Voxels
{
    [UpdateInGroup(typeof(SimulationSystemGroup))] // Run in the presentation group, after simulation
    [UpdateAfter(typeof(ChunkMeshingSystem))]
    public partial struct ChunkMeshFinalizerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<WorldSettings>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var material = MaterialHolder.ChunkMaterial;

            int maxMeshesToCreateThisFrame = 2;
            int meshesCreatedThisFrame = 0;
            
            List<AddRenderMeshCommand> addRenderComponentsCommands = new List<AddRenderMeshCommand>();
            
            foreach (var (chunkPos, chunkRenderData, meshJobHandle, chunkHasMesh, chunkIsGeneratingMesh, entity) in SystemAPI
                         .Query<RefRO<ChunkPosition>, RefRO<ChunkMeshRenderData>, RefRW<MeshJobHandle>, EnabledRefRW<ChunkHasMesh>, EnabledRefRO<IsChunkMeshGenerating>>()
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

                var mesh = new Mesh
                {
                    name = "VoxelChunkMesh"
                };
                
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.ToArrayNBC(), 0, false);
                mesh.SetUVs(0, uvs.AsArray());

                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                
                var desc = new RenderMeshDescription(
                    shadowCastingMode: ShadowCastingMode.Off);

                var renderMeshArray = new RenderMeshArray(new [] { material }, new [] { mesh });
        
                addRenderComponentsCommands.Add(new AddRenderMeshCommand
                {
                    Desc = desc,
                    Entity = entity,
                    RenderMeshArray = renderMeshArray,
                    MatMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0),
                    
                    Vertices = vertices,
                    Triangles = triangles,
                });

                // Define the physics material properties.
                var physicsMaterial = Unity.Physics.Material.Default;
                physicsMaterial.Friction = 0.6f;
                physicsMaterial.Restitution = 0.1f;

                // This is the core step: "Bake" the vertices and triangles into an
                // immutable, high-performance BlobAssetReference<Collider>.
                var colliderBlob = Unity.Physics.MeshCollider.Create(mesh, CollisionFilter.Default, physicsMaterial);
                    
                // Add the PhysicsCollider component to the entity.
                // This component just holds a reference to the collider blob asset.
                ecb.AddComponent(entity, new PhysicsCollider { Value = colliderBlob });
                
                ecb.AddSharedComponent(entity, new PhysicsWorldIndex { Value = 0 });
                
                ecb.AddComponent(entity, new LocalTransform
                {
                    Position = chunkPos.ValueRO.Value * 32,
                    Rotation = quaternion.identity,
                    Scale = 1
                });
                
                chunkHasMesh.ValueRW = true;
            }
            
            if(meshesCreatedThisFrame == 0) SystemAPI.SetComponentEnabled<FinishedInitialGeneration>(SystemAPI.GetSingletonEntity<WorldSettings>(), true);

            foreach (var command in addRenderComponentsCommands)
            {
                RenderMeshUtility.AddComponents(
                        command.Entity,
                        state.EntityManager,
                        command.Desc,
                        command.RenderMeshArray,
                        command.MatMeshInfo);

                command.Vertices.Dispose();
                command.Triangles.Dispose();
            }
            
            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }

    public class AddRenderMeshCommand
    {
        public Entity Entity;
        public RenderMeshDescription Desc;
        public RenderMeshArray RenderMeshArray;
        public MaterialMeshInfo MatMeshInfo;
        
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
    }
}