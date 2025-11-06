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

namespace Voxels
{
    /// <summary>
    /// Runtime store for finalized Mesh objects per-chunk.
    /// This is a simple process-local cache that maps Entity -> Mesh[] (index = LOD level).
    /// It intentionally uses managed types (Dictionary) and must be accessed on main thread only.
    /// </summary>
    public static class ChunkMeshStore
    {
        // Mesh array length must be at least 1; we allow up to 4 LODs (0..3).
        private static readonly Dictionary<Entity, Mesh[]> store = new Dictionary<Entity, Mesh[]>();

        public static void SetMeshes(Entity e, Mesh[] meshes)
        {
            // Replace existing meshes (dispose old managed meshes if desired).
            if (store.TryGetValue(e, out var old))
            {
                // Dispose old Meshes to free GPU memory.
                if (old != null)
                {
                    for (int i = 0; i < old.Length; i++)
                        if (old[i] != null) UnityEngine.Object.Destroy(old[i]);
                }
            }

            store[e] = meshes;
        }

        public static bool TryGetMesh(Entity e, int lod, out Mesh mesh)
        {
            mesh = null;
            if (!store.TryGetValue(e, out var arr) || arr == null) return false;
            if (lod < 0) lod = 0;
            if (lod >= arr.Length) lod = arr.Length - 1;
            mesh = arr[lod];
            return mesh != null;
        }

        public static bool HasAnyMesh(Entity e)
        {
            return store.TryGetValue(e, out var arr) && arr != null && arr.Length > 0;
        }

        public static void Remove(Entity e)
        {
            if (!store.TryGetValue(e, out var arr)) return;
            if (arr != null)
            {
                for (int i = 0; i < arr.Length; i++)
                    if (arr[i] != null) UnityEngine.Object.Destroy(arr[i]);
            }
            store.Remove(e);
        }
    }
    
    // A temporary command struct to hold all the data needed for one chunk's finalization.
    // This allows us to separate reading data from writing structural changes.
    public class FinalizeMeshCommand
    {
        public Entity Entity;
        public int LodLevel; // 0..N
        public Mesh ChunkMesh;
        public RenderMeshArray RenderMeshArray;
        public RenderMeshDescription RenderDescription;
        public MaterialMeshInfo MaterialMeshInfo;
        public int3 Position;

