using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>左键选中敌人后的目标信息面板：头像配置、面板绑定、资源清单与只读快照组装。</summary>
    public static partial class CombatValidation
    {
        private const string TargetPanelPath="Assets/Resources/UI/Panels/UI.Game.TargetInfo.prefab";

        private static void ValidateTargetInfo()
        {
            var project=AssetDatabase.LoadAssetAtPath<EnemyRoster>(RosterPath);
            string missingPortrait=project && project.entries!=null && project.entries.Length>0?null:"敌人名册为空";
            if(missingPortrait==null)
                foreach(var entry in project.entries)
                    if(!entry.prefab || !entry.prefab.portrait) { missingPortrait=entry.prefab?entry.prefab.name:"空条目"; break; }
            Check(missingPortrait==null,"Every project enemy kind has a target panel portrait: "+missingPortrait);

            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TargetPanelPath);
            var panel=prefab?prefab.GetComponent<UIPanel>():null;
            Check(panel && panel.panelId==CombatHud.TargetPanel && panel.blocksPointer && !prefab.activeSelf,
                "Target info panel prefab matches its id, blocks the pointer and starts hidden");
            string missing=Missing(panel.texts,b=>b.key,b=>b.target,CombatHud.TargetKeys.Texts)
                ??Missing(panel.images,b=>b.key,b=>b.target,CombatHud.TargetKeys.Images)
                ??Missing(panel.fills,b=>b.key,b=>b.target,CombatHud.TargetKeys.Fills)
                ??Missing(panel.toggles,b=>b.key,b=>b.target,CombatHud.TargetKeys.Toggles);
            Check(missing==null,"Target panel binds every key the HUD publishes: "+missing);
            bool close=false;
            foreach(var action in prefab.GetComponentsInChildren<UIAction>(true)) close|=action.command==CombatHud.TargetKeys.CloseCommand;
            Check(close,"Target panel has a close button");
            Check(ResourcePaths.Resolve(CombatHud.TargetPanel)=="UI/Panels/UI.Game.TargetInfo","Resource catalog registers the target panel");

            GameObject root=null; Texture2D portrait=null;
            using(var assets=new Resources())
            using(var world=new World("TargetInfoValidation"))
            try
            {
                var attack=assets.New<AutoAttackDefinition>(); attack.stableId="attack.validation_target"; attack.displayName="验证爪击";
                attack.delivery=AttackDelivery.Melee; attack.range=1.5f; attack.acquisitionRange=1.5f; attack.baseAttackTime=1; attack.attackPoint=0.25f; attack.backswing=0.5f;
                var slow=assets.Buff("buff.validation_slow",4); slow.displayName="验证减速";
                root=new GameObject("validation target");
                var archetype=root.AddComponent<EnemyArchetype>();
                archetype.configId=3950; archetype.stableId="enemy.validation_target"; archetype.displayName="验证怪物";
                archetype.description="说明文字"; archetype.behavior=EnemyBehavior.Melee; archetype.attack=attack;
                portrait=new Texture2D(2,2); archetype.portrait=portrait;
                var manager=world.EntityManager;
                var entity=manager.CreateEntity(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform),typeof(AutoAttackData),typeof(AutoAttackState),typeof(EnemyBehaviorData));
                manager.SetComponentData(entity,new AbilityUnit{Team=1,Health=30,MaxHealth=120,BaseSpeed=2,Speed=1.4f,BaseAttack=10,Attack=10,AttackSpeedMultiplier=0.5f});
                manager.SetComponentData(entity,new CombatEnemy{ConfigId=3950,Kind=1,Experience=40,Reach=0.5f,Tier=EnemyTier.Elite,Wave=4});
                manager.SetComponentData(entity,LocalTransform.FromPositionRotationScale(new float3(6,0,0),quaternion.identity,1.5f));
                manager.SetComponentData(entity,attack.Compile());
                manager.SetComponentData(entity,new AutoAttackState{Phase=AttackPhase.Windup});
                manager.SetComponentData(entity,new EnemyBehaviorData{Behavior=EnemyBehavior.Melee});
                var buffs=manager.AddBuffer<ActiveBuff>(entity);
                buffs.Add(new ActiveBuff{Definition=0,Stacks=2,ExpiresAt=12.5f,Scale=1});
                buffs.Add(new ActiveBuff{Definition=0,Stacks=1,ExpiresAt=float.MaxValue,Scale=1});
                var context=new TargetInfoContext{KindNames=new[]{"其他","验证怪物"},KindArchetypes=new EnemyArchetype[]{null,archetype},
                    Buffs=new[]{slow},HeroPosition=float3.zero,HeroAttackRange=5.4f,Now=10};
                var view=new TargetInfoView();
                Check(TargetInfoBuilder.Build(manager,entity,context,view) && view.Visible,"Target info builds from a live enemy entity");
                Check(view.Name=="验证怪物" && view.Tier==EnemyTier.Elite && view.TierLabel=="精英" && view.Subtitle=="第 4 波 · 近战追击" &&
                    view.Portrait==portrait && view.AttackName=="验证爪击" && view.Description=="说明文字",
                    "Target info shows kind name, tier, wave, behavior, portrait and attack module");
                Check(Mathf.Approximately(view.HealthFraction,0.25f) && view.HealthText=="30 / 120","Target info reads authoritative health");
                Check(view.AttackInterval.Contains("2.00 秒") && view.AttackInterval.Contains("+100%") && view.MoveSpeed.Contains("-30%") && view.Damage=="10",
                    "Target info scales attack interval by attack speed and marks modified stats");
                Check(view.Range=="2 米" && view.Distance=="6.0 米" && view.Experience=="40","Target info shows reach including body size, distance and experience");
                Check(view.InHeroRange,"Hero attack range counts the target body radius");
                Check(view.AttackStatus.Contains("前摇中") && view.Trait.StartsWith("近战追击",StringComparison.Ordinal),"Target info shows attack phase and behavior trait");
                Check(view.StatusCount==2 && view.Statuses[0]=="验证减速×2 2.5秒" && view.Statuses[1]=="验证减速 常驻","Target info lists buffs with stacks and remaining time");
                context.HeroAttackRange=5;
                Check(TargetInfoBuilder.Build(manager,entity,context,view) && !view.InHeroRange,"Target outside hero attack range is reported");
                var unit=manager.GetComponentData<AbilityUnit>(entity); unit.Health=0; manager.SetComponentData(entity,unit);
                Check(!TargetInfoBuilder.Build(manager,entity,context,view) && !view.Visible,"Dead target hides the target panel");
                manager.DestroyEntity(entity);
                Check(!TargetInfoBuilder.Build(manager,entity,context,view) && !TargetInfoBuilder.Build(manager,Entity.Null,context,view),
                    "Destroyed or empty selection is rejected safely");
            }
            finally
            {
                if(root) UnityEngine.Object.DestroyImmediate(root);
                if(portrait) UnityEngine.Object.DestroyImmediate(portrait);
            }
        }

        private static string Missing<T>(T[] bindings,Func<T,string> key,Func<T,UnityEngine.Object> target,string[] keys)
        {
            foreach(string expected in keys)
            {
                bool found=false;
                if(bindings!=null) foreach(var binding in bindings) if(key(binding)==expected && target(binding)) { found=true; break; }
                if(!found) return expected;
            }
            return null;
        }
    }
}
