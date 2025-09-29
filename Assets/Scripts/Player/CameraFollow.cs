using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Player
{
    public class CameraFollow : MonoBehaviour
    {
        public Entity entity;
        public EntityManager entityManager;
        public Vector3 offset;
    
        void Start()
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        }

        void Update()
        {
            if (entity.Index == 0)
            {
                var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
                query.TryGetSingletonEntity<PlayerTag>(out entity);
                if (entity.Index == 0) return;
            }
            
            transform.position = (Vector3)entityManager.GetComponentData<LocalTransform>(entity).Position + offset;
            transform.rotation = entityManager.GetComponentData<PlayerLookRotation>(entity).Value;
        }
    }
}
