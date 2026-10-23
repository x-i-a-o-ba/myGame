using System;
using System.Collections.Generic;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEngine;

namespace Roguelike.Flow
{
    /// <summary>
    /// 开局选择目录：选择界面可选的英雄与战场，数组顺序即界面顺序，第一项为默认选中。
    /// 只存配置；本局的选择记录在 GameSelection，战斗状态归战场场景的会话。
    /// </summary>
    [CreateAssetMenu(menuName="Roguelike/流程/开局选择目录")]
    public sealed class StartSelectionCatalog : ScriptableObject
    {
        [Tooltip("可选英雄，按选择界面英雄卡顺序排列；最多 4 个。英雄需填写头像、定位标签与简介。")]
        public HeroDefinition[] heroes=Array.Empty<HeroDefinition>();
        [Tooltip("可选战场，按选择界面战场卡顺序排列；最多 3 个。每个战场引用自己的场景与敌人名册。")]
        public GameMapDefinition[] maps=Array.Empty<GameMapDefinition>();

        public HeroDefinition HeroAt(int index)=>heroes!=null && index>=0 && index<heroes.Length?heroes[index]:null;
        public GameMapDefinition MapAt(int index)=>maps!=null && index>=0 && index<maps.Length?maps[index]:null;
        public int IndexOf(HeroDefinition hero)=>hero && heroes!=null?Array.IndexOf(heroes,hero):-1;
        public int IndexOf(GameMapDefinition map)=>map && maps!=null?Array.IndexOf(maps,map):-1;

        /// <summary>校验英雄与战场列表；返回第一个错误，没有错误时返回 null。不修改资源。</summary>
        public string Validate()
        {
            if(heroes==null || heroes.Length==0) return "开局选择目录没有任何英雄。";
            if(heroes.Length>SelectSceneController.HeroSlots) return $"开局选择目录最多 {SelectSceneController.HeroSlots} 个英雄（选择界面的英雄卡数量）。";
            var heroIds=new HashSet<int>();
            for(int i=0;i<heroes.Length;i++)
            {
                var hero=heroes[i];
                if(!hero) return $"第 {i+1} 个英雄为空。";
                if(Array.IndexOf(heroes,hero)!=i || !heroIds.Add(hero.configId)) return "英雄重复："+hero.name;
                if(hero.configId<2000 || hero.configId>2999) return "英雄数字 ID 必须为 2000–2999："+hero.name;
                if(string.IsNullOrWhiteSpace(hero.displayName) || string.IsNullOrWhiteSpace(hero.role) || string.IsNullOrWhiteSpace(hero.description))
                    return "英雄缺少名称、定位标签或简介："+hero.name;
                if(!hero.portrait) return "英雄缺少头像："+hero.name;
                if(hero.skills==null || hero.skills.Length==0 || hero.skills.Length>SelectSceneController.SkillSlots) return "英雄技能槽必须为 1–4 个："+hero.name;
            }
            if(maps==null || maps.Length==0) return "开局选择目录没有任何战场。";
            if(maps.Length>SelectSceneController.MapSlots) return $"开局选择目录最多 {SelectSceneController.MapSlots} 个战场（选择界面的战场卡数量）。";
            var mapIds=new HashSet<int>();
            for(int i=0;i<maps.Length;i++)
            {
                var map=maps[i];
                if(!map) return $"第 {i+1} 个战场为空。";
                if(Array.IndexOf(maps,map)!=i || !mapIds.Add(map.configId)) return "战场重复："+map.name;
                string error=map.Validate();
                if(error!=null) return error;
                if(!map.difficulty) return "战场缺少关卡难度曲线："+map.name;
                if(!map.preview) return "战场缺少预览图："+map.name;
            }
            return null;
        }
    }
}
