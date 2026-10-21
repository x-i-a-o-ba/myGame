using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.SkillIndicators;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>读取装备技能第一段几何的瞄准预览。只决定输入目标，不结算效果。</summary>
    [DisallowMultipleComponent]
    public sealed class GameAbilityIndicator : MonoBehaviour
    {
        [SerializeField] private CircleIndicatorView rangeCircle,areaCircle;
        [SerializeField] private LineRenderer outline;
        private Color previewColor;
        private const float RangeHeight=0.04f,AreaHeight=0.055f,OutlineHeight=0.075f;
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

        private void OnDisable() => Cancel();
    }
}

