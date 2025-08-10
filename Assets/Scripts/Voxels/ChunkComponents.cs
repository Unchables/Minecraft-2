using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Voxels
{
    public struct ChunkVoxels : IComponentData
    {
        public NativeArray<Voxel> Voxels;
    }
    public struct NeighbouringChunks : IComponentData
    {
        // Neighbor on the positive X axis
        public Entity Right;

        // Neighbor on the negative X axis
        public Entity Left;

        // Neighbor on the positive Y axis
        public Entity Top;

        // Neighbor on the negative Y axis
        public Entity Bottom;
    
        // Neighbor on the positive Z axis
        public Entity Front;

        // Neighbor on the negative Z axis
        public Entity Back;
    }
    public struct TerrainJobHandle : IComponentData
    {
        public JobHandle Value;
    }
    public struct MeshJobHandle : IComponentData
    {
        public JobHandle Value;
    }
    
    // Component to store the final mesh data.
    // This uses IComponentData because NativeLists are blittable.
    public struct ChunkMeshRenderData : IComponentData
    {
        public NativeList<float3> Vertices;
        public NativeList<int> Triangles;
    }
    
    public struct ChunkHasVoxelData : IComponentData, IEnableableComponent  { }
    public struct IsChunkTerrainGenerating : IComponentData, IEnableableComponent  { }
    public struct IsChunkMeshGenerating : IComponentData, IEnableableComponent  { }
    public struct ChunkHasMesh : IComponentData, IEnableableComponent  { }
}