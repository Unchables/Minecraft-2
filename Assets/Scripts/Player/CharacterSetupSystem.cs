using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Player
{
    partial struct CharacterSetupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {

            foreach (var (physicsMass, characterMoveInput)
                     in SystemAPI.Query<RefRW<PhysicsMass>, RefRO<CharacterMoveInput>>())
            {
                physicsMass.ValueRW.InverseInertia = float3.zero;
            }

            state.Enabled = false;
        }
    }
}