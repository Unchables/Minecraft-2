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
        public int TerrainHeight;
        public Entity ChunkPrefab; // A template entity for creating new chunks
    }

    [ChunkSerializable]
    public struct AllChunks : IComponentData
    {
        public NativeHashMap<int3, ChunkVoxels> Chunks;
        public NativeHashMap<int3, Entity> Entites;
    }
    
    public struct TerrainGenerationData : IComponentData
    {
        public TerrainConfig TerrainConfig;
    }
    public struct WaterSimulationTimer : IComponentData
    {
        // The time the last simulation tick occurred.
        public double LastTickTime;
    }
}