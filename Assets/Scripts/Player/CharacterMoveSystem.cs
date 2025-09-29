using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Voxels;

namespace Player
{
    partial struct CharacterMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<WorldSettings>();

            foreach (var (physicsMass, characterMoveInput)
                     in SystemAPI.Query<RefRW<PhysicsMass>, RefRO<CharacterMoveInput>>())
            {
                physicsMass.ValueRW.InverseInertia = float3.zero;
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // this only works with 1 player
            foreach (var (disableVelocity, physicsVelocity) in SystemAPI.Query<EnabledRefRO<DisableCharacterVelocity>, RefRW<PhysicsVelocity>>())
            {
                if (disableVelocity.ValueRO)
                {
                    physicsVelocity.ValueRW.Linear = float3.zero;
                    return;
                }
            }
            
            foreach (var (velocity, moveInput, moveStats,
                         localTransform) 
                     in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<CharacterMoveInput>, RefRO<CharacterMoveStats>, 
                         RefRO<LocalTransform>>())
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
                float3 world = float3.zero;
                
                
                var collisionWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld.CollisionWorld;

                var filter = new CollisionFilter
                {
                    BelongsTo = (uint)CollisionLayers.Player, // The cast "belongs" to the player layer
                    CollidesWith = (uint)CollisionLayers.Terrain, // It should only hit terrain
                    GroupIndex = 0
                };
                
                bool isGrounded = collisionWorld.SphereCast(localTransform.ValueRO.Position, moveStats.ValueRO.GroundCheckRadius, -localTransform.ValueRO.Up(), moveStats.ValueRO.GroundCheckDistance, filter);

                
                float moveSpeed = moveInput.ValueRO.IsSprinting ? moveStats.ValueRO.SprintSpeed : moveStats.ValueRO.WalkSpeed;
                world += desiredDirection * moveSpeed * (isGrounded ? 1 : 0.3f);
                
                if (isGrounded && moveInput.ValueRO.IsJumping)
                {
                    // ...apply an immediate upward force.
                    // We set the Y velocity directly to ensure a consistent jump height.
                    velocity.ValueRW.Linear.y = moveStats.ValueRO.JumpForce;
                }
                
                // Apply gravity
                world.y -= moveStats.ValueRO.GravityForce;
                
                // ---  Update the Physics Velocity ---
                // Add the calculated forces to the linear velocity.
                velocity.ValueRW.Linear += world * SystemAPI.Time.DeltaTime;

                float friction =
                    isGrounded ? moveStats.ValueRO.GroundFriction : moveStats.ValueRO.GroundFriction * 0.2f;

                float3 targetVelocity = new float3(0, velocity.ValueRO.Linear.y, 0);
                velocity.ValueRW.Linear = math.lerp(velocity.ValueRO.Linear, targetVelocity, friction * SystemAPI.Time.DeltaTime);
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

    [System.Flags]
    public enum CollisionLayers : uint
    {
        Nothing = 0,
        Terrain = 1 << 3,
        Player = 1 << 6,
        All = ~0u
    }
}
