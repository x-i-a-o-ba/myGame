using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 左键点击敌人查看属性（Dota 2 式单位面板）。选择只影响展示与选中圈，不影响自动攻击目标或任何结算。
    /// 技能瞄准中左键用于确认施法，不会切换选中目标。
    /// </summary>
    public sealed partial class GameCombatController
    {
        /// <summary>底部 HUD 显示的遗物格数，超出部分以“+N”显示。</summary>
        public const int RelicSlots=4;
        private Entity selectedEnemy=Entity.Null;
        private readonly TargetInfoView targetView=new TargetInfoView();
        public Entity SelectedEnemy=>selectedEnemy;
        private static readonly Color NormalSelection=new Color(1f,.85f,.35f,1),EliteSelection=new Color(1f,.6f,.2f,1),BossSelection=new Color(1f,.3f,.25f,1);

        /// <summary>战斗中左键：点中敌人则选中，点空地则取消选择。</summary>
        private void UpdateSelectionInput()
        {
            if(IsAimingSkill || !Input.GetMouseButtonDown(0) || hud.BlocksPointer(Input.mousePosition)) return;
            var enemy=PickEnemy(Input.mousePosition);
            if(enemy!=Entity.Null) {selectedEnemy=enemy;nextHudUpdate=0;}
            else ClearSelection();
        }
        /// <summary>选中指定敌人（探针或界面调用）；目标无效时返回 false。</summary>
        public bool SelectEnemy(Entity enemy)
        {
            if(!Alive(enemy) || !session.World.EntityManager.HasComponent<CombatEnemy>(enemy)) return false;
            selectedEnemy=enemy;nextHudUpdate=0;return true;
        }
        public void ClearSelection()
        {
            selectedEnemy=Entity.Null;
            if(abilityIndicator) abilityIndicator.HideSelection();
            ExpeditionUI?.Visible("target.visible",false);
        }
        /// <summary>按屏幕距离选取鼠标附近的敌人：比地面射线更贴合俯视角下有高度的模型。</summary>
        private Entity PickEnemy(Vector2 pointer)
        {
            if(!hasEnemyQuery || !gameCamera) return Entity.Null;
            float pixelsPerMeter=Screen.height/(2f*Mathf.Max(1,gameCamera.orthographic?gameCamera.orthographicSize:cameraZoom));
            Entity best=Entity.Null;float bestDistance=float.MaxValue;
            using var units=enemyQuery.ToEntityArray(Allocator.Temp);
            using var transforms=enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            using var stats=enemyQuery.ToComponentDataArray<AbilityUnit>(Allocator.Temp);
            for(int i=0;i<units.Length;i++)
            {
                if(stats[i].Health<=0) continue;
                var transform=transforms[i];
                Vector3 screen=gameCamera.WorldToScreenPoint((Vector3)transform.Position);
                if(screen.z<0) continue;
                float radius=Mathf.Max(18,(CombatSimulationSystem.BodyRadius*transform.Scale+.35f)*pixelsPerMeter);
                float distance=Vector2.Distance(pointer,new Vector2(screen.x,screen.y));
                if(distance<=radius && distance<bestDistance) {bestDistance=distance;best=units[i];}
            }
            return best;
        }
        /// <summary>每帧让选中圈跟随目标；目标死亡或战斗结束时自动取消。</summary>
        private void UpdateSelectionView()
        {
            if(selectedEnemy==Entity.Null) return;
            if(Phase!=ExpeditionPhase.Battle || !Alive(selectedEnemy)) {ClearSelection();return;}
            var manager=session.World.EntityManager;
            var transform=manager.GetComponentData<LocalTransform>(selectedEnemy);
            var tier=manager.HasComponent<CombatEnemy>(selectedEnemy)?manager.GetComponentData<CombatEnemy>(selectedEnemy).Tier:EnemyTier.Normal;
            abilityIndicator.ShowSelection((Vector3)transform.Position,CombatSimulationSystem.BodyRadius*transform.Scale+.35f,
                tier==EnemyTier.Boss?BossSelection:tier==EnemyTier.Elite?EliteSelection:NormalSelection);
        }
        private void PublishTarget()
        {
            var ui=ExpeditionUI;if(!ui) return;
            if(selectedEnemy==Entity.Null) {ui.Visible("target.visible",false);return;}
            var manager=session.World.EntityManager;
            float now=manager.GetComponentData<AbilityRuntime>(session.Entity).Time;
            if(!TargetInfoBuilder.Build(manager,selectedEnemy,session,now,targetView)) {ClearSelection();return;}
            var v=targetView;
            ui.Visible("target.visible",true);
            ui.Texture("target.portrait",v.Portrait);
            ui.Text("target.name",v.Name);ui.Text("target.tier",v.TierLabel);ui.Text("target.subtitle",v.Subtitle);
            ui.Fill("target.health",v.HealthFraction);ui.Text("target.healthText",v.HealthText);
            ui.Text("target.damage",v.Damage);ui.Text("target.interval",v.AttackInterval);ui.Text("target.range",v.Range);
            ui.Text("target.speed",v.MoveSpeed);ui.Text("target.armor",v.Armor);ui.Text("target.bounty",v.Bounty);
            ui.Text("target.affixes",v.Affixes);ui.Text("target.abilities",v.Abilities);ui.Text("target.statuses",v.Statuses);
            ui.Text("target.description",v.Description);
        }
    }
}
