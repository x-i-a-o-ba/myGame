using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.SkillIndicators;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// 读取装备技能第一段几何的瞄准预览。只决定输入目标，不结算效果。
    /// 不在技能瞄准时，同一组圆圈复用为 Dota 2 式指令预览：A 键攻击瞄准的攻击距离圈、悬停敌人与攻击目标圈。
    /// 左键选中敌人的选中圈由目标圈在运行时克隆出独立实例，与瞄准/指令预览互不干扰。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAbilityIndicator : MonoBehaviour
    {
        [SerializeField] private CircleIndicatorView rangeCircle,areaCircle;
        [SerializeField] private LineRenderer outline;
        private Color previewColor;
        private CircleIndicatorView selectionCircle;
        private const float RangeHeight=0.04f,AreaHeight=0.055f,OutlineHeight=0.075f,SelectionHeight=0.05f;
        // 容纳 64 段胶囊和闭合端点，瞄准期间复用，避免每帧创建顶点数组。
        private readonly List<Vector3> outlinePoints=new List<Vector3>(68);
        private SkillAimPreview skill;
        private Vector3 target,direction;
        public int ActiveSlot {get; private set;}
        public bool IsAiming {get; private set;}
        public bool CanConfirm {get; private set;}
        public bool HasBindings=>rangeCircle && areaCircle && outline;
        private void Awake() { if(outline) { outline.useWorldSpace=true; outline.loop=false; } Cancel(); }
        public void BeginAim(int slot,SkillAimPreview definition)
        {
            Cancel();
            if(!HasBindings || definition.Aim==AimMode.Self) return;
            skill=definition; previewColor=definition.IndicatorColor; ActiveSlot=slot; IsAiming=true;
        }
        public void UpdateAim(Vector3 origin,Vector3 pointer,bool usable,float arenaHalfSize)
        {
            if(!IsAiming) return;
            CanConfirm=false; HideTargetViews(); rangeCircle.Hide();
            if(!usable) return;
            target=(Vector3)SkillTargeting.ClampPoint(origin,pointer,skill.CastRange);
            float edge=Mathf.Max(1,arenaHalfSize-2);
            if(skill.Aim==AimMode.Point || skill.Aim==AimMode.Unit)
            { target.x=Mathf.Clamp(target.x,-edge,edge); target.z=Mathf.Clamp(target.z,-edge,edge); }
            direction=target-origin; direction.y=0;
            if(direction.sqrMagnitude>0.00001f) direction.Normalize();
            else if(skill.Aim==AimMode.Direction) return;
            else direction=Vector3.forward;
            rangeCircle.Show(new Vector3(origin.x,RangeHeight,origin.z),Mathf.Max(0.1f,skill.CastRange),previewColor,0.7f);
            Vector3 deliveryDirection=SkillPreviewGeometry.DeliveryDirection(skill,origin,target,direction);
            if(skill.Delivery==DeliveryKind.LinearProjectile)
            {
                SkillPreviewGeometry.ProjectileOutline(skill,origin,deliveryDirection,outlinePoints);
                ShowOutline();
            }
            else if(skill.Scope==TargetScope.Area)
            {
                Vector3 center=SkillPreviewGeometry.AreaCenter(skill,origin,target);
                if(skill.Shape==AreaShape.Circle)
                    areaCircle.Show(new Vector3(center.x,AreaHeight,center.z),skill.Radius,previewColor);
                else
                {
                    SkillPreviewGeometry.AreaOutline(skill,center,deliveryDirection,outlinePoints);
                    ShowOutline();
                }
            }
            else if(skill.Scope==TargetScope.Single)
                areaCircle.Show(new Vector3(target.x,AreaHeight,target.z),0.6f,previewColor);
            CanConfirm=true;
        }
        public bool TryConfirm(out Vector3 confirmedTarget,out Vector3 confirmedDirection)
        {
            confirmedTarget=target; confirmedDirection=direction;
            if(!IsAiming || !CanConfirm) return false;
            Cancel(); return true;
        }
        public void Cancel()
        {
            IsAiming=false; CanConfirm=false; ActiveSlot=-1; skill=default;
            target=default; direction=default;
            if(rangeCircle) rangeCircle.Hide(); HideTargetViews();
        }
        /// <summary>指令预览：showRange 显示以 origin 为中心的攻击距离圈，showUnit 在敌人脚下显示目标圈。技能瞄准期间忽略。</summary>
        public void ShowOrderPreview(Vector3 origin,float attackRange,bool showRange,Vector3 unit,float unitRadius,bool showUnit,Color unitColor)
        {
            if(IsAiming || !HasBindings) return;
            if(showRange) rangeCircle.Show(new Vector3(origin.x,RangeHeight,origin.z),Mathf.Max(0.1f,attackRange),AttackRangeColor,0.55f);
            else rangeCircle.Hide();
            if(showUnit) areaCircle.Show(new Vector3(unit.x,AreaHeight,unit.z),Mathf.Max(0.2f,unitRadius),unitColor,0.95f);
            else areaCircle.Hide();
            outline.enabled=false;
        }
        /// <summary>隐藏指令预览；技能瞄准期间不影响瞄准显示。</summary>
        public void HideOrderPreview()
        {
            if(IsAiming) return;
            if(rangeCircle) rangeCircle.Hide();
            HideTargetViews();
        }
        private static readonly Color AttackRangeColor=new Color(1f,0.35f,0.28f,1);
        /// <summary>选中圈：在选中敌人脚下显示；首次使用时克隆目标圈，技能瞄准与取消不会隐藏它。</summary>
        public void ShowSelection(Vector3 center,float radius,Color color)
        {
            if(!areaCircle) return;
            if(!selectionCircle)
            {
                selectionCircle=Instantiate(areaCircle,areaCircle.transform.parent);
                selectionCircle.name="Selection Circle";
            }
            selectionCircle.Show(new Vector3(center.x,SelectionHeight,center.z),Mathf.Max(0.2f,radius),color,0.9f);
        }
        public void HideSelection() { if(selectionCircle) selectionCircle.Hide(); }
        private void ShowOutline()
        {
            if(outline.positionCount!=outlinePoints.Count) outline.positionCount=outlinePoints.Count;
            for(int i=0;i<outlinePoints.Count;i++)
            {
                Vector3 point=outlinePoints[i];
                outline.SetPosition(i,new Vector3(point.x,OutlineHeight,point.z));
            }
            outline.startColor = previewColor;
            outline.endColor = previewColor;
            outline.enabled = true;
        }

        private void HideTargetViews()
        {
            if (areaCircle != null) areaCircle.Hide();
            if (outline != null) outline.enabled = false;
        }

        private void OnDisable() { Cancel(); HideSelection(); }
        private void OnDestroy() { if(selectionCircle) Destroy(selectionCircle.gameObject); }
    }
}

