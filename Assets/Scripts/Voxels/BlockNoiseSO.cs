using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Voxels
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "BlockNoise", menuName = "Voxel/Block Noise Settings")]
    public class BlockNoiseSO : ScriptableObject
    {
        public List<BlockNoiseObject> BlockNoises;
    }

    [System.Serializable]
    public struct BlockNoiseObject
    {
        public BlockTypeSO BlockTypeSO;
        [Range(0, 10)] public float MinThreshold;
    }
}