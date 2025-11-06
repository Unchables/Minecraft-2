using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    public class CreativePlayerAuthoring : MonoBehaviour
    {
        public float moveSpeed = 25f;
        public float sprintMultiplier = 2f;
        public float yFlySpeed = 20f;
        
        class Baker : Baker<CreativePlayerAuthoring>
        {
            public override void Bake(CreativePlayerAuthoring authoring)
            {
                var characterEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(characterEntity, new CreativePlayerMoveStats
                {
                    MoveSpeed = authoring.moveSpeed,
                    SprintMultiplier = authoring.sprintMultiplier,
                    YFlySpeed = authoring.yFlySpeed,
                });
                AddComponent<CreativePlayerMoveInput>(characterEntity);
            }
        }
    }

    public struct CreativePlayerMoveStats : IComponentData
    {
        public float MoveSpeed;
        public float SprintMultiplier;
        public float YFlySpeed;
    }
    public struct CreativePlayerMoveInput : IComponentData
    {
        public float2 MoveValue;
        public bool IsSprinting;
        public bool IsRaising;
        public bool IsLowering;
    }
}
