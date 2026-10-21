using System;
using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>玩家配方交换格式。导入先在临时定义上完整编译，成功后才交给调用方提交。</summary>
    public static class SkillRecipeJson
    {
        public const int MaximumJsonCharacters=262144;

        public static string Export(SkillDefinition skill)
        {
            SkillRecipeCompiler.Validate(skill);
            var doc=new SkillRecipeDocument {name=skill.displayName,aim=skill.aim,castRange=skill.castRange,
                cooldown=skill.cooldown,castTime=skill.castTime,recoveryTime=skill.recoveryTime,manaCost=skill.manaCost,
                maxCharges=skill.maxCharges,requiredHits=skill.requiredHits,rechargeSeconds=skill.rechargeSeconds,
                recipe=skill.recipe};
            return JsonUtility.ToJson(doc,true);
        }

        /// <summary>返回尚未保存的临时技能，调用方负责销毁或生成资产。默认执行玩家授权检查。</summary>
        public static SkillDefinition Import(string json,SkillModuleCatalog catalog,bool forPlayer=true)
        {
            if(string.IsNullOrWhiteSpace(json) || json.Length>MaximumJsonCharacters)
                throw new InvalidOperationException("配方文件为空或超过 256 Ki 字符。");
            SkillRecipeDocument document;
            try { document=JsonUtility.FromJson<SkillRecipeDocument>(json); }
            catch(ArgumentException error) { throw new InvalidOperationException("配方 JSON 格式无效，请使用技能设计器导出的文件。",error); }
            if(document==null || document.schemaVersion!=1 || document.recipe==null || !document.recipe.enabled)
                throw new InvalidOperationException("配方文件格式版本不受支持或未启用。");
            if(string.IsNullOrWhiteSpace(document.name)||document.name.Length>80)
                throw new InvalidOperationException("配方名称必须为 1–80 字。");
            var skill=ScriptableObject.CreateInstance<SkillDefinition>();
            try
            {
                skill.name=document.name; skill.displayName=document.name;
                // 官方编号由编辑器另行分配；玩家配方使用命名空间 + recipeId，不占官方号段。
                skill.stableId="recipe."+Guid.NewGuid().ToString("N"); skill.configId=0;
                skill.modules=catalog; skill.recipe=document.recipe; skill.aim=document.aim;
                skill.castRange=document.castRange; skill.cooldown=document.cooldown; skill.castTime=document.castTime;
                skill.recoveryTime=document.recoveryTime; skill.manaCost=document.manaCost;
                skill.maxCharges=document.maxCharges; skill.requiredHits=document.requiredHits; skill.rechargeSeconds=document.rechargeSeconds;
                SkillRecipeCompiler.Validate(skill,forPlayer);
                return skill;
            }
            catch
            {
                if(Application.isPlaying) UnityEngine.Object.Destroy(skill); else UnityEngine.Object.DestroyImmediate(skill);
                throw;
            }
        }
    }
}
