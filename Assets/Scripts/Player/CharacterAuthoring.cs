using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    public class CharacterAuthoring : MonoBehaviour
    {
        public float moveSpeed = 15f;
        class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                var characterEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(characterEntity, new CharacterMoveSpeed
                {
                    Value = authoring.moveSpeed
                });
                AddComponent<CharacterMoveInput>(characterEntity);
            }
        }
    }

    public struct CharacterMoveSpeed : IComponentData
    {
        public float Value;
    }
    public struct CharacterMoveInput : IComponentData
    {
        public float3 Value;
    }
}