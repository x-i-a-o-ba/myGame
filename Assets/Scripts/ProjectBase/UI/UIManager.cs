using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using ManagedUI = Roguelike.Foundation.UI.UIManager;
using Roguelike.Foundation.UI;

public enum E_UI_Layer { Bot, Mid, Top, System }
/// <summary>旧 API 兼容适配，所有面板仍归场景 Canvas 的 UIManager；不再创建常驻 Canvas/EventSystem。</summary>
[System.Obsolete("新代码使用 Roguelike.Foundation.UI.UIManager 与面板事件。")]
public class UIManager : BaseManager<UIManager>
{
    private ManagedUI Current => Object.FindFirstObjectByType<ManagedUI>();
    public RectTransform canvas => Current ? (RectTransform)Current.transform : null;
    public Transform GetLayerFather(E_UI_Layer layer) => Current ? Current.popupLayer : null;
    public void ShowPanel<T>(string panelName,E_UI_Layer layer=E_UI_Layer.Mid,UnityAction<T> callBack=null) where T:BasePanel
    {
        var manager=Current;
        if(!manager) { Debug.LogError("当前场景没有 UIManager"); return; }
        void Ready(UIPanel panel)
        {
            if(panel.panelId!=panelName) return;
            manager.PanelOpened-=Ready;
            var legacy=panel.GetComponent<T>();
            if(legacy) { legacy.ShowMe(); callBack?.Invoke(legacy); }
            else Debug.LogError("旧接口需要 BasePanel；新面板请通过事件操作："+panelName);
        }
        if(callBack!=null) manager.PanelOpened+=Ready;
        manager.Show(panelName);
    }
    public void HidePanel(string panelName) { if(Current) Current.Hide(panelName); }
    public T GetPanel<T>(string name) where T:BasePanel => Current && Current.FindPanel(name) ? Current.FindPanel(name).GetComponent<T>() : null;
    public static void AddCustomEventListener(UIBehaviour control,EventTriggerType type,UnityAction<BaseEventData> callback)
    {
        var trigger=control.GetComponent<EventTrigger>(); if(!trigger) trigger=control.gameObject.AddComponent<EventTrigger>();
        var entry=new EventTrigger.Entry {eventID=type};entry.callback.AddListener(callback);trigger.triggers.Add(entry);
    }
}
