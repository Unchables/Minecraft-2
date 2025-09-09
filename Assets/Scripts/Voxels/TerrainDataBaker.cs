using System.Linq;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Voxels
{
    public class TerrainDataBaker : MonoBehaviour
    {
        [SerializeField] NoiseSettingsSO noiseSettingsSO;
        [SerializeField] BlockNoiseSO blockNoiseSO;
        void Start()
        {
            NoiseSettings noiseSettings = new NoiseSettings
            {
                Frequency = noiseSettingsSO.frequency,
                Octaves = noiseSettingsSO.octaves,
                Seed = noiseSettingsSO.seed,
                Lacunarity = noiseSettingsSO.lacunarity,
                Persistence = noiseSettingsSO.persistence,
                Amplitude = noiseSettingsSO.amplitude
            };

            var orderedList = blockNoiseSO.BlockNoises.OrderBy(b => b.MinThreshold).ToArray();
            
            NativeArray<BlockNoise> blockNoises =
                new NativeArray<BlockNoise>(blockNoiseSO.BlockNoises.Count, Allocator.Persistent);

            for (int i = 0; i < blockNoiseSO.BlockNoises.Count; i++)
            {
                blockNoises[i] = new BlockNoise
                {
                    BlockID = orderedList[i].BlockTypeSO.BlockID,
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
                    NoiseSettings = noiseSettings,
                    BlockNoises = blockNoises
                }
            });
        }
    }
}
