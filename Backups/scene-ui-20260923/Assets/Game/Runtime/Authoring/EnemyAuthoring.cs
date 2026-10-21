using Roguelike.Simulation;
using Unity.Entities;
using UnityEngine;

namespace Roguelike.Authoring
{
    // Add to a mesh prefab and reference it from a SubScene authoring workflow.
    public sealed class EnemyAuthoring : MonoBehaviour
    {
        [Min(1)] public float maximumHealth = 100;
        [Min(0)] public float moveSpeed = 2;

        private sealed class EnemyBaker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                float health = Mathf.Max(1, authoring.maximumHealth);
                AddComponent<EnemyTag>(entity);
                AddComponent(entity, new Health { Current = health, Maximum = health });
                AddComponent(entity, new MoveSpeed { Value = Mathf.Max(0, authoring.moveSpeed) });
            }
        }
    }
}
