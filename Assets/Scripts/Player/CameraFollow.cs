using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Player
{
    public class CameraFollow : MonoBehaviour
    {
        public Entity entity;
        public EntityManager entityManager;
    
        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            query.TryGetSingletonEntity<PlayerTag>(out entity);
        }

        void Update()
        {
            transform.position = entityManager.GetComponentData<LocalTransform>(entity).Position;
            transform.rotation = entityManager.GetComponentData<LocalTransform>(entity).Rotation;
        }
    }
}
