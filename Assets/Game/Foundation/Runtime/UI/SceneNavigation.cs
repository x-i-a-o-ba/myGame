using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Foundation.UI
{
    /// <summary>主菜单的场景导航适配器，面板只发 main.start/main.quit，不自行切换场景。</summary>
    public sealed class SceneNavigation : MonoBehaviour
    {
        private UIManager UI => GetComponent<UIManager>();
        private void OnEnable() => UI.Events.Command += Receive;
        private void OnDisable() { if (UI) UI.Events.Command -= Receive; }
        private void Receive(UiCommand command)
        {
            if (command.Id == "main.start") SceneManager.LoadScene(ResourcePaths.Resolve("Scene.SelectScene", "scene"));
            else if (command.Id == "main.quit") Application.Quit();
        }
    }
}
