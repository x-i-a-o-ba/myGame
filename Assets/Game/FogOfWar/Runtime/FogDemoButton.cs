using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.FogOfWar
{
    /// <summary>Inspector 中指定按钮意图，避免 UI 直接依赖视野会话。</summary>
    public sealed class FogDemoButton:MonoBehaviour
    {
        public FogDemoHud hud;
        public FogCommand command;
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(Click);
        private void OnDisable() => GetComponent<Button>().onClick.RemoveListener(Click);
        private void Click() => hud.Send(command);
    }
}
