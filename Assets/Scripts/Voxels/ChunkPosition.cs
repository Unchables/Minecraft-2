using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Voxels
{
    /// <summary>
    /// An IComponentData that stores the world-space coordinate of this chunk.
    /// The coordinate is based on chunk dimensions (e.g., (0,0,0), (1,0,0), etc.).
    /// </summary>
    public struct ChunkPosition : IComponentData
    {
        public int3 Value;
    }

    public struct GenerateChunkMesh : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// An IComponentData that holds the lookup map for voxels with extra state.
    /// This map links a voxel's local 1D index to the separate Entity that holds its state data
    /// (e.g., a chest's inventory). This component is only present on chunks that contain such voxels.
    /// </summary>
    public struct VoxelStateMap : IComponentData, IDisposable
    {
        // A hash map where:
        // Key: The 1D index of the voxel within the chunk (0 to 32*32*32 - 1).
        // Value: The Entity that holds the components for this voxel's state.
        public NativeHashMap<int, Entity> Map;

        /// <summary>
        /// The map uses unmanaged memory and must be disposed of when the component is destroyed.
        /// </summary>
        public void Dispose()
        {
            if (Map.IsCreated)
            {
                Map.Dispose();
            }
        }
    }
}