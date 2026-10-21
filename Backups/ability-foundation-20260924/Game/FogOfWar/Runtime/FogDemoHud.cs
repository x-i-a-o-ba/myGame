using System;
using UnityEngine;
using UnityEngine.UI;
using Roguelike.Foundation.UI;
namespace Roguelike.FogOfWar
{
    public enum FogCommand { Team,Night,Cycle,Flying,EnemySource,TrueSight,Invisible,Trees,ObserverWard,SentryWard,RemoveWards,Pulse,Exploration,Reset,Overview,Omniscient }
    /// <summary>视野 UI 适配器发布安全快照；小地图和选中面板分别订阅自己的数据键。</summary>
    public sealed class FogDemoHud : MonoBehaviour
    {
        [HideInInspector] public Text status,selection;
        [HideInInspector] public RawImage minimap;
        [HideInInspector] public RectTransform[] blockingPanels;
        private string statusValue;
        private Roguelike.Foundation.UI.UIManager UI => GetComponent<Roguelike.Foundation.UI.UIManager>();
        public event Action<FogCommand> Command;
        private void OnEnable() { if(UI) UI.Events.Command += Receive; }
        private void OnDisable() { if(UI) UI.Events.Command -= Receive; }
        private void Receive(UiCommand value) { if(value.Id=="fog.command") Command?.Invoke((FogCommand)value.Value); }
        public void Send(FogCommand value) => UI.Events.Send(new UiCommand("fog.command",(int)value));
        public string Status { get => statusValue; set { statusValue=value; UI.Text("fog.status",value); } }
        public string Selection { set => UI.Text("fog.selection",value); }
        public Texture MinimapTexture { set => UI.Texture("fog.minimap",value); }
        public bool Blocks(Vector2 point) => UI.Blocks(point);
    }
}
