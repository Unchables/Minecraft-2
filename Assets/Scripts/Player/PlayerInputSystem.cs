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
            var currentInput = (float2)_input.Player.Move.ReadValue<Vector2>();
            
            float yValue = 0;
            yValue += _input.Player.Jump.ReadValue<float>();
            yValue -= _input.Player.Crouch.ReadValue<float>();
            
            float3 inputDirection = new float3(currentInput.x, yValue, currentInput.y);
            
            foreach (var characterMoveInput in SystemAPI.Query<RefRW<CharacterMoveInput>>().WithAll<CameraTag>())
            {
                characterMoveInput.ValueRW.Value = inputDirection;
            }
        }
    }
}