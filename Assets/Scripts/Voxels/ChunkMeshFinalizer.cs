using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms; // Required for RenderMesh, RenderBounds etc.
using UnityEngine; // Required for creating Mesh objects
using UnityEngine.Rendering; // Required for SubMeshDescriptor

namespace Voxels
{
    [UpdateInGroup(typeof(SimulationSystemGroup))] // Run in the presentation group, after simulation
    [UpdateAfter(typeof(ChunkMeshingSystem))]
    public partial struct ChunkMeshFinalizerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var material = MaterialHolder.ChunkMaterial;
            
            List<AddRenderMeshCommand> addRenderComponentsCommands = new List<AddRenderMeshCommand>();
            
            foreach (var (chunkPos, chunkRenderData, meshJobHandle, chunkHasMesh, chunkIsGeneratingMesh, entity) in SystemAPI
                         .Query<RefRO<ChunkPosition>, RefRO<ChunkMeshRenderData>, RefRW<MeshJobHandle>, EnabledRefRW<ChunkHasMesh>, EnabledRefRO<IsChunkMeshGenerating>>()
                         .WithDisabled<ChunkHasMesh>()
                         .WithEntityAccess())
            {
                if (!meshJobHandle.ValueRO.Value.IsCompleted)
                    continue;
                
                meshJobHandle.ValueRW.Value.Complete();
                
                var vertices = chunkRenderData.ValueRO.Vertices;
                var triangles = chunkRenderData.ValueRO.Triangles;

                var mesh = new Mesh
                {
                    name = "VoxelChunkMesh"
                };
                
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.ToArrayNBC(), 0, false);

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

                ecb.AddComponent(entity, new LocalTransform
                {
                    Position = chunkPos.ValueRO.Value * 32,
                    Rotation = quaternion.identity,
                    Scale = 1
                });
                
                chunkHasMesh.ValueRW = true;
            }

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