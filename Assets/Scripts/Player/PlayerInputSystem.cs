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
            
            bool isJumping = _input.Player.Jump.IsPressed();
            bool isSprinting = _input.Player.Sprint.IsPressed();
            
            float2 inputDirection = new float2(moveInput.x, moveInput.y);
            
            foreach (var (characterMoveInput, playerLookInput) in SystemAPI.Query<RefRW<CharacterMoveInput>, RefRW<PlayerLookInput>>().WithAll<PlayerTag>())
            {
                characterMoveInput.ValueRW.MoveValue = inputDirection;
                characterMoveInput.ValueRW.IsJumping = isJumping;
                characterMoveInput.ValueRW.IsSprinting = isSprinting;
                playerLookInput.ValueRW.Value = lookInput;
            }
        }
    }
}