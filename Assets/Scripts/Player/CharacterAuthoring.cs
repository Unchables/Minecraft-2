using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Player
{
    public class CharacterAuthoring : MonoBehaviour
    {
        public float walkSpeed = 25f;
        public float sprintSpeed = 40f;
        public float groundCheckDistance = 0.5f;
        public float groundCheckRadius = 0.5f;
        public float jumpForce = 20f;
        public float groundFriction = 5f;
        public float gravityForce = 5f;
        class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                var characterEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(characterEntity, new CharacterMoveStats
                {
                    WalkSpeed = authoring.walkSpeed,
                    SprintSpeed = authoring.sprintSpeed,
                    GroundCheckDistance = authoring.groundCheckDistance,
                    GroundCheckRadius = authoring.groundCheckRadius,
                    JumpForce = authoring.jumpForce,
                    GroundFriction = authoring.groundFriction,
                    GravityForce = authoring.gravityForce,
                });
                AddComponent<CharacterMoveInput>(characterEntity);
                AddComponent<DisableCharacterVelocity>(characterEntity);
            }
        }
    }

    public struct CharacterMoveStats : IComponentData
    {
        public float WalkSpeed;
        public float SprintSpeed;
        public float GroundCheckDistance;
        public float GroundCheckRadius;
        public float JumpForce;
        public float GroundFriction;
        public float GravityForce;
    }
    public struct DisableCharacterVelocity : IComponentData, IEnableableComponent
    {
    }
    public struct CharacterMoveInput : IComponentData
    {
        public float2 MoveValue;
        public bool IsJumping;
        public bool IsSprinting;
    }
}
