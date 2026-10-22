using System;
using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.Foundation.UI
{
    /// <summary>UI 命令只有意图、数值与可选的指针屏幕坐标，不携带面板、控制器或 ECS 引用。</summary>
    public readonly struct UiCommand
    {
        public readonly string Id;
        public readonly int Value;
        /// <summary>指针命令（例如拖动松开）附带的屏幕坐标（像素）；其他命令为零。</summary>
        public readonly Vector2 Pointer;
        public UiCommand(string id, int value = 0) { Id = id; Value = value; Pointer = default; }
        public UiCommand(string id, int value, Vector2 pointer) { Id = id; Value = value; Pointer = pointer; }
    }

    /// <summary>展示消息。Key 在面板本地绑定；纹理所有权仍属于提供者。</summary>
    public struct UiValue
    {
        public string Key, Text;
        public float Number;
        public Texture Texture;
        public UiValueKind Kind;
    }
    public enum UiValueKind { Text, Fill, Texture, Visible }

    /// <summary>场景作用域事件通道，缓存最近展示状态供重新显示的面板回放。无静态订阅。</summary>
    public sealed class UiEvents
    {
        private readonly Dictionary<string, UiValue> latest = new Dictionary<string, UiValue>();
        public event Action<UiCommand> Command;
        public event Action<UiValue> Changed;
        public void Send(UiCommand command) => Command?.Invoke(command);
        public void Publish(UiValue value)
        {
            latest[value.Key] = value;
            Changed?.Invoke(value);
        }
        public void Replay(Action<UiValue> receiver)
        {
            // 回放不会改变订阅；面板只更新自己的控件。
            foreach (var value in latest.Values) receiver(value);
        }
        public void Clear() { latest.Clear(); Command = null; Changed = null; }
    }
}
