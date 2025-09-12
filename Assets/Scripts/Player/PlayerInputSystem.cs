using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    public partial class PlayerInputSystem : SystemBase
    {
        private InputSystem _input;
        protected override void OnCreate()
        {
            _input = new InputSystem();
            _input.Enable();
        }

        protected override void OnUpdate()
        {
            var moveInput = (float2)_input.Player.Move.ReadValue<Vector2>();
            var lookInput = (float2)_input.Player.Look.ReadValue<Vector2>();
            
            float yValue = 0;
            yValue += _input.Player.Jump.ReadValue<float>();
            yValue -= _input.Player.Crouch.ReadValue<float>();
            
            float3 inputDirection = new float3(moveInput.x, yValue, moveInput.y);
            
            foreach (var (characterMoveInput, playerLookInput) in SystemAPI.Query<RefRW<CharacterMoveInput>, RefRW<PlayerLookInput>>().WithAll<PlayerTag>())
            {
                characterMoveInput.ValueRW.Value = inputDirection;
                playerLookInput.ValueRW.Value = lookInput;
            }
        }
    }
}