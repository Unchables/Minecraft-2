using Unity.Collections;
using Unity.Mathematics;
namespace Voxels
{
// Simple container for mesh lists — stored in chunk component
    public struct MeshData
    {
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
        public NativeList<float2> UVs0;
        public NativeList<float2> UVs1;
        public bool IsCreated => Vertices.IsCreated || Triangles.IsCreated
                                                    || UVs0.IsCreated || UVs1.IsCreated;
        public void CreateIfNeeded(int vertexCapacity, int triangleCapacity,
            Allocator allocator)
        {
            if (!Vertices.IsCreated) Vertices = new
                NativeList<float3>(vertexCapacity, allocator);
            if (!Triangles.IsCreated) Triangles = new
                NativeList<int>(triangleCapacity, allocator);
            if (!UVs0.IsCreated) UVs0 = new
                NativeList<float2>(vertexCapacity, allocator);
            if (!UVs1.IsCreated) UVs1 = new
                NativeList<float2>(vertexCapacity, allocator);
        }
        public void Dispose()
        {
            if (Vertices.IsCreated) Vertices.Dispose();
            if (Triangles.IsCreated) Triangles.Dispose();
            if (UVs0.IsCreated) UVs0.Dispose();
            if (UVs1.IsCreated) UVs1.Dispose();
        }
    }