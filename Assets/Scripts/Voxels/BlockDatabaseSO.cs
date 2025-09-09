using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Voxels
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "BlockDatabase", menuName = "Voxel/Block Database")]
    public class BlockDatabaseSO : ScriptableObject
    {
        [Button]
        public void SetBlockIDs()
        {
            for (var i = 0; i < blockTypes.Count; i++)
            {
                if(i > 4096) Debug.LogError("ushorts cannot support that number of Block IDs\nBlockID:" + i + " is out of range.");
                blockTypes[i].BlockID = (ushort)i;
            }
        }
        
        // Drag all of your BlockTypeSO assets into this list in the Inspector.
        // The order matters! Index 0 will become Block ID 1, Index 1 becomes Block ID 2, etc.
        public List<BlockTypeSO> blockTypes;
    }
}