using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Player
{
    public class CameraAuthoring : MonoBehaviour
    {
        public float moveSpeed;
        class Baker : Baker<CameraAuthoring>
        {
            public override void Bake(CameraAuthoring authoring)
            {
                var playerEntity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<CameraTag>(playerEntity);
                AddComponent<CharacterMoveInput>(playerEntity);
                AddComponent(playerEntity, new CharacterMoveStats() { WalkSpeed = authoring.moveSpeed });
            }
        }
    }
    
    public struct CameraTag : IComponentData { }
}