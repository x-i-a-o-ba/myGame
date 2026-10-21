using Unity.Entities;

namespace Roguelike.Simulation
{
    public struct EnemyTag : IComponentData { }

    public struct Health : IComponentData
    {
        public float Current;
        public float Maximum;
    }

    public struct MoveSpeed : IComponentData
    {
        public float Value;
    }
}