        // We also need to own the native lists so we can dispose them later
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        
        public NativeList<float2> Uvs0;
        public NativeList<float2> Uvs1;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ChunkMeshingSystem))]
    public partial struct ChunkMeshFinalizerSystem : ISystem
    {
        private const int MAX_LODS = 4;
        public void OnCreate(ref SystemState state)
        {
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(World.DefaultGameObjectInjectionWorld.UpdateAllocator.ToAllocator);
            //var waterEcb = new EntityCommandBuffer(World.DefaultGameObjectInjectionWorld.UpdateAllocator.ToAllocator);
            var material = MaterialHolder.ChunkMaterial;
            //var waterMaterial = MaterialHolder.WaterMaterial;
            var commandList = new List<FinalizeMeshCommand>();

            int maxMeshesToCreateThisFrame = 4;
            int meshesCreatedThisFrame = 0;
            
            // =================================================================================
            //  PHASE 1: READ & PREPARE
            //  Iterate over entities, read data, and prepare commands. No structural changes here.
            // =================================================================================
            
            
            // Query chunks with LOD meshes; they should have a ChunkLODMeshes component and MeshJobHandle
            foreach (var (entity, chunkPos, chunkLODMeshes, meshJobHandle, isGenerating) in SystemAPI
                         .Query<Entity, RefRO<ChunkPosition>, RefRW<ChunkLODMeshes>, RefRW<MeshJobHandle>, EnabledRefRW<IsChunkMeshGenerating>>()
                         .WithEntityAccess())
            {
                // For each LOD level check if there is a job handle for that LOD and if it's completed.
                // For each completed LOD produce a FinalizeMeshCommand to convert the NativeLists into a UnityEngine.Mesh
                Mesh[] meshesToSet = null;
                bool anyNewMeshThisEntity = false;

                for (int lod = 0; lod < MAX_LODS; lod++)
                {
                    // check the appropriate handle and the mesh lists for that LOD
                    JobHandle lodHandle = default;
                    NativeList<float3> verts = default;
                    NativeList<int> tris = default;
                    NativeList<float2> uvs0 = default;
                    NativeList<float2> uvs1 = default;

                    // Grab the handle+lists depending on lod index
                    switch (lod)
                    {
                        case 0:
                            lodHandle = meshJobHandle.ValueRO.TerrainMeshHandle0;
                            verts = chunkLODMeshes.ValueRO.LOD0.Vertices;
                            tris = chunkLODMeshes.ValueRO.LOD0.Triangles;
                            uvs0 = chunkLODMeshes.ValueRO.LOD0.UVs0;
                            uvs1 = chunkLODMeshes.ValueRO.LOD0.UVs1;
                            break;
                        case 1:
                            lodHandle = meshJobHandle.ValueRO.TerrainMeshHandle1;
                            verts = chunkLODMeshes.ValueRO.LOD1.Vertices;
                            tris = chunkLODMeshes.ValueRO.LOD1.Triangles;
                            uvs0 = chunkLODMeshes.ValueRO.LOD1.UVs0;
                            uvs1 = chunkLODMeshes.ValueRO.LOD1.UVs1;
                            break;
                        case 2:
                            lodHandle = meshJobHandle.ValueRO.TerrainMeshHandle2;
                            verts = chunkLODMeshes.ValueRO.LOD2.Vertices;
                            tris = chunkLODMeshes.ValueRO.LOD2.Triangles;
                            uvs0 = chunkLODMeshes.ValueRO.LOD2.UVs0;
                            uvs1 = chunkLODMeshes.ValueRO.LOD2.UVs1;
                            break;
                        case 3:
                            lodHandle = meshJobHandle.ValueRO.TerrainMeshHandle3;
                            verts = chunkLODMeshes.ValueRO.LOD3.Vertices;
                            tris = chunkLODMeshes.ValueRO.LOD3.Triangles;
                            uvs0 = chunkLODMeshes.ValueRO.LOD3.UVs0;
                            uvs1 = chunkLODMeshes.ValueRO.LOD3.UVs1;
                            break;
                    }

                    // If the handle is not valid (default) we skip; if the handle is still running skip as well.
                    // If lists are empty (Length == 0) that means job produced no vertices (empty mesh)
                    if (lodHandle.Equals(default(JobHandle)) || !lodHandle.IsCompleted)
                        continue;

                    // If the lists are not created or already consumed (Length == 0 or not created), skip
                    if (!verts.IsCreated || verts.Length == 0)
                    {
                        // If created but empty, dispose them and mark as done
                        if (verts.IsCreated)
                        {
                            verts.Dispose();
                            tris.Dispose();
                            if (uvs0.IsCreated) uvs0.Dispose();
                            if (uvs1.IsCreated) uvs1.Dispose();
                        }
                        // clear the corresponding lists in chunkLODMeshes (writeback below)
                        anyNewMeshThisEntity = true;
                        continue;
                    }

                    // At this point we have a completed job and a populated NativeList -> create a finalize command.
                    var cmd = new FinalizeMeshCommand()
                    {
                        Entity = entity,
                        LodLevel = lod,
                        ChunkMesh = null, // will be constructed below on main thread
                        Material = chunkMaterial,
                        Vertices = verts,
                        Triangles = tris,
                        Uvs0 = uvs0,
                        Uvs1 = uvs1,
                        Position = chunkPos.Value * 32
                    };

                    if (!finalizeCommandsPerEntity.TryGetValue(entity, out var list))
                    {
                        list = new List<FinalizeMeshCommand>(4);
                        finalizeCommandsPerEntity.Add(entity, list);
                    }
                    list.Add(cmd);
                    anyNewMeshThisEntity = true;
                }

                // If we found new meshes for this entity, we should stop marking generating flag only after finalization.
                // (We'll update ChunkLODMeshes to clear the NativeLists after creating meshes.)
            }

            // =================================================================================
            // PHASE 2: Finalize meshes on main thread and apply structural changes
            // =================================================================================
            foreach (var kv in finalizeCommandsPerEntity)
            {
                var entity = kv.Key;
                var cmdList = kv.Value;

                // Each command in cmdList corresponds to a single LOD for this entity.
                // We'll create meshes array sized to maxLOD (4) and insert created Mesh objects at indices
                Mesh[] meshArr = new Mesh[MAX_LODS];

                foreach (var cmd in cmdList)
                {
                    // Construct managed mesh from NB native lists
                    var mesh = new Mesh { name = $"Chunk_{entity.Index}_LOD{cmd.LodLevel}" };

                    // Convert and assign vertices & triangles safely
                    // Use AsArray where possible to avoid temporary managed copies for vertices/uvs.
                    // Triangles require an int[]; use ToArrayNBC() is available via Unity.Collections.NotBurstCompatible
                    // but to keep compatibility we use CopyTo/ToArray via Unsafe.

                    // Vertices
                    var vertsArr = cmd.Vertices.AsArray(); // NativeArray<float3>
                    var vertsList = new Vector3[vertsArr.Length];
                    for (int i = 0; i < vertsArr.Length; i++)
                        vertsList[i] = new Vector3(vertsArr[i].x, vertsArr[i].y, vertsArr[i].z);
                    mesh.SetVertices(vertsList);

                    // Triangles
                    var triArr = cmd.Triangles.AsArray();
                    var triInt = new int[triArr.Length];
                    for (int i = 0; i < triArr.Length; i++) triInt[i] = triArr[i];
                    mesh.SetTriangles(triInt, 0, false);

                    // UV0
                    if (cmd.Uvs0.IsCreated)
                    {
                        var u0 = cmd.Uvs0.AsArray();
                        var uv0arr = new Vector2[u0.Length];
                        for (int i = 0; i < u0.Length; i++) uv0arr[i] = new Vector2(u0[i].x, u0[i].y);
                        mesh.SetUVs(0, uv0arr);
                    }

                    // UV1
                    if (cmd.Uvs1.IsCreated)
                    {
                        var u1 = cmd.Uvs1.AsArray();
                        var uv1arr = new Vector2[u1.Length];
                        for (int i = 0; i < u1.Length; i++) uv1arr[i] = new Vector2(u1[i].x, u1[i].y);
                        mesh.SetUVs(1, uv1arr);
                    }

                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(markNoLongerReadable: false); // keep readable so we can create collider

                    // store in mesh array
                    meshArr[cmd.LodLevel] = mesh;

                    // Dispose native lists now that data is in the managed Mesh
                    cmd.Vertices.Dispose();
                    cmd.Triangles.Dispose();
                    if (cmd.Uvs0.IsCreated) cmd.Uvs0.Dispose();
                    if (cmd.Uvs1.IsCreated) cmd.Uvs1.Dispose();
                }

                // Merge new meshes with any existing cached ones for this entity.
                // If the entity already had cached meshes, replace indices where we created new ones.
                if (ChunkMeshStore.TryGetMesh(entity, 0, out _))
                {
                    // existing meshes present -> get them from store and replace
                    Mesh[] existing = new Mesh[MAX_LODS];
                    // pull each index, prefer new mesh if present
                    for (int i = 0; i < MAX_LODS; i++)
                    {
                        if (meshArr[i] != null)
                            existing[i] = meshArr[i];
                        else
                        {
                            if (ChunkMeshStore.TryGetMesh(entity, i, out var m)) existing[i] = m;
                        }
                    }
                    ChunkMeshStore.SetMeshes(entity, existing);
                }
                else
                {
                    // No existing cached meshes -> set new array (fill missing entries with null)
                    ChunkMeshStore.SetMeshes(entity, meshArr);
                }

                // If the entity does not yet have render components, add them now using the best available mesh (LOD0 -> LOD1 -> ...)
                // We check for RenderMesh by trying to read RenderMeshArray component presence via EntityManager.
                if (!state.EntityManager.HasComponent<RenderMeshArray>(entity))
                {
                    // pick highest-detail mesh available
                    Mesh chosenMesh = null;
                    int chosenIndex = -1;
                    for (int i = 0; i < MAX_LODS; i++)
                    {
                        if (meshArr[i] != null)
                        {
                            chosenMesh = meshArr[i];
                            chosenIndex = i;
                            break;
                        }
                    }
                    if (chosenMesh != null)
                    {
                        var renderDesc = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
                        var renderMeshArray = new RenderMeshArray(new[] { chunkMaterial }, new[] { chosenMesh });
                        var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

                        // Structural change: add render components
                        RenderMeshUtility.AddComponents(
                            entity,
                            state.EntityManager,
                            in renderDesc,
                            renderMeshArray,
                            materialMeshInfo);

                        // Add transform & optionally collider for LOD0 if available
                        ecb.AddComponent(entity, new LocalTransform
                        {
                            Position = state.EntityManager.GetComponentData<ChunkPosition>(entity).Value * 32,
                            Rotation = quaternion.identity,
                            Scale = 1f
                        });

                        // Create physics collider if LOD0 exists
                        if (meshArr[0] != null && meshArr[0].vertexCount > 0)
                        {
                            var physicsMaterial = Unity.Physics.Material.Default;
                            var filter = CollisionFilter.Default;
                            var colliderBlob = Unity.Physics.MeshCollider.Create(meshArr[0], filter, physicsMaterial);
                            ecb.AddComponent(entity, new PhysicsCollider { Value = colliderBlob });
                        }

                        // Mark that chunk has mesh
                        if (!state.EntityManager.HasComponent<ChunkHasMesh>(entity))
                            ecb.AddComponent(entity, new ChunkHasMesh());
                    }
                }
                else
                {
                    // If the entity already had render components, we don't change them here.
                    // The LOD selection system will swap the RenderMesh's mesh when needed.
                }

                // After finalization, mark IsChunkMeshGenerating = false (the chunk now has its meshes cached)
                if (state.EntityManager.HasComponent<IsChunkMeshGenerating>(entity))
                {
                    ecb.RemoveComponent<IsChunkMeshGenerating>(entity);
                }
            }

            /*// Query for finished water mesh jobs
            foreach (var (waterRenderData, meshJobHandle, chunkWaterMesh, isWaterMeshGenerating, entity) in SystemAPI
                        .Query<RefRW<ChunkWaterMeshRenderData>, RefRO<MeshJobHandle>, RefRO<ChunkWaterMesh>, EnabledRefRW<IsChunkWaterMeshGenerating>>()
                        .WithAll<IsChunkWaterMeshGenerating>()
                        .WithEntityAccess())
            {
                if (!meshJobHandle.ValueRO.WaterMeshHandle.IsCompleted)
                    continue;

                var vertices = waterRenderData.ValueRO.Vertices;
                var triangles = waterRenderData.ValueRO.Triangles;
                var uvs = waterRenderData.ValueRO.UVs;
                if (vertices.Length == 0)
                {
                    // No water mesh to create, just clean up
                    vertices.Dispose();
                    waterRenderData.ValueRW.Triangles.Dispose();
                    waterRenderData.ValueRW.UVs.Dispose();
                    isWaterMeshGenerating.ValueRW = false;
                    waterEcb.SetComponentEnabled<ChunkHasWaterMesh>(entity, true);
                    continue;
                }

                var mesh = new Mesh { name = "VoxelWaterMesh" };
                mesh.SetVertices(vertices.AsArray());
                mesh.SetTriangles(triangles.ToArrayNBC(), 0, false);
                mesh.SetUVs(0, uvs.AsArray());
                mesh.RecalculateNormals(); // Important for lighting/reflections
                mesh.RecalculateBounds();

                var renderDescription = new RenderMeshDescription(ShadowCastingMode.Off, receiveShadows: false);
                var renderMeshArray = new RenderMeshArray(new[] { waterMaterial }, new[] { mesh });
                var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

                var newCommand = new FinalizeMeshCommand()
                {
                    ChunkMesh = mesh,
                    Entity = chunkWaterMesh.ValueRO.Value,
                    MaterialMeshInfo = materialMeshInfo,
                    RenderDescription = renderDescription,
                    RenderMeshArray = renderMeshArray,
                    Position = SystemAPI.GetComponent<ChunkPosition>(entity).Value * 32,
                    Vertices = vertices,
                    Triangles = triangles,
                    Uvs0 = uvs,
                    Uvs1 = default,
                };
                
                commandList.Add(newCommand);

                // Mark as complete
                waterEcb.SetComponentEnabled<ChunkHasWaterMesh>(entity, true);
                isWaterMeshGenerating.ValueRW = false;
            }
            
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
                
                ecb.AddComponent(command.Entity, new LocalTransform
                {
                    Position = command.Position,
                    Rotation = quaternion.identity,
                    Scale = 1,
                });

                // 4. Clean up native collections now that they've been used for the mesh and collider.
                command.Vertices.Dispose();
                command.Triangles.Dispose();
                command.Uvs0.Dispose();
                if (command.Uvs1.IsCreated) command.Uvs1.Dispose(); // Safe for water
            }*/

            // Finally, play back all the queued changes from the ECB.
            ecb.Playback(state.EntityManager);
            //waterEcb.Playback(state.EntityManager);
        }
    }
}
