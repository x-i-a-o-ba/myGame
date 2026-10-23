using Roguelike.Abilities;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>
    /// 选择界面的英雄展示台：显示所选英雄的展示预制体（没有时显示场景里的默认外形）并缓慢旋转。
    /// 与战场的玩家节点使用同一尺寸约定（展示预制体实例化到模型节点下的原点）；只负责外形，不含战斗数据。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroShowcase : MonoBehaviour
    {
        [Tooltip("旋转台：展示模型随它绕竖直轴缓慢旋转；第 2 步（选择战场）时隐藏。")] public Transform turntable;
        [Tooltip("模型节点：与战场 Player 节点同尺寸。它下面的渲染器是默认外形；英雄有展示预制体时隐藏默认外形并把预制体实例化到这里。")] public Transform model;
        [Tooltip("旋转速度（度/秒）；0 表示不旋转。")] public float spinSpeed=18;
        private GameObject visual;
        private Renderer[] defaults;
        private bool hasShown;

        /// <summary>当前展示的英雄；null 表示尚未展示。</summary>
        public HeroDefinition Shown {get; private set;}
        /// <summary>当前实例化的展示预制体；英雄没有展示预制体时为 null。</summary>
        public GameObject Visual=>visual;
        public bool HasBindings=>turntable && model;

        public void Show(HeroDefinition hero)
        {
            if(!model || (hasShown && hero==Shown)) return;
            hasShown=true; Shown=hero;
            if(visual) Destroy(visual);
            visual=null;
            defaults??=model.GetComponentsInChildren<Renderer>(true);
            bool custom=hero && hero.visualPrefab;
            foreach(var renderer in defaults) if(renderer) renderer.enabled=hero && !custom;
            if(!custom) return;
            visual=Instantiate(hero.visualPrefab,model);
            visual.transform.localPosition=Vector3.zero; visual.transform.localRotation=Quaternion.identity;
        }
        public void SetVisible(bool visible) { if(turntable && turntable.gameObject.activeSelf!=visible) turntable.gameObject.SetActive(visible); }
        private void Update() { if(turntable && spinSpeed!=0) turntable.Rotate(0,spinSpeed*Time.deltaTime,0,Space.Self); }
        private void OnDestroy() { if(visual) Destroy(visual); }
    }
}
