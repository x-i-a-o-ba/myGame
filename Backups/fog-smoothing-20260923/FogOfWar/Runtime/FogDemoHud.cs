using System;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.FogOfWar
{
    public enum FogCommand { Team,Night,Cycle,Flying,EnemySource,TrueSight,Invisible,Trees,ObserverWard,SentryWard,RemoveWards,Pulse,Exploration,Reset,Overview,Omniscient }
    /// <summary>场景调试面板只发送命令，显示会话提供的安全快照。</summary>
    public sealed class FogDemoHud:MonoBehaviour
    {
        public Text status,selection;
        public RawImage minimap;
        public RectTransform[] blockingPanels;
        public event Action<FogCommand> Command;
        public void Send(FogCommand command) => Command?.Invoke(command);
        public bool Blocks(Vector2 point)
        {
            foreach(var panel in blockingPanels)
                if(panel && RectTransformUtility.RectangleContainsScreenPoint(panel,point)) return true;
            return false;
        }
    }
}
