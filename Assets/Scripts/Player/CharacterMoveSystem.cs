using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

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
            foreach (var (velocity, direction, speed) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<CharacterMoveInput>, RefRO<CharacterMoveSpeed>>())
            {
                float3 moveDirection = new float3(direction.ValueRO.Value.x, 0, direction.ValueRO.Value.y);
                velocity.ValueRW.Linear = moveDirection * speed.ValueRO.Value;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        
        }
    }
}
