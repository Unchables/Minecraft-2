using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Voxels
{
    public class AtlasGenerator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BlockDatabaseSO blockDatabase;
        [SerializeField] private Material chunkMaterialTemplate; // A URP/Lit material to use as a base

        void Awake()
        {
            GenerateAtlasAndPrepareDOTSData();
        }

        private void GenerateAtlasAndPrepareDOTSData()
        {
            // === 1. Collect all unique textures ===
            var uniqueTextures = new List<Texture2D>();
            var textureToIndexMap = new Dictionary<Texture2D, int>();

            foreach (var blockSO in blockDatabase.BlockTypes)
            {
                // Add each unique texture to our list
                AddUniqueTexture(blockSO.TopFaceTexture, uniqueTextures, textureToIndexMap);
                AddUniqueTexture(blockSO.SideFaceTexture, uniqueTextures, textureToIndexMap);
                AddUniqueTexture(blockSO.BottomFaceTexture, uniqueTextures, textureToIndexMap);
            }

            // === 2. Calculate Atlas Dimensions ===
            // Assume all textures are the same size
            int textureSize = uniqueTextures.Count > 0 ? uniqueTextures[0].width : 16; 
        
            int atlasSizeInTiles = Mathf.CeilToInt(Mathf.Sqrt(uniqueTextures.Count));
            atlasSizeInTiles = Mathf.NextPowerOfTwo(atlasSizeInTiles);
            int atlasPixelSize = atlasSizeInTiles * textureSize;

            // === 3. Create the Atlas Texture ===
            var atlas = new Texture2D(atlasPixelSize, atlasPixelSize, TextureFormat.RGBA32, false)
            {
                name = "Voxel Texture Atlas",
                filterMode = FilterMode.Point, // Crucial for pixel-perfect textures
                wrapMode = TextureWrapMode.Clamp
            };

            for (int i = 0; i < uniqueTextures.Count; i++)
            {
                int x = i % atlasSizeInTiles;
                int y = i / atlasSizeInTiles;
                Graphics.CopyTexture(
                    uniqueTextures[i], 0, 0, 0, 0, textureSize, textureSize,
                    atlas, 0, 0, x * textureSize, y * textureSize);
            }
            atlas.Apply();

            // === 4. Create the final Material ===
            var finalChunkMaterial = new Material(chunkMaterialTemplate);
            finalChunkMaterial.mainTexture = atlas;

            // === 5. Create BlockTextureData for DOTS ===
            // +1 to account for Air at ID 0
            int blockTypeCount = blockDatabase.BlockTypes.Count + 1; 
            var blockTextureDataArray = new NativeArray<BlockTextureData>(blockTypeCount, Allocator.Persistent);
        
            for (int i = 0; i < blockDatabase.BlockTypes.Count; i++)
            {
                var blockSO = blockDatabase.BlockTypes[i];
                ushort blockID = (ushort)(i + 1); // ID 0 is Air

                blockTextureDataArray[blockID] = new BlockTextureData
                {
                    Top = GetFaceTexture(blockSO.TopFaceTexture, textureToIndexMap, atlasSizeInTiles),
                    Side = GetFaceTexture(blockSO.SideFaceTexture, textureToIndexMap, atlasSizeInTiles),
                    Bottom = GetFaceTexture(blockSO.BottomFaceTexture, textureToIndexMap, atlasSizeInTiles)
                };
            }
        
            // === 6. Create Singleton Entity with a VoxelRenderResources component ===
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            var singletonEntity = entityManager.CreateEntity();
            entityManager.SetName(singletonEntity, "Voxel Render Resources");

            entityManager.AddComponentData(singletonEntity, new VoxelRenderResources
            {
                BlockTypeData = blockTextureDataArray,
                AtlasSizeInTiles = atlasSizeInTiles
            });

            MaterialHolder.ChunkMaterial = finalChunkMaterial;
            /*entityManager.AddComponentObject(singletonEntity, new VoxelRenderMaterial
            {
                ChunkMaterial = finalChunkMaterial
            });*/
            
            Debug.Log($"Generated {atlasPixelSize}x{atlasPixelSize} Texture Atlas with {uniqueTextures.Count} unique textures.");
        }
    
        private void AddUniqueTexture(Texture2D tex, List<Texture2D> uniqueTextures, Dictionary<Texture2D, int> map)
        {
            if (tex != null && !map.ContainsKey(tex))
            {
                map.Add(tex, uniqueTextures.Count);
                uniqueTextures.Add(tex);
            }
        }

        private BlockFaceTextures GetFaceTexture(Texture2D tex, Dictionary<Texture2D, int> map, int atlasSizeInTiles)
        {
            if (tex == null || !map.TryGetValue(tex, out int index))
            {
                return new BlockFaceTextures { TileX = 0, TileY = 0 }; // Default to a known texture (e.g., purple error)
            }

            return new BlockFaceTextures
            {
                TileX = index % atlasSizeInTiles,
                TileY = index / atlasSizeInTiles
            };
        }
    
        // Make sure to clean up the persistent NativeArray when the game closes
        private void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && world.IsCreated)
            {
                var query = world.EntityManager.CreateEntityQuery(typeof(VoxelRenderResources));
                if (query.TryGetSingleton<VoxelRenderResources>(out var resources))
                {
                    if (resources.BlockTypeData.IsCreated)
                    {
                        resources.BlockTypeData.Dispose();
                    }
                }
            }
        }
    }
}