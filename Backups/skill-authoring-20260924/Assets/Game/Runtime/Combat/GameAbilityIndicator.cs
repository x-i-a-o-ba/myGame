using Roguelike.Abilities;
using Roguelike.SkillIndicators;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>会话编译配置的只读展示快照；视图不读取可变资源或ECS。</summary>
    public struct SkillAimPreview
    {
        public AimMode Aim;
        public TargetScope Scope;
        public AreaShape Shape;
        public DeliveryKind Delivery;
        public float CastRange,Range,Radius,HitRadius,HalfAngleDegrees;
    }
    /// <summary>读取装备技能第一段几何的瞄准预览。只决定输入目标，不结算效果。</summary>
    [DisallowMultipleComponent]
    public sealed class GameAbilityIndicator : MonoBehaviour
    {
        [SerializeField] private CircleIndicatorView rangeCircle,areaCircle;
        [SerializeField] private LineRenderer outline;
        [SerializeField] private Color previewColor=new Color(0.2f,0.83f,1f,0.9f);
        private const float RangeHeight=0.04f,AreaHeight=0.055f,OutlineHeight=0.075f;
        private const int ConeArcSegments=24;
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
            skill=definition; ActiveSlot=slot; IsAiming=true;
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
            if(skill.Delivery==DeliveryKind.LinearProjectile)
                DrawLine(origin,direction,skill.Range,skill.HitRadius);
            else if(skill.Scope==TargetScope.Area)
            {
                Vector3 center=skill.Aim==AimMode.Point || skill.Aim==AimMode.Unit?target:origin;
                switch(skill.Shape)
                {
                    case AreaShape.Circle: areaCircle.Show(new Vector3(center.x,AreaHeight,center.z),skill.Radius,previewColor); break;
                    case AreaShape.Cone: DrawCone(center,direction,skill.Range,skill.HalfAngleDegrees); break;
                    case AreaShape.Line: DrawLine(center,direction,skill.Range,skill.Radius); break;
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
        private void DrawCone(Vector3 origin, Vector3 forward, float range, float halfAngle)
        {
            const int vertexCount = ConeArcSegments + 3;
            if (outline.positionCount != vertexCount) outline.positionCount = vertexCount;
            outline.SetPosition(0, new Vector3(origin.x, OutlineHeight, origin.z));
            for (int i = 0; i <= ConeArcSegments; i++)
            {
                float degrees = Mathf.Lerp(-halfAngle, halfAngle, i / (float)ConeArcSegments);
                Vector3 ray = Quaternion.AngleAxis(degrees, Vector3.up) * forward;
                Vector3 point = origin + ray * range;
                outline.SetPosition(i + 1, new Vector3(point.x, OutlineHeight, point.z));
            }
            outline.SetPosition(vertexCount - 1, new Vector3(origin.x, OutlineHeight, origin.z));
            ShowOutline();
        }

        private void DrawLine(Vector3 origin, Vector3 forward, float range, float halfWidth)
        {
            if (outline.positionCount != 5) outline.positionCount = 5;
            Vector3 side = new Vector3(forward.z, 0f, -forward.x) * halfWidth;
            Vector3 far = origin + forward * range;
            SetOutlinePoint(0, origin - side);
            SetOutlinePoint(1, far - side);
            SetOutlinePoint(2, far + side);
            SetOutlinePoint(3, origin + side);
            SetOutlinePoint(4, origin - side);
            ShowOutline();
        }

        private void SetOutlinePoint(int index, Vector3 point) =>
            outline.SetPosition(index, new Vector3(point.x, OutlineHeight, point.z));

        private void ShowOutline()
        {
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

