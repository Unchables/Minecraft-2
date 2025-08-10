using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels
{
    public struct WorldSettings : IComponentData
    {
        public int ChunkSize;
        public int ChunkPoolSize;
        public int ChunkLoadRadius;
        public int ChunkUnloadRadius; // Slightly larger to prevent rapid load/unload
        public int TerrainHeight;
        public Entity ChunkPrefab; // A template entity for creating new chunks
    }

    public struct AllChunks : IComponentData
    {
        public NativeHashMap<int3, Entity> Chunks;
    }
}