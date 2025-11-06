using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    // Holds the reference to the child entity that has the water mesh.
    public struct ChunkWaterMesh : IComponentData
    {
        public Entity Value;
    }

    // A buffer to hold the mesh data between the job and the finalizer.
    public struct ChunkWaterMeshRenderData : IComponentData
    {
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        public NativeList<float2> UVs;
    }
    
    // Tag to indicate the water mesh is currently generating.
    public struct IsChunkWaterMeshGenerating : IComponentData, IEnableableComponent {}
    
    // Tag to indicate the chunk has a water mesh.
    public struct ChunkHasWaterMesh : IComponentData, IEnableableComponent {}
}