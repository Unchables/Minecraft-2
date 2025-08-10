using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(playerEntity);
                AddComponent(playerEntity, new LastPlayerChunkCoord {ChunkCoord = int.MinValue});
            }
        }
    }
    
    public struct PlayerTag : IComponentData { }

    public struct LastPlayerChunkCoord : IComponentData
    {
        public int3 ChunkCoord;
    }
}