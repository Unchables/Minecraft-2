using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace Voxels
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "TreeSettings", menuName = "Voxel/Tree Settings")]
    public class TreeSettingsSO : ScriptableObject
    {
        public BlockData LogBlock;
        [FormerlySerializedAs("leafBlock")] public BlockData LeafBlock;
        public BlockData SurfaceBlock; // The block trees can grow on (e.g., Grass)

        public int MinTrunkHeight;
        public int MaxTrunkHeight;
        
        public int MaxLeafRadius = 5;

        // A value from 0 to 1. 0.01 = 1% chance a valid spot will grow a tree.
        [Range(0, 0.1f)] public float SpawnRate;
    }
}