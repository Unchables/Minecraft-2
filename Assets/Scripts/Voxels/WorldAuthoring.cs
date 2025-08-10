using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels
{
    public class WorldAuthoring : MonoBehaviour
    {
        public int chunkPoolSize = 216;
        public int chunkSize = 32;
        public int chunkLoadRadius = 8;
        public int chunkUnloadRadius = 9;
        public int terrainHeight = 16;

        class Baker : Baker<WorldAuthoring>
        {
            public override void Bake(WorldAuthoring authoring)
            {
                var worldEntity = GetEntity(TransformUsageFlags.None);

                // Add the settings component to the main world entity
                AddComponent(worldEntity, new WorldSettings
                {
                    ChunkPoolSize = authoring.chunkPoolSize,
                    ChunkSize = authoring.chunkSize,
                    ChunkLoadRadius = authoring.chunkLoadRadius,
                    ChunkUnloadRadius = authoring.chunkUnloadRadius,
                    TerrainHeight = authoring.terrainHeight
                });
                
                AddComponent(worldEntity, new AllChunks
                {
                    Chunks = new NativeHashMap<int3, Entity>(1000, Allocator.Persistent)
                });
            }
        }
    }

}