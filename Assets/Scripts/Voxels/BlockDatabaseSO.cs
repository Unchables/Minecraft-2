namespace Voxels
{
    using System.Collections.Generic;
    using UnityEngine;

    [CreateAssetMenu(fileName = "BlockDatabase", menuName = "Voxel/Block Database")]
    public class BlockDatabaseSO : ScriptableObject
    {
        // Drag all of your BlockTypeSO assets into this list in the Inspector.
        // The order matters! Index 0 will become Block ID 1, Index 1 becomes Block ID 2, etc.
        public List<BlockTypeSO> BlockTypes;
    }
}