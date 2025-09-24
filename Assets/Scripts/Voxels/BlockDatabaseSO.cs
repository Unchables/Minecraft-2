using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Voxels
{
    [InlineEditor]
    [CreateAssetMenu(fileName = "BlockDatabase", menuName = "Voxel/Block Database")]
    public class BlockDatabaseSO : ScriptableObject
    {
        public void SetBlockIDs()
        {
            blockTypes = Resources.LoadAll<BlockData>("Blocks").ToList();
            var air = blockTypes.FirstOrDefault(b => b.BlockName == "Air");
            blockTypes.Remove(air);
            blockTypes.Insert(0, air);
            
            for (var i = 0; i < blockTypes.Count; i++)
            {
                if(i > 4096) Debug.LogError("ushorts cannot support that number of Block IDs\nBlockID:" + i + " is out of range.");
                blockTypes[i].BlockID = (ushort)i;
            }
        }
        
        public List<BlockData> blockTypes { get; private set; }
    }
}