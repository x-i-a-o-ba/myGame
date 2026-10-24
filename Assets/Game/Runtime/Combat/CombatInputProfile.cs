using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    /// <summary>键位只映射输入槽，不包含技能行为。配置在开局读取。</summary>
    [CreateAssetMenu(menuName="Roguelike/操作/战斗键位")]
    public sealed class CombatInputProfile : ScriptableObject
    {
        public KeyCode forward=KeyCode.W,backward=KeyCode.S,left=KeyCode.A,right=KeyCode.D;
        public KeyCode[] abilities={KeyCode.Q,KeyCode.E,KeyCode.R,KeyCode.F};
        public KeyCode statistics=KeyCode.Tab,pause=KeyCode.Escape;
        public void Validate()
        {
            if(abilities==null || abilities.Length!=4) throw new InvalidOperationException("战斗键位必须有四个技能槽。");
            var keys=new System.Collections.Generic.HashSet<KeyCode>();
            foreach(var key in new[]{forward,backward,left,right,statistics,pause})
                if(key==KeyCode.None || !keys.Add(key)) throw new InvalidOperationException("移动、属性与暂停键不可为空或重复。");
            foreach(var key in abilities)
                if(key==KeyCode.None || !keys.Add(key) || key==KeyCode.Alpha1 || key==KeyCode.Alpha2 || key==KeyCode.LeftControl || key==KeyCode.RightControl)
                    throw new InvalidOperationException("技能键不可重复，也不可占用 Ctrl 或形态选择键 1/2。");
        }
    }
    /// <summary>英雄的世界平面输入。暂停在模拟入口拦截，所有战斗计时一起冻结。</summary>
    public struct HeroSteering : IComponentData
    {
        public float2 Direction;
        public bool Paused;
    }
}
