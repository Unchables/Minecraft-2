using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering; // Required for rendering components
using Unity.Transforms; // Required for LocalToWorld
using UnityEngine;
using UnityEngine.Rendering; // Required for Mesh, Material, etc.

// We remove [BurstCompile] from the struct because OnCreate deals with managed objects.
public partial struct TestingSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        //return;
        var entity = state.EntityManager.CreateEntity();

        state.EntityManager.SetName(entity, "hello");
        
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = Color.white;
        
        var mesh = new Mesh
        {
            name = "ProceduralTestQuad"
        };

        // Define the vertices (corners) of the quad
        var vertices = new Vector3[4]
        {
            new Vector3(-0.5f, -0.5f, 0), // Bottom-left
            new Vector3(0.5f, -0.5f, 0),  // Bottom-right
            new Vector3(-0.5f, 0.5f, 0),  // Top-left
            new Vector3(0.5f, 0.5f, 0)   // Top-right
        };
        mesh.vertices = vertices;

        // Define the triangles. A quad is made of two triangles.
        // The order of vertices matters (winding order) for which side is visible.
        // Unity uses a counter-clockwise order for front faces.
        var triangles = new int[6]
        {
            0, 2, 1, // First triangle (bottom-left, top-left, bottom-right)
            2, 3, 1  // Second triangle (top-left, top-right, bottom-right)
        };
        mesh.triangles = triangles;
        
        // It's good practice to recalculate these after setting vertices/triangles
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Create a RenderMeshDescription using the convenience constructor
        // with named parameters.
        var desc = new RenderMeshDescription(
            shadowCastingMode: ShadowCastingMode.Off);

        // Create an array of mesh and material required for runtime rendering.
        var renderMeshArray = new RenderMeshArray(new [] { material }, new [] { mesh });
        
        // Call AddComponents to populate base entity with the components required
        // by Entities Graphics
        RenderMeshUtility.AddComponents(
            entity,
            state.EntityManager,
            desc,
            renderMeshArray,
            MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
        
        state.EntityManager.AddComponentData(entity, new LocalToWorld());
        state.EntityManager.AddComponentData(entity, new LocalTransform
        {
            Position = new float3(0, 0, 0),
            Rotation = quaternion.identity,
            Scale = 1
        });
        
        state.Enabled = false;
    }

    // OnUpdate and OnDestroy can be empty for this one-time setup system.
    public void OnUpdate(ref SystemState state) { }
    public void OnDestroy(ref SystemState state) { }
}