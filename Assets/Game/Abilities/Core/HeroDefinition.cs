using System;
using UnityEngine;
namespace Roguelike.Abilities
{
    /// <summary>英雄模板通过技能引用组装 loadout；输入按槽位绑定，技能定义不识别 Q/W/E/R。</summary>
    [CreateAssetMenu(menuName="Roguelike/技能/英雄定义")]
    public sealed class HeroDefinition : ScriptableObject
    {
        [Tooltip("数字配置 ID：英雄使用 2000–2999；由编辑器编号登记分配，0 仅表示尚未分配的临时定义。")]
        public int configId;
        [InspectorName("稳定 ID")] public string stableId;
        [InspectorName("英雄名称")] public string displayName;
        [InspectorName("技能槽（按顺序）")] public SkillDefinition[] skills=Array.Empty<SkillDefinition>();
        [InspectorName("基础生命"), Min(1)] public float health=300;
        [InspectorName("移动速度（米/秒）"), Min(0)] public float speed=8;
        [InspectorName("普攻伤害"), Min(0)] public float attackDamage=18;
        [InspectorName("普攻间隔（秒）"), Min(0.01f)] public float attackInterval=0.65f;
        [InspectorName("普攻距离（米）"), Min(0)] public float range=16;

        /// <summary>验证英雄与给定库的引用。返回槽位到库索引的映射，不修改共享资源。</summary>
        public int[] ResolveSkills(SkillCatalog catalog)
        {
            if(configId!=0 && (configId<2000 || configId>2999))
                throw new InvalidOperationException("英雄数字 ID 必须为 2000–2999："+name);
            if(string.IsNullOrWhiteSpace(stableId) || stableId.Trim()!=stableId)
                throw new InvalidOperationException("英雄稳定 ID 不能为空或带首尾空格："+name);
            if(!catalog || catalog.skills==null || skills==null || skills.Length==0 || skills.Length>32)
                throw new InvalidOperationException("英雄技能槽或技能库无效："+name);
            if(!Finite(health) || health<=0 || !Finite(speed) || speed<0 || !Finite(attackDamage) || attackDamage<0 ||
                !Finite(attackInterval) || attackInterval<=0 || !Finite(range) || range<0)
                throw new InvalidOperationException("英雄基础属性必须有限且在合法范围："+name);
            var result=new int[skills.Length];
            for(int i=0;i<skills.Length;i++)
            {
                if(!skills[i] || (result[i]=Array.IndexOf(catalog.skills,skills[i]))<0)
                    throw new InvalidOperationException("英雄技能未登记在库中："+name+" / 槽 "+i);
            }
            return result;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
