using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Voxels
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "NoiseSettings", menuName = "Voxel/Noise Settings")]
    public class NoiseSettingsSO : ScriptableObject
    {
        public int seed;
        public float frequency = 0.01f;
        public float amplitude = 1f;
        [Tooltip("Controls frequency increase per octave")] public float lacunarity = 2.0f;
        [Tooltip("Controls amplitude decrease per octave")] public float persistence = 0.5f;
        public int octaves = 4;
    }
}