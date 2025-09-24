using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Voxels
{
    public class TerrainDataBaker : MonoBehaviour
    {
        [SerializeField] NoiseSettingsSO terrainSettingsSO;
        [SerializeField] BlockNoiseSO blockNoiseSO;
        [SerializeField] TreeSettingsSO treeSettingsSO;
        [SerializeField] NoiseSettingsSO treePlacementSettingsSO;
        void Start()
        {
            NoiseSettings terrainNoise = new NoiseSettings
            {
                Frequency = terrainSettingsSO.frequency,
                Octaves = terrainSettingsSO.octaves,
                Seed = terrainSettingsSO.seed,
                Lacunarity = terrainSettingsSO.lacunarity,
                Persistence = terrainSettingsSO.persistence,
                Amplitude = terrainSettingsSO.amplitude
            };
            NoiseSettings treePlacementNoise = new NoiseSettings
            {
                Frequency = treePlacementSettingsSO.frequency,
                Octaves = treePlacementSettingsSO.octaves,
                Seed = treePlacementSettingsSO.seed,
                Lacunarity = treePlacementSettingsSO.lacunarity,
                Persistence = treePlacementSettingsSO.persistence,
                Amplitude = treePlacementSettingsSO.amplitude
            };
            
            TreeSettings treeSettings = new TreeSettings
            {
                LogBlockID = treeSettingsSO.LogBlock.BlockID,
                LeafBlockID = treeSettingsSO.LeafBlock.BlockID,
                SurfaceBlockID = treeSettingsSO.SurfaceBlock.BlockID,
                MinTrunkHeight = treeSettingsSO.MinTrunkHeight,
                MaxTrunkHeight = treeSettingsSO.MaxTrunkHeight,
                MaxLeafRadius = treeSettingsSO.MaxLeafRadius,
                SpawnRate = treeSettingsSO.SpawnRate
            };

            var orderedList = blockNoiseSO.BlockNoises.OrderByDescending(b => b.MinThreshold).ToArray();
            
            NativeArray<BlockNoise> blockNoises =
                new NativeArray<BlockNoise>(blockNoiseSO.BlockNoises.Count, Allocator.Persistent);

            for (int i = 0; i < blockNoiseSO.BlockNoises.Count; i++)
            {
                blockNoises[i] = new BlockNoise
                {
                    BlockID = orderedList[i].blockData.BlockID,
                    MinThreshold = orderedList[i].MinThreshold
                };
            }
            
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;
            
            var singletonEntity = entityManager.CreateEntity();
            entityManager.SetName(singletonEntity, "Terrain Generation Settings");

            entityManager.AddComponentData(singletonEntity, new TerrainGenerationData()
            {
                TerrainConfig = new TerrainConfig
                {
                    TerrainNoise = terrainNoise,
                    BlockNoises = blockNoises,
                    
                    TreeSettings = treeSettings,
                    TreePlacementNoise = treePlacementNoise
                }
            });
        }
    }
}
