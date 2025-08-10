using Unity.Burst;
using Unity.Collections;
using Unity.Collections.NotBurstCompatible;
using Unity.Entities;
using Unity.Entities.Graphics;
using Unity.Mathematics;
using Unity.Rendering; // Required for RenderMesh, RenderBounds etc.
using Unity.Transforms; // Required for LocalToWorld
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
            // Create a command buffer to make structural changes (add/remove components)
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var material = MaterialHolder.ChunkMaterial;

            // Query for all chunks that have generated mesh data but have not yet been finalized
            // and had their rendering components added.
            foreach (var (chunkRenderData, meshJobHandle, chunkHasMesh, chunkIsGeneratingMesh, entity) in SystemAPI
                         .Query<RefRO<ChunkMeshRenderData>, RefRW<MeshJobHandle>, EnabledRefRW<ChunkHasMesh>, EnabledRefRO<IsChunkMeshGenerating>>()
                         .WithDisabled<ChunkHasMesh>()
                         .WithEntityAccess())
            {
                // First, check if the meshing job for this specific chunk is complete.
                // If not, we skip it and will check again next frame.
                if (!meshJobHandle.ValueRO.Value.IsCompleted)
                {
                    continue;
                }
                
                meshJobHandle.ValueRW.Value.Complete();
                
                var vertices = chunkRenderData.ValueRO.Vertices;
                var triangles = chunkRenderData.ValueRO.Triangles;

                // Create a new UnityEngine.Mesh object
                var mesh = new Mesh
                {
                    // Use MeshUpdateFlags to prevent recalculating anything until all data is set
                    name = "VoxelChunkMesh"
                };
                
                // Use SetVertices and SetTriangles with NativeLists to be highly efficient.
                // This avoids creating intermediate C# arrays.
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.ToArrayNBC(), 0, false); // Submesh 0, don't calculate bounds yet

                // Recalculate normals and bounds now that the data is set
                mesh.RecalculateNormals();
                mesh.RecalculateBounds(); // This is crucial for the RenderBounds component
                
                var desc = new RenderMeshDescription(
                    shadowCastingMode: ShadowCastingMode.Off);

                var renderMeshArray = new RenderMeshArray(new [] { material }, new [] { mesh });
        
                RenderMeshUtility.AddComponents(
                    entity,
                    state.EntityManager,
                    desc,
                    renderMeshArray,
                    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
                
                // --- CRITICAL CLEANUP ---
                // Dispose the native lists to prevent memory leaks.
                vertices.Dispose();
                triangles.Dispose();

                chunkHasMesh.ValueRW = true;
            }
            
            // Play back the commands from the ECB to apply the component changes.
            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}