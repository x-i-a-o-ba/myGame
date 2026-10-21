using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Roguelike.SkillIndicators;

namespace Roguelike.Abilities
{
    /// <summary>独立场景组合根：装配配置、ECS 会话、输入和展示。规则由 ECS/纯函数持有。</summary>
    public sealed class SkillDemoController : MonoBehaviour
    {
        [Header("资源与场景引用")]
        public SkillCatalog catalog;
        public Camera gameCamera;
        public Transform playerView;
        public Mesh unitMesh;
        public Material enemyMaterial,allyMaterial;
        public SkillDemoHud hud;
        public CircleIndicatorView rangeView,targetView,feedbackView;
        public CircleIndicatorView[] areaViews;
        public LineRenderer attackBeam;
        [Range(1,20000),Tooltip("重新开始时使用；默认一万 ECS 训练目标。")] public int enemyCount=10000;
        [Tooltip("超距默认截到边界；关闭后标红拒绝。")] public bool clampOutOfRange=true;
        public SkillDemoWorld Runtime { get; private set; }
        public int Aiming { get; private set; }=-1;
        public Entity Selected { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public bool AimValid { get; private set; }
        private SkillDemoInput input;
        private SkillDemoPresentation presentation;
        public string Message { get; private set; }="右键移动，自动普攻；Q/W/E 瞄准。";
        private void Start()
        {
            try
            {
                if(!catalog || !gameCamera || !hud || !playerView || !rangeView || !targetView || !feedbackView || !unitMesh || !attackBeam || areaViews.Length<8)
                    throw new InvalidOperationException("技能演示场景引用不完整，请检查 Skill Demo 根节点。");
                Runtime=new SkillDemoWorld(World.DefaultGameObjectInjectionWorld,catalog);
                input=new SkillDemoInput(this); presentation=new SkillDemoPresentation(this);
                hud.Command+=Command; Restart();
            }
            catch(Exception e) { Debug.LogException(e,this); enabled=false; }
        }
        public void Restart()
        {
            Cancel(); Selected=Entity.Null;
            Runtime.Reset(enemyCount,unitMesh,enemyMaterial,allyMaterial);
            presentation.Reset(); Message="Q 爆裂 / W 寒霜区域 / E 鼓舞；被动默认开启。";
        }
        private void Update()
        {
            if(Runtime==null) return;
            input.Tick();
        }
        private void LateUpdate() { if(Runtime!=null) presentation.Tick(); }
        public void SelectSkill(int slot)
        {
            if(slot<0 || slot>=catalog.skills.Length) return;
            Aiming=slot; AimValid=false; Message="左键确认；右键/Esc 取消。";
        }
        public void SetAim(Vector3 requested,bool usable)
        {
            if(Aiming<0) return;
            var state=Runtime.State; var origin=Runtime.Manager.GetComponentData<LocalTransform>(state.Hero).Position;
            var slot=Runtime.Manager.GetBuffer<AbilitySlot>(state.Hero)[Aiming];
            ref var skill=ref state.Library.Value.Skills[slot.Definition];
            var point=AbilityGeometry.Resolve(origin,requested,skill.CastRange,clampOutOfRange,out bool valid);
            if(skill.Delivery==SkillDelivery.SelfCircle) { point=origin; valid=true; }
            AimPoint=point; AimValid=usable && valid;
            if(!usable) { rangeView.Hide(); targetView.Hide(); return; }
            var color=AimValid?catalog.skills[Aiming].indicatorColor:Color.red;
            rangeView.Show(new Vector3(origin.x,0.03f,origin.z),skill.CastRange,color);
            targetView.Show(new Vector3(point.x,0.04f,point.z),AbilityGeometry.Radius(ref skill,slot.Level),color);
        }
        public bool Confirm()
        {
            if(Aiming<0 || !AimValid) return false;
            var state=Runtime.State; state.ClampRange=clampOutOfRange; Runtime.Manager.SetComponentData(Runtime.Session,state);
            Runtime.Cast(Aiming,AimPoint); Cancel(); return true;
        }
        public void Cancel() { Aiming=-1; AimValid=false; if(rangeView) rangeView.Hide(); if(targetView) targetView.Hide(); }
        public void Move(Vector3 point)
        {
            var unit=Runtime.Manager.GetComponentData<AbilityUnit>(Runtime.Hero);
            unit.Destination=new float3(Mathf.Clamp(point.x,-58,58),0.55f,Mathf.Clamp(point.z,-58,58));
            Runtime.Manager.SetComponentData(Runtime.Hero,unit);
        }
        public void Pick(Vector3 point)
        {
            Selected=Runtime.World.GetOrCreateSystemManaged<SkillSimulationSystem>().FindClosest(point,1.5f,0,false);
        }
        public void Command(SkillDemoCommand command)
        {
            if(Runtime==null) return;
            int n=(int)command;
            if(n<=2) { SelectSkill(n); return; }
            if(n<=5) { Runtime.Upgrade(n-3); return; }
            var state=Runtime.State;
            switch(command)
            {
                case SkillDemoCommand.PassiveStats: case SkillDemoCommand.PassivePoison:
                    int index=n-6; var hero=Runtime.Manager.GetComponentData<AbilityUnit>(Runtime.Hero);
                    Runtime.SetPassive(index,(hero.PassiveMask&(1u<<index))==0); break;
                case SkillDemoCommand.AutoAttack: state.AutoAttack=!state.AutoAttack; Runtime.Manager.SetComponentData(Runtime.Session,state); break;
                case SkillDemoCommand.MoveEnemies: state.MoveEnemies=!state.MoveEnemies; Runtime.Manager.SetComponentData(Runtime.Session,state); break;
                case SkillDemoCommand.Dispel: Runtime.Dispel(Selected==Entity.Null?Runtime.Hero:Selected); break;
                case SkillDemoCommand.Reset: Restart(); break;
                case SkillDemoCommand.Load10k: enemyCount=10000; Restart(); break;
                case SkillDemoCommand.DamageHero:
                    var unit=Runtime.Manager.GetComponentData<AbilityUnit>(Runtime.Hero); unit.Health=Mathf.Max(1,unit.Health-80);
                    Runtime.Manager.SetComponentData(Runtime.Hero,unit); break;
            }
        }
        public void SetMessage(string message) => Message=message;
        private void OnDestroy()
        {
            if(hud) hud.Command-=Command;
            Runtime?.Dispose(); Runtime=null;
        }
    }
}
