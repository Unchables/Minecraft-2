using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Voxels
{
    // A struct to hold atlas coords for one face
    public struct BlockFaceTextures { public int TileX; public int TileY; }

    // A struct holding the data for all faces of one block type.
    // This is what the mesher job will read from.
    public struct BlockTextureData
    {
        public BlockFaceTextures Top;
        public BlockFaceTextures Side;
        public BlockFaceTextures Bottom;
    }

    // The singleton component that holds our Burst-compatible resources.
    public struct VoxelRenderResources : IComponentData
    {
        public NativeArray<BlockTextureData> BlockTypeData;
        public int AtlasSizeInTiles;
    }
    
    // This is a class, not a struct, and will be a "managed component".
    // It will hold our Material reference.
    public class VoxelRenderMaterial : IComponentData
    {
        public UnityEngine.Material ChunkMaterial;
    }
}