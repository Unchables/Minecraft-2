using Sirenix.OdinInspector;
using UnityEngine;

namespace Voxels
{
    [CreateAssetMenu(fileName = "NewBlockType", menuName = "Voxel/Block Type")]
    public class BlockTypeSO : ScriptableObject
    {
        [Header("Block Info")]
        public string BlockName;
        public bool IsSolid = true;
    
        [Header("Textures")]
        // Texture for the top face (+Y)
        public Texture2D TopFaceTexture;
    
        // Texture for the side faces (+X, -X, +Z, -Z)
        public Texture2D SideFaceTexture;
    
        // Texture for the bottom face (-Y)
        public Texture2D BottomFaceTexture;

        [ReadOnly] public ushort BlockID;
    }

    public class AirID
    {
        public const ushort Value = 0;
    }
}