using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.Abilities
{
    /// <summary>场景中的按钮绑定，可在 Inspector 调整命令。</summary>
    [RequireComponent(typeof(Button))]
    public sealed class SkillDemoButton : MonoBehaviour
    {
        public SkillDemoHud hud; public SkillDemoCommand command;
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Click);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Click);
        private void Click() => hud.Send(command);
    }
}
