using System;
using Roguelike.Combat;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>验证 Game 视野快照会同步影响 ECS 敌人显隐，以及 E 飞行视野的加入和移除。</summary>
    public static class CombatFogValidation
    {
        [MenuItem("Tools/Roguelike/Validate Game Fog Integration")]
        public static void Validate()
        {
            const string materialPath = "Assets/Game/FogOfWar/Materials/Fog Smoothing.mat";
            Material smoothing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Check(smoothing != null, "Fog smoothing material exists");

            using var world = new World("Game fog integration validation");
            EntityManager manager = world.EntityManager;
            Entity near = CreateEnemy(manager, new Vector3(3f, 0.55f, 0f));
            Entity far = CreateEnemy(manager, new Vector3(40f, 0.55f, 0f));
            Entity flareOwner = manager.CreateEntity();
            manager.AddBuffer<CombatVisionFlare>(flareOwner);
            using var enemies = manager.CreateEntityQuery(
                ComponentType.ReadWrite<CombatEnemy>(), ComponentType.ReadOnly<LocalTransform>());
            using var fog = new CombatFogSession(manager, enemies, 125f, smoothing, null, 64);

            fog.Reset(Vector3.zero, false, false);
            CheckVisible(manager, near, true, "player's initial vision reveals the near enemy");
            CheckVisible(manager, far, false, "distant enemy is hidden before E");

            // Reset can change entity archetypes. Fetch the buffer only after its structural updates.
            manager.GetBuffer<CombatVisionFlare>(flareOwner).Add(new CombatVisionFlare
            {
                Position = new Unity.Mathematics.float3(40f, 0.55f, 0f),
                Destination = new Unity.Mathematics.float3(40f, 0.55f, 0f),
                Radius = 8f,
                RevealRemaining = 5f
            });
            fog.Tick(Vector3.zero, false, false, manager.GetBuffer<CombatVisionFlare>(flareOwner));
            CheckVisible(manager, far, true, "E vision reveals the distant enemy");

            // Reacquire after Tick because rendering visibility adds or removes ECS components.
            manager.GetBuffer<CombatVisionFlare>(flareOwner).Clear();
            fog.Tick(Vector3.zero, false, false, manager.GetBuffer<CombatVisionFlare>(flareOwner));
            CheckVisible(manager, far, false, "distant enemy hides when E vision expires");

            Debug.Log("GAME_FOG_INTEGRATION_VALIDATED: player vision, E reveal, E expiry, ECS visibility and rendering.");
        }

        private static Entity CreateEnemy(EntityManager manager, Vector3 position)
        {
            Entity enemy = manager.CreateEntity(typeof(CombatEnemy), typeof(LocalTransform));
            manager.SetComponentData(enemy, LocalTransform.FromPosition(position));
            return enemy;
        }

        private static void CheckVisible(EntityManager manager, Entity enemy, bool expected, string description)
        {
            bool targetable = manager.GetComponentData<CombatEnemy>(enemy).VisibleToPlayer != 0;
            bool rendered = !manager.HasComponent<DisableRendering>(enemy);
            Check(targetable == expected && rendered == expected, description);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Game fog validation: " + description);
        }
    }
}
