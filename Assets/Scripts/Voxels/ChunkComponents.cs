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
    public struct NeighbouringChunks : IBufferElementData
    {
        public Entity Neighbour;
        public int3 Position;
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