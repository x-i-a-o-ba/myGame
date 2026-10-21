using Roguelike.SkillIndicators;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>
    /// Game 场景的施法预览。只维护瞄准状态和地面几何；伤害、冷却及是否接受命令由战斗会话决定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAbilityIndicator : MonoBehaviour
    {
        [Header("场景中的固定展示节点")]
        [SerializeField] private CircleIndicatorView rangeCircle;
        [SerializeField] private CircleIndicatorView areaCircle;
        [SerializeField] private LineRenderer outline;
        [SerializeField] private Color previewColor = new Color(0.2f, 0.83f, 1f, 0.9f);

        private const float RangeHeight = 0.04f;
        private const float AreaHeight = 0.055f;
        private const float OutlineHeight = 0.075f;
        private const int ConeArcSegments = 24;

        private CombatAbilityId activeAbility;
        private Vector3 target;
        private Vector3 direction;

        public bool IsAiming { get; private set; }
        public CombatAbilityId ActiveAbility => activeAbility;
        public bool CanConfirm { get; private set; }
        public bool HasBindings => rangeCircle != null && areaCircle != null && outline != null;

        private void Awake()
        {
            if (outline != null)
            {
                outline.useWorldSpace = true;
                outline.loop = false;
            }
            Cancel();
        }

        /// <summary>Q 是自身技能，无需指示器。W/E/R 进入等待有效地面指针的瞄准状态。</summary>
        public void BeginAim(CombatAbilityId id)
        {
            Cancel();
            if (id == CombatAbilityId.Q) return;
            if (!HasBindings)
            {
                Debug.LogError("Game 技能指示器缺少场景引用：距离圈、落点圈和轮廓线必须绑定。", this);
                return;
            }
            activeAbility = id;
            IsAiming = true;
        }

        /// <summary>
        /// 每帧根据当前玩家和鼠标地面位置更新预览。usable=false 时丢弃旧落点，防止在 UI 上确认上一次位置。
        /// 传入的 origin 可在角色高度；预览几何放在 Game 的 y=0 地面上。
        /// </summary>
        public void UpdateAim(Vector3 origin, Vector3 pointer, bool usable,
            in CombatAbilitySettings settings, float arenaHalfSize)
        {
            if (!IsAiming) return;

            CanConfirm = false;
            HideTargetViews();

            if (activeAbility == CombatAbilityId.E)
                rangeCircle.Show(new Vector3(origin.x, RangeHeight, origin.z),
                    Mathf.Max(0.1f, settings.ERange), previewColor, 0.7f);
            else
                rangeCircle.Hide();

            if (!usable) return;

            var planarOrigin = new Vector3(origin.x, origin.y, origin.z);
            var planarPointer = new Vector3(pointer.x, origin.y, pointer.z);
            var offset = planarPointer - planarOrigin;
            offset.y = 0f;

            if (activeAbility == CombatAbilityId.E)
            {
                // 与 ECS 目标限制一致：先截施法距离，再限制到场地内侧 2 米。
                target = CircleTargeting.Resolve(planarOrigin, planarPointer,
                    Mathf.Max(0.1f, settings.ERange), true, out _);
                float edge = Mathf.Max(1f, arenaHalfSize - 2f);
                target.x = Mathf.Clamp(target.x, -edge, edge);
                target.z = Mathf.Clamp(target.z, -edge, edge);
                direction = target - planarOrigin;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.0001f) direction.Normalize();
                // 允许 E 在自己脚下落点；该方向为零时，战斗结算应使用 Target。
                areaCircle.Show(new Vector3(target.x, AreaHeight, target.z),
                    Mathf.Max(0.1f, settings.EAreaRadius), previewColor);
                CanConfirm = true;
                return;
            }

            if (offset.sqrMagnitude <= 0.0001f) return;
            direction = offset.normalized;

            if (activeAbility == CombatAbilityId.W)
            {
                float range = Mathf.Max(0.1f, settings.WRange);
                target = planarOrigin + direction * range;
                DrawCone(planarOrigin, direction, range,
                    Mathf.Clamp(settings.WHalfAngleDegrees, 1f, 89f));
            }
            else if (activeAbility == CombatAbilityId.R)
            {
                float range = Mathf.Max(0.1f, settings.RRange);
                target = planarOrigin + direction * range;
                DrawLine(planarOrigin, direction, range, Mathf.Max(0.1f, settings.RHitRadius));
            }
            else return;

            CanConfirm = true;
        }

        /// <summary>只返回瞄准结果，不触碰 ECS。成功后隐藏预览；调用方先读取 ActiveAbility。</summary>
        public bool TryConfirm(out Vector3 confirmedTarget, out Vector3 confirmedDirection)
        {
            confirmedTarget = default;
            confirmedDirection = default;
            if (!IsAiming || !CanConfirm) return false;
            confirmedTarget = target;
            confirmedDirection = direction;
            Cancel();
            return true;
        }

        public void Cancel()
        {
            IsAiming = false;
            CanConfirm = false;
            activeAbility = CombatAbilityId.Q;
            target = default;
            direction = default;
            if (rangeCircle != null) rangeCircle.Hide();
            HideTargetViews();
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
