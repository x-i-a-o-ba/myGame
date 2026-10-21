using UnityEngine;

namespace Roguelike.SkillIndicators
{
    /// <summary>薄场景桥接：瞄准状态连接距离规则、展示、HUD 与反馈，不计算伤害。</summary>
    public sealed class IndicatorDemoController : MonoBehaviour
    {
        [Header("场景引用")]
        [SerializeField] private Transform caster;
        [SerializeField] private CircleSkillPreset[] skills;
        [SerializeField] private CircleIndicatorView rangeView, areaView;
        [SerializeField] private IndicatorFeedback feedback;
        [SerializeField] private IndicatorDemoHud hud;
        [Header("超出距离行为")]
        [SerializeField, Tooltip("开启：落点限制到施法距离边界，可直接确认。关闭：超距落点标红并禁止施放。运行时可调整。")]
        private bool clampOutOfRange = true;
        private int selected = -1;
        private Vector3 requested;
        private bool hasPoint;
        public bool ClampOutOfRange { get => clampOutOfRange; set => clampOutOfRange = value; }
        public bool IsAiming => selected >= 0;
        public Vector3 CasterPosition => caster.position;
        public bool CanCast { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 LastCastPosition { get; private set; }
        public int CastCount { get; private set; }
        public CircleSkillPreset ActiveSkill => IsAiming ? skills[selected] : null;

        private void OnEnable() { if (hud) hud.SkillRequested += SelectSkill; }
        private void Start() => CancelAim();
        private void OnDisable()
        {
            if (hud) hud.SkillRequested -= SelectSkill;
            if (rangeView) rangeView.Hide();
            if (areaView) areaView.Hide();
            if (feedback) feedback.Stop();
            selected = -1; CanCast = false;
        }

        public void SelectSkill(int index)
        {
            if (index < 0 || index >= skills.Length || !skills[index]) return;
            selected = index; hasPoint = false; CanCast = false;
            feedback.Stop(); areaView.Hide();
            RefreshAim();
        }
        /// <summary>鼠标在 UI 或不能投影时使当前落点失效，避免确认旧位置。</summary>
        public void SetPointer(Vector3 position, bool usable)
        {
            if (!IsAiming) return;
            requested = position; hasPoint = usable; RefreshAim();
        }
        private void Update() { if (IsAiming) RefreshAim(); }
        private void RefreshAim()
        {
            var skill = ActiveSkill;
            rangeView.Show(caster.position + Vector3.up * 0.04f, skill.castRange, skill.color, 0.7f);
            if (!hasPoint)
            {
                CanCast = false; areaView.Hide();
                hud.ShowStatus("鼠标移到地面开始瞄准", Parameters(skill)); return;
            }
            Target = CircleTargeting.Resolve(caster.position, requested, skill.castRange, clampOutOfRange, out bool valid);
            CanCast = valid;
            areaView.Show(Target + Vector3.up * 0.055f, skill.areaRadius, valid ? skill.color : new Color(1,0.18f,0.16f));
            hud.ShowStatus(valid ? "左键确认 · 右键 / Esc 取消" : "超出施法距离 · 无法施放", Parameters(skill));
        }
        private string Parameters(CircleSkillPreset skill) => $"{skill.displayName}  距离 {skill.castRange:0.#}m / 半径 {skill.areaRadius:0.#}m\n超距：{(clampOutOfRange ? "限制到边界施放" : "标红禁止施放")}";
        public bool TryConfirm()
        {
            if (!IsAiming) return false;
            RefreshAim();
            if (!CanCast) return false;
            var skill = ActiveSkill;
            LastCastPosition = Target; CastCount++;
            feedback.Play(Target + Vector3.up * 0.07f, skill);
            selected = -1; CanCast = false; rangeView.Hide(); areaView.Hide();
            hud.ShowStatus("已施放 · 仅视觉反馈", "按 Q / W / E 再次瞄准");
            return true;
        }
        public void CancelAim()
        {
            selected = -1; hasPoint = false; CanCast = false;
            rangeView.Hide(); areaView.Hide();
            hud.ShowStatus("按 Q / W / E 开始瞄准", "左键确认 · 右键 / Esc 取消");
        }
    }
}
