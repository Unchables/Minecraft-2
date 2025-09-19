using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float sensitivity = 10;
        class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(playerEntity);
                AddComponent<PlayerLookInput>(playerEntity);
                AddComponent(playerEntity, new PlayerLookSensitivity
                {
                    Value = authoring.sensitivity
                });
            }
        }
    }
    
    public struct PlayerTag : IComponentData { }

    public struct PlayerLookInput : IComponentData
    {
        public float2 Value;
    }
    public struct PlayerLookSensitivity : IComponentData
    {
        public float Value;
    }
}