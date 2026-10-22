using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>技能悬停提示与移动中攻击：英雄步进纯函数、提示文字组装、提示框与悬停源预制体、资源清单。</summary>
    public static partial class CombatValidation
    {
        private const string TooltipPanelPath="Assets/Resources/UI/Panels/UI.Game.AbilityTooltip.prefab";
        private const string BottomHudPath="Assets/Resources/UI/Panels/UI.Game.BottomHUD.prefab";

        private static void ValidateMoveAttack()
        {
            // 时间步长与节奏取二进制可精确表示的值，避免浮点累加误差。
            var data=new AutoAttackData{Delivery=AttackDelivery.Projectile,Range=5,AcquisitionRange=10,BaseAttackTime=1,AttackPoint=0.25f,Backswing=0.5f,
                ProjectileSpeed=40,RangeBuffer=1,AutoAcquire=true};
            var hero=new AbilityUnit{Team=0,Health=100,MaxHealth=100,BaseSpeed=4,Speed=4,AttackSpeedMultiplier=1};
            // 目标在正前方攻击距离内，目的地在右侧：移动方向与攻击方向垂直。
            var motion=new HeroMotion{HasTarget=true,TargetPosition=new float3(0,0,3),TargetRadius=0.5f,Chase=false,Travel=true,Destination=new float3(10,0,0)};
            var player=new CombatPlayer{Position=float3.zero,Facing=new float3(0,0,1)}; var attack=new AutoAttackState();
            var result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,motion,true,math.radians(90),100,false,0.125f);
            Check(result==AutoAttackEvent.Started && attack.Phase==AttackPhase.Windup && Mathf.Approximately(player.Position.x,0.5f),
                "Attack while moving starts an attack and moves in the same frame");
            result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,motion,true,math.radians(90),100,false,0.125f);
            Check(result==AutoAttackEvent.Released && Mathf.Approximately(player.Position.x,1f),"Attack windup does not root the hero while moving");
            Check(math.dot(player.Facing,new float3(0,0,1))>0.95f,"Hero keeps facing the attack target while moving sideways");
            var blocked=hero; blocked.Controls=ControlFlags.MovementBlocked;
            player=new CombatPlayer{Position=float3.zero,Facing=new float3(0,0,1)}; attack=new AutoAttackState();
            result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,blocked,motion,true,math.radians(90),100,false,0.125f);
            Check(result==AutoAttackEvent.Started && math.all(player.Position==float3.zero),"Root stops movement but not attacks while moving");
            player=new CombatPlayer{Position=float3.zero,Facing=new float3(0,0,1)}; attack=new AutoAttackState();
            result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,motion,true,math.radians(90),100,true,0.125f);
            Check(result==AutoAttackEvent.None && attack.Phase==AttackPhase.Idle && math.all(player.Position==float3.zero),"Cast windup blocks both movement and attacks");
            var far=motion; far.TargetPosition=new float3(0,0,8);
            player=new CombatPlayer{Position=float3.zero,Facing=new float3(0,0,1)}; attack=new AutoAttackState();
            result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,far,true,math.radians(90),100,false,0.125f);
            Check(result==AutoAttackEvent.None && Mathf.Approximately(player.Position.x,0.5f),"Move order never chases targets outside attack range");
            // 传统模式：移动取消前摇，转身对齐后才移动。
            player=new CombatPlayer{Position=float3.zero,Facing=new float3(0,0,1)}; attack=new AutoAttackState();
            var stand=new HeroMotion{HasTarget=true,TargetPosition=new float3(0,0,3),TargetRadius=0.5f};
            Check(CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,stand,false,0,100,false,0.125f)==AutoAttackEvent.Started,"Classic mode starts the attack while standing");
            var walk=new HeroMotion{Travel=true,Destination=new float3(10,0,0)};
            result=CombatSimulationSystem.StepMotion(ref player,ref attack,data,hero,walk,false,0,100,false,0.125f);
            Check(result==AutoAttackEvent.Cancelled && attack.Phase==AttackPhase.Idle && player.Position.x>0,"Classic mode cancels the windup when moving");
            var project=AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroPath);
            Check(project && project.attackWhileMoving,"Project hero attacks while moving");
        }

        private static void ValidateAbilityTooltip()
        {
            Check(UIHoverAction.ExitValue(0)==-1 && UIHoverAction.ExitValue(CombatHud.PassiveSlot)==-1-CombatHud.PassiveSlot,"Hover exit values are negative and distinct");
            using(var assets=new Resources())
            {
                var damage=assets.Damage(40);
                var slow=assets.Buff("buff.validation_tooltip_slow",2.5f); slow.displayName="验证减速";
                slow.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.MoveSpeed,operation=ModifierOperation.Percent,value=-0.35f}};
                var stun=assets.Buff("buff.validation_tooltip_stun",1.5f); stun.displayName="验证眩晕";
                stun.controls=ControlFlags.MovementBlocked|ControlFlags.AttackBlocked|ControlFlags.CastBlocked;
                var applySlow=assets.New<ApplyBuffEffect>(); applySlow.buff=slow;
                var applyStun=assets.New<ApplyBuffEffect>(); applyStun.buff=stun;
                var skill=assets.Skill("validation_tooltip",AimMode.Direction,new SkillStage{scope=TargetScope.Area,shape=AreaShape.Cone,range=22,halfAngleDegrees=35,
                    effects=new EffectDefinition[]{damage,applySlow,applyStun}});
                skill.cooldown=5; skill.manaCost=10; skill.castRange=22;
                var library=assets.Catalog(new[]{skill},new[]{slow,stun}).Compile();
                try
                {
                    var buffs=new[]{slow,stun};
                    var context=new SkillTooltipContext{Hotkey="W",Name="验证扇击",Description="说明文字",Track=SkillLevelTrack.Basic,Buffs=buffs,
                        Slot=new AbilitySlot{Definition=0,Level=2,Charges=1},Owner=new AbilityUnit{Mana=100,MaxMana=100},
                        Progression=new HeroProgression{Level=5,SkillPoints=1}};
                    var view=new AbilityTooltipView();
                    AbilityTooltipBuilder.BuildSkill(ref library.Value,ref library.Value.Skills[0],context,view);
                    string white=AbilityTooltipBuilder.Current,green=AbilityTooltipBuilder.Next,gray=AbilityTooltipBuilder.Other;
                    Check(view.Visible && !view.Passive && view.Level==2 && view.MaxLevel==4 && view.LevelText=="等级 2 / 4" && view.Hotkey=="W" && view.Description=="说明文字",
                        "Skill tooltip shows name, hotkey, level and description");
                    Check(view.Values.Contains($"<color={white}><b>76</b></color>") && view.Values.Contains($"<color={green}>112</color>") &&
                          view.Values.Contains($"<color={gray}>40</color>") && view.Values.Contains($"<color={gray}>148</color>"),
                        "Damage series lists every level with the current level white and the learnable next level green");
                    Check(view.Values.Contains("角度") && view.Values.Contains("70") && view.Values.Contains("22"),"Tooltip shows cone length and angle");
                    Check(view.Values.Contains("验证减速") && view.Values.Contains($"-<color={gray}>35</color>") && view.Values.Contains($"持续 <color={white}><b>2.5</b></color> 秒"),
                        "Buff line shows modifier per level and duration");
                    Check(view.Values.Contains("验证眩晕") && view.Values.Contains("眩晕（无法移动、攻击和施法）"),"Control buffs are named like Dota (stun)");
                    Check(view.Properties.Contains("指向方向") && view.Properties.Contains("敌方单位") && view.Properties.Contains("伤害") && view.Properties.Contains("减速"),
                        "Properties list targeting, affected units and effect categories");
                    Check(view.Cooldown.Contains($"<color={white}><b>4.6</b></color>") && view.Cooldown.Contains($"<color={green}>4.2</color>") &&
                          view.Mana.Contains($"<color={white}><b>11</b></color>"),"Cooldown and mana show per-level values");
                    Check(view.Status.Contains("就绪") && view.Status.Contains("可升级"),"Ready learnable skill reports ready and upgrade hints");
                    context.Progression.SkillPoints=0;
                    AbilityTooltipBuilder.BuildSkill(ref library.Value,ref library.Value.Skills[0],context,view);
                    Check(!view.Values.Contains(green) && !view.Status.Contains("可升级"),"Without skill points no next level is highlighted");
                    context.Slot.ReadyAt=3; context.Now=1;
                    AbilityTooltipBuilder.BuildSkill(ref library.Value,ref library.Value.Skills[0],context,view);
                    Check(view.Status.Contains("冷却中") && !view.Status.Contains("就绪"),"Cooling skill reports remaining cooldown");
                    context.Slot=new AbilitySlot{Definition=0,Level=1,Charges=1}; context.Now=0; context.Owner.SkillAmplify=0.5f; context.Owner.CooldownReduction=0.5f;
                    AbilityTooltipBuilder.BuildSkill(ref library.Value,ref library.Value.Skills[0],context,view);
                    Check(view.Values.Contains($"<color={white}><b>60</b></color>") && view.Cooldown.Contains($"<color={white}><b>2.5</b></color>") &&
                          view.Notes.Contains("技能增强") && view.Notes.Contains("冷却缩减"),"Tooltip numbers include skill amplify and cooldown reduction");
                    context.Slot=new AbilitySlot{Definition=0,Level=0}; context.Owner=new AbilityUnit{Mana=100,MaxMana=100};
                    context.Progression=new HeroProgression{Level=1,SkillPoints=1};
                    AbilityTooltipBuilder.BuildSkill(ref library.Value,ref library.Value.Skills[0],context,view);
                    Check(view.Level==0 && view.LevelText.Contains("未学习") && view.Status.Contains("可学习") && view.Values.Contains($"<color={green}>40</color>"),
                        "Unlearned skill shows learn hint and highlights level 1");
                }
                finally { library.Dispose(); }
            }
            var data=new AutoAttackData{Delivery=AttackDelivery.Projectile,Range=20,AcquisitionRange=25,BaseAttackTime=0.12f,AttackPoint=0.03f,Backswing=0.06f,
                ProjectileSpeed=60,RangeBuffer=1,AutoAcquire=true,CountsAsBasicAttackHit=true};
            var unit=new AbilityUnit{Attack=51,BaseAttack=30,AttackSpeedMultiplier=1.2f};
            var passive=new AbilityTooltipView();
            AbilityTooltipBuilder.BuildAttack(data,new AutoAttackState(),unit,null,true,true,passive);
            Check(passive.Visible && passive.Passive && passive.Hotkey=="" && passive.Values.Contains("<b>0.10</b>") && passive.Values.Contains("<b>51</b>") &&
                  passive.Values.Contains("基础 30"),"Passive tooltip shows attack damage and attack-speed-scaled interval");
            Check(passive.Notes.Contains("移动中攻击") && passive.Status.Contains("攻击中"),"Passive tooltip explains attacking while moving");
            AbilityTooltipBuilder.BuildAttack(data,new AutoAttackState(),unit,null,false,false,passive);
            Check(passive.Notes.Contains("移动会取消") && passive.Status.Contains("索敌"),"Classic mode tooltip explains move cancel");

            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPanelPath);
            var panel=prefab?prefab.GetComponent<UIPanel>():null;
            Check(panel && panel.panelId==CombatHud.TooltipPanel && !panel.blocksPointer && !prefab.activeSelf,
                "Tooltip panel prefab matches its id, never blocks the pointer and starts hidden");
            string missing=Missing(panel.texts,b=>b.key,b=>b.target,CombatHud.TooltipKeys.Texts)
                ??Missing(panel.images,b=>b.key,b=>b.target,CombatHud.TooltipKeys.Images)
                ??Missing(panel.toggles,b=>b.key,b=>b.target,CombatHud.TooltipKeys.Toggles);
            Check(missing==null,"Tooltip panel binds every key the HUD publishes: "+missing);
            bool raycast=false;
            foreach(var graphic in prefab.GetComponentsInChildren<Graphic>(true)) raycast|=graphic.raycastTarget;
            Check(!raycast,"Tooltip graphics never receive raycasts (no hover flicker)");
            var layout=prefab.GetComponent<UITooltip>();
            bool sections=layout && layout.sections!=null && layout.sections.Length>=8;
            if(sections) foreach(var section in layout.sections) sections&=section && section.parent==prefab.transform;
            Check(sections,"Tooltip layout lists its sections as direct children");
            Check(ResourcePaths.Resolve(CombatHud.TooltipPanel)=="UI/Panels/UI.Game.AbilityTooltip","Resource catalog registers the tooltip panel");
            ValidateTooltipLayout(prefab);

            var hud=AssetDatabase.LoadAssetAtPath<GameObject>(BottomHudPath);
            int hovers=0;
            if(hud)
                foreach(var hover in hud.GetComponentsInChildren<UIHoverAction>(true))
                {
                    if(hover.command!=CombatHud.HoverCommand || hover.value<0 || hover.value>CombatHud.PassiveSlot) continue;
                    bool target=false;
                    foreach(var graphic in hover.GetComponentsInChildren<Graphic>(true)) target|=graphic.raycastTarget;
                    if(target) hovers|=1<<hover.value;
                }
            Check(hovers==(1<<(CombatHud.PassiveSlot+1))-1,"Bottom HUD has hover sources with raycast targets for Q/W/E/R and the passive");

            var project=AssetDatabase.LoadAssetAtPath<HeroDefinition>(HeroPath);
            string undocumented=project && project.skills!=null && project.skills.Length>0?null:"英雄没有技能";
            if(undocumented==null)
                foreach(var definition in project.skills)
                    if(!definition || string.IsNullOrWhiteSpace(definition.description)) { undocumented=definition?definition.name:"空槽"; break; }
            Check(undocumented==null,"Every project hero skill has a tooltip description: "+undocumented);
        }

        /// <summary>在临时矩形下实例化提示框（不进入任何场景画布）：长说明撑高提示框，隐藏分段后高度回落。</summary>
        private static void ValidateTooltipLayout(GameObject prefab)
        {
            var canvas=new GameObject("tooltip validation area",typeof(RectTransform));
            try
            {
                var area=(RectTransform)canvas.transform; area.sizeDelta=new Vector2(1920,1080);
                var instance=(GameObject)UnityEngine.Object.Instantiate(prefab,area,false); instance.SetActive(true);
                var layout=instance.GetComponent<UITooltip>(); var panel=instance.GetComponent<UIPanel>();
                Text description=null;
                foreach(var binding in panel.texts) if(binding.key==CombatHud.TooltipKeys.Description) description=binding.target as Text;
                Check(description && description.horizontalOverflow==HorizontalWrapMode.Wrap,"Tooltip description wraps");
                description.gameObject.SetActive(true);
                description.text="短说明"; layout.Refresh(); float shortHeight=layout.Height;
                description.text=string.Concat(System.Linq.Enumerable.Repeat("很长的技能说明会自动换行并撑高提示框。",12)); layout.Refresh(); float longHeight=layout.Height;
                description.gameObject.SetActive(false); layout.Refresh(); float hidden=layout.Height;
                Check(longHeight>shortHeight+40 && hidden<shortHeight,"Tooltip height follows wrapped text and skips hidden sections");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }
    }
}
