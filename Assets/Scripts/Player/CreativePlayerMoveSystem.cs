using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Voxels;

namespace Player
{
    partial struct CreativePlayerMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (moveInput, moveStats,
                         localTransform) 
                     in SystemAPI.Query<RefRO<CreativePlayerMoveInput>, RefRO<CreativePlayerMoveStats>, 
                         RefRW<LocalTransform>>())
            {
                float2 moveInputValue = moveInput.ValueRO.MoveValue;

                float3 forward = localTransform.ValueRO.Forward();
                float3 right = localTransform.ValueRO.Right();
                forward.y = 0;
                right.y = 0;
                forward = math.normalize(forward);
                right = math.normalize(right);

                float3 desiredDirection = forward * moveInputValue.y + right * moveInputValue.x;

                if (math.lengthsq(desiredDirection) > 0)
                    desiredDirection = math.normalize(desiredDirection);

                // --- Apply Forces ---
                
                float3 moveDirection = float3.zero;

                float moveSpeed = moveStats.ValueRO.MoveSpeed;
                if(moveInput.ValueRO.IsSprinting)
                    moveSpeed *= moveStats.ValueRO.SprintMultiplier;
                
                moveDirection = desiredDirection * moveSpeed;
                
                if (moveInput.ValueRO.IsRaising)
                {
                    moveDirection.y = moveStats.ValueRO.YFlySpeed;
                }
                if (moveInput.ValueRO.IsLowering)
                {
                    moveDirection.y = -moveStats.ValueRO.YFlySpeed;
                }

                localTransform.ValueRW.Position += moveDirection * SystemAPI.Time.DeltaTime;
            }
            
            foreach (var (playerLookInput, playerLookSensitivity, localTransform, playerLookState, playerLookAngles) in 
                     SystemAPI.Query<RefRO<PlayerLookInput>, RefRO<PlayerLookSensitivity>, RefRW<LocalTransform>, RefRW<PlayerLookRotation>, RefRW<PlayerLookAngles>>())
            {
                // --- 1. Get the Input DELTA for this frame ---
                // This is the CHANGE in angle, not the final angle.
                float yawDelta = playerLookInput.ValueRO.Value.x * playerLookSensitivity.ValueRO.Value * 0.001f;
                float pitchDelta = -playerLookInput.ValueRO.Value.y * playerLookSensitivity.ValueRO.Value * 0.001f; // Inverted

                // --- 2. Accumulate the Total Rotation ---
                // Add this frame's change to our persistent angle values.
                playerLookAngles.ValueRW.Yaw += yawDelta;
                playerLookAngles.ValueRW.Pitch += pitchDelta;

                // --- 3. CLAMP THE PITCH ANGLE ---
                // This is the simple, robust way to clamp. We clamp the raw float value.
                // 90 degrees up and down, converted to radians.
                const float pitchLimit = (math.PI / 2.0f) - 0.01f; // Subtract a tiny epsilon to prevent gimbal lock issues
                playerLookAngles.ValueRW.Pitch = math.clamp(playerLookAngles.ValueRW.Pitch, -pitchLimit, pitchLimit);

                // --- 4. Reconstruct the Quaternions from the Final Angles ---
                // Create the full yaw rotation for the body.
                var yawRotation = quaternion.RotateY(playerLookAngles.ValueRW.Yaw);

                // Create the clamped pitch rotation for the camera.
                var pitchRotation = quaternion.RotateX(playerLookAngles.ValueRW.Pitch);

                // --- 5. Apply the Rotations ---
                // The player's main body only gets the horizontal (yaw) rotation.
                // This affects the movement direction.
                localTransform.ValueRW.Rotation = yawRotation;
                
                // The camera's final orientation is the combination of the body's yaw and its own local pitch.
                // We store this in PlayerLookState for the separate camera system to use.
                playerLookState.ValueRW.Value = math.mul(yawRotation, pitchRotation);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
        }
    }
}
