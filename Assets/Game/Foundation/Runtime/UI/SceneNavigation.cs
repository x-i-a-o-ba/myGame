using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Foundation.UI
{
    /// <summary>导航路由的动作；每个值都有执行语义。</summary>
    public enum SceneRouteAction
    {
        [InspectorName("加载场景")] LoadScene,
        [InspectorName("退出游戏")] Quit,
    }

    /// <summary>
    /// 场景 Canvas 上的导航适配器：按路由表把 UI 命令映射为“加载场景”或“退出游戏”。面板只发命令，不自行切换场景。
    /// 确认时间大于 0 的路由首次点击只发布显隐键 “命令.confirm”，在确认时间内再次点击才执行，超时自动取消（使用不受暂停影响的真实时间）。
    /// </summary>
    [RequireComponent(typeof(UIManager))]
    public sealed class SceneNavigation : MonoBehaviour
    {
        [Serializable]
        public struct Route
        {
            [Tooltip("触发本路由的 UI 命令 ID，例如 main.start。")] public string command;
            [Tooltip("命令触发后执行的动作。")] public SceneRouteAction action;
            [Tooltip("动作为“加载场景”时使用的资源清单场景 ID（Scene.*）。")] public string sceneId;
            [Tooltip("大于 0 时需要在该时间（秒，真实时间）内再次点击才执行；首次点击显示 “命令.confirm” 绑定的提示。0 表示立即执行。")]
            [Min(0)] public float confirmSeconds;
        }

        [Tooltip("命令到导航动作的映射；同一命令只能出现一次。")]
        public Route[] routes =
        {
            new Route { command = "main.start", action = SceneRouteAction.LoadScene, sceneId = "Scene.SelectScene" },
            new Route { command = "main.quit", action = SceneRouteAction.Quit },
        };

        private string pending;
        private float pendingUntil;
        private bool leaving;

        /// <summary>正在等待二次确认的命令；没有时为 null。</summary>
        public string PendingCommand => pending;
        /// <summary>需要确认的路由在首次点击后发布的显隐键。</summary>
        public static string ConfirmKey(string command) => command + ".confirm";

        private UIManager UI => GetComponent<UIManager>();
        private void OnEnable() => UI.Events.Command += Receive;
        private void OnDisable()
        {
            if (UI) UI.Events.Command -= Receive;
            Cancel();
        }
        private void Update()
        {
            if (pending != null && Time.unscaledTime >= pendingUntil) Cancel();
        }

        /// <summary>返回命令对应的路由下标；没有时为 -1。</summary>
        public int Find(string command)
        {
            if (routes == null || string.IsNullOrEmpty(command)) return -1;
            for (int i = 0; i < routes.Length; i++) if (routes[i].command == command) return i;
            return -1;
        }

        private void Receive(UiCommand command)
        {
            if (leaving) return;
            int index = Find(command.Id);
            if (index < 0) return;
            var route = routes[index];
            if (route.confirmSeconds > 0 && !(pending == route.command && Time.unscaledTime < pendingUntil))
            {
                Cancel();
                pending = route.command;
                pendingUntil = Time.unscaledTime + route.confirmSeconds;
                UI.Visible(ConfirmKey(route.command), true);
                return;
            }
            Cancel();
            Execute(route);
        }

        private void Execute(Route route)
        {
            if (route.action == SceneRouteAction.Quit)
            {
                Debug.Log("SCENE_NAVIGATION: " + route.command + " -> quit", this);
                Application.Quit();
                return;
            }
            string scene = ResourcePaths.Resolve(route.sceneId, "scene");
            leaving = true;
            Debug.Log("SCENE_NAVIGATION: " + route.command + " -> " + scene, this);
            SceneManager.LoadScene(scene);
        }

        /// <summary>取消等待中的确认并隐藏提示。</summary>
        public void Cancel()
        {
            if (pending == null) return;
            string command = pending;
            pending = null;
            var ui = UI;
            if (ui) ui.Visible(ConfirmKey(command), false);
        }
    }
}
