using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Player
{
    partial struct CharacterMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (velocity, direction, speed, localTransform) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<CharacterMoveInput>, RefRO<CharacterMoveSpeed>, RefRO<LocalTransform>>())
            {
                float3 localMoveDirection = direction.ValueRO.Value;
                float3 worldMoveDirection = math.mul(localTransform.ValueRO.Rotation, localMoveDirection);
                velocity.ValueRW.Linear = worldMoveDirection * speed.ValueRO.Value;
            }
            
            foreach (var (playerLookInput, playerLookSensitivity, localTransform) in SystemAPI.Query<RefRO<PlayerLookInput>, RefRO<PlayerLookSensitivity>, RefRW<LocalTransform>>())
            {
                // Calculate the rotation angles from input and sensitivity.
                float yawAngle = playerLookInput.ValueRO.Value.x * playerLookSensitivity.ValueRO.Value;
                
                // Invert the pitch angle by negating the Y input.
                // This is standard because a positive mouse Y (moving mouse up) should result in pitching the camera upwards (negative rotation around the X-axis).
                float pitchAngle = -playerLookInput.ValueRO.Value.y * playerLookSensitivity.ValueRO.Value;

                // Create a quaternion for the horizontal rotation (Yaw).
                // This rotates around the global Y-axis.
                var yawRotation = quaternion.RotateY(yawAngle);

                // Create a quaternion for the vertical rotation (Pitch).
                // This rotates around the local X-axis.
                var pitchRotation = quaternion.RotateX(pitchAngle);

                // To prevent Z-axis roll (unwanted tilting), we apply the rotations in a specific order.
                // 1. First, apply the horizontal (yaw) rotation in world space. This turns the camera left/right.
                localTransform.ValueRW.Rotation = math.mul(yawRotation, localTransform.ValueRW.Rotation);

                // 2. Then, apply the vertical (pitch) rotation in the new local space. This tilts the camera up/down.
                localTransform.ValueRW.Rotation = math.mul(localTransform.ValueRW.Rotation, pitchRotation);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
        }
    }
}
