using Roguelike.Combat;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>选中 Demo 时显示范围与少量状态；不枚举敌人，不参与 Player 构建。</summary>
    internal static class CombatSceneVisualization
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawSelected(CombatDemoController demo, GizmoType gizmoType)
        {
            // Active 也可能是未选中的 Inspector 目标，因此显式限制选中对象。
            if (Selection.activeGameObject != demo.gameObject) return;
            var previousColor = Handles.color;
            try
            {
                var center = Vector3.zero;
                var status = "预览配置（战斗参数重开后生效）";
                if (Application.isPlaying && demo.IsReady)
                {
                    var state = demo.State;
                    center = new Vector3(state.Position.x, 0, state.Position.z);
                    status = $"生命 {state.Health:0}/{state.MaximumHealth:0}  敌人 {state.AliveEnemies}  击杀 {state.Kills}";
                }
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(center, Vector3.up, demo.attackRange);
                Handles.Label(center + Vector3.right * demo.attackRange, $"攻击配置半径 {demo.attackRange:0.0}");
                Handles.color = Color.green;
                Handles.DrawWireDisc(Vector3.zero, Vector3.up, demo.safeSpawnRadius);
                Handles.Label(Vector3.forward * demo.safeSpawnRadius, "初始安全区");
                var size = demo.arenaHalfSize;
                Handles.color = Color.cyan;
                Handles.DrawPolyLine(new Vector3(-size, 0, -size), new Vector3(size, 0, -size),
                    new Vector3(size, 0, size), new Vector3(-size, 0, size), new Vector3(-size, 0, -size));
                Handles.Label(center + Vector3.up * 3, status);
            }
            finally { Handles.color = previousColor; }
        }
    }
}
