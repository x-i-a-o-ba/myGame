using System;
using System.Collections.Generic;
using System.Globalization;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Foundation;
using Roguelike.Foundation.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.Flow
{
    /// <summary>
    /// 开局选择流程：第 1 步选英雄、第 2 步选战场，确认后写入 GameSelection 并加载所选战场的场景。
    /// 只向选择面板发布展示键、接收面板命令；英雄与战场来自开局选择目录，战斗状态归战场场景的会话。
    /// 返回主菜单的 select.menu 由同一 Canvas 上的 SceneNavigation 路由。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SelectSceneController : MonoBehaviour
    {
        /// <summary>选择面板的卡位数量：英雄卡、战场卡、每张战场卡的怪物格与详情中的技能行。</summary>
        public const int HeroSlots=4, MapSlots=3, EnemySlots=8, SkillSlots=4;
        public const string HeroCommand="select.hero", MapCommand="select.map", NextCommand="select.next", BackCommand="select.back",
            StartCommand="select.start", MenuCommand="select.menu";
        /// <summary>面板文本框能容纳的说明长度（字符）；更长的说明由 Brief 截断，完整说明仍在配置资源中。</summary>
        public const int HeroTextMax=110, AttackTextMax=76, SkillTextMax=74, MapTextMax=140;
        public static readonly string[] StatKeys={"health","mana","attack","interval","range","move"};
        private static readonly string[] SlotKeys={"Q","E","R","F"};

        [Tooltip("场景根 Canvas 上的 UI 管理器：选择面板的展示键与按钮命令都经过它。")] public UIManager ui;
        [Tooltip("开局选择目录：可选英雄与战场，顺序即界面顺序。")] public StartSelectionCatalog catalog;
        [Tooltip("英雄展示台：第 1 步在界面中间显示所选英雄的模型；为空时只显示界面。")] public HeroShowcase showcase;

        public int HeroIndex {get; private set;}
        public int MapIndex {get; private set;}
        /// <summary>是否处于第 2 步（选择战场）。</summary>
        public bool ChoosingMap {get; private set;}
        /// <summary>已确认开始并正在加载战场场景；之后忽略全部命令。</summary>
        public bool Starting {get; private set;}
        public bool IsReady {get; private set;}
        public HeroDefinition Hero=>catalog?catalog.HeroAt(HeroIndex):null;
        public GameMapDefinition Map=>catalog?catalog.MapAt(MapIndex):null;

        /// <summary>选择面板必须绑定的全部展示键（编辑器校验使用）。</summary>
        public static IEnumerable<string> RequiredKeys()
        {
            foreach(var key in new[] {"select.title","select.stepText","select.hint","select.summary","select.step.hero","select.step.map",
                "select.hero.name","select.hero.role","select.hero.description","select.hero.portrait",
                "select.attack.icon","select.attack.name","select.attack.text"}) yield return key;
            foreach(var stat in StatKeys) yield return "select.hero.stat."+stat;
            for(int i=0;i<HeroSlots;i++) foreach(var part in new[] {"visible","selected","name","role","portrait"}) yield return $"select.hero.{i}.{part}";
            for(int j=0;j<SkillSlots;j++) foreach(var part in new[] {"visible","icon","name","meta","text"}) yield return $"select.skill.{j}.{part}";
            for(int k=0;k<MapSlots;k++)
            {
                foreach(var part in new[] {"visible","selected","name","info","description","preview"}) yield return $"select.map.{k}.{part}";
                for(int m=0;m<EnemySlots;m++) foreach(var part in new[] {"visible","portrait","name","tag"}) yield return $"select.map.{k}.enemy.{m}.{part}";
            }
        }

        private void OnEnable() { if(ui) ui.Events.Command+=Receive; }
        private void OnDisable() { if(ui) ui.Events.Command-=Receive; }
        private void Start()
        {
            try
            {
                if(!ui) throw new InvalidOperationException("选择场景缺少 UI 管理器引用。");
                string error=catalog?catalog.Validate():"选择场景缺少开局选择目录。";
                if(error!=null) throw new InvalidOperationException(error);
                // 从战场返回时预选上一局的英雄与战场。
                HeroIndex=Mathf.Max(0,catalog.IndexOf(GameSelection.Hero));
                MapIndex=Mathf.Max(0,catalog.IndexOf(GameSelection.Map));
                IsReady=true;
                PublishHeroes(); PublishMaps(); PublishStep();
                Debug.Log($"SELECT_SCENE_READY heroes={catalog.heroes.Length}; maps={catalog.maps.Length}; hero={Hero.stableId}; map={Map.stableId}");
            }
            catch(Exception error) { Debug.LogException(error,this); enabled=false; }
        }
        private void Update()
        {
            if(!IsReady || Starting) return;
            if(Input.GetKeyDown(KeyCode.Escape)) Send(ChoosingMap?BackCommand:MenuCommand);
            else if(Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Send(ChoosingMap?StartCommand:NextCommand);
            else if(Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.UpArrow)) Cycle(-1);
            else if(Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.DownArrow)) Cycle(1);
        }
        // 键盘也走面板命令通道，与按钮行为一致（Esc 的 select.menu 由 SceneNavigation 处理）。
        private void Send(string command)=>ui.Events.Send(new UiCommand(command));
        private void Cycle(int step)
        {
            if(ChoosingMap) SelectMap((MapIndex+step+catalog.maps.Length)%catalog.maps.Length);
            else SelectHero((HeroIndex+step+catalog.heroes.Length)%catalog.heroes.Length);
        }
        private void Receive(UiCommand command)
        {
            if(!IsReady || Starting) return;
            switch(command.Id)
            {
                case HeroCommand: SelectHero(command.Value); break;
                case MapCommand: SelectMap(command.Value); break;
                case NextCommand: SetStep(true); break;
                case BackCommand: SetStep(false); break;
                case StartCommand: if(ChoosingMap) Begin(); else SetStep(true); break;
            }
        }

        /// <summary>选中第 index 个英雄；下标无效时返回 false 且不改变选择。</summary>
        public bool SelectHero(int index)
        {
            if(!IsReady || Starting || !catalog.HeroAt(index)) return false;
            if(index!=HeroIndex) { HeroIndex=index; PublishHeroes(); }
            return true;
        }
        /// <summary>选中第 index 个战场；下标无效时返回 false 且不改变选择。</summary>
        public bool SelectMap(int index)
        {
            if(!IsReady || Starting || !catalog.MapAt(index)) return false;
            if(index!=MapIndex) { MapIndex=index; PublishMaps(); }
            return true;
        }
        /// <summary>切换步骤：true 为第 2 步（选择战场），false 为第 1 步（选择英雄）。</summary>
        public void SetStep(bool chooseMap)
        {
            if(!IsReady || Starting || ChoosingMap==chooseMap) return;
            ChoosingMap=chooseMap; PublishStep();
        }
        /// <summary>确认选择：写入 GameSelection 并加载所选战场的场景。</summary>
        public void Begin()
        {
            if(!IsReady || Starting) return;
            var hero=Hero; var map=Map;
            string scene;
            try { scene=ResourcePaths.Resolve(map.sceneId,"scene"); }
            catch(InvalidOperationException error) { Debug.LogException(error,this); return; }
            var second=Array.Find(catalog.maps,value=>value && value!=map);
            RunState.Begin(hero,map,second);
            GameSelection.Set(hero,map); Starting=true;
            ui.Text("select.summary",$"正在进入 {map.Label}……");
            Debug.Log($"GAME_SELECTION hero={hero.stableId}; map={map.stableId}; scene={scene}");
            SceneManager.LoadScene(scene);
        }

        private void PublishStep()
        {
            ui.Visible("select.step.hero",!ChoosingMap);
            ui.Visible("select.step.map",ChoosingMap);
            ui.Text("select.title",ChoosingMap?"选择战场":"选择英雄");
            ui.Text("select.stepText",ChoosingMap?"第 2 步 / 共 2 步  ·  每个战场有不同的怪物":"第 1 步 / 共 2 步  ·  选择出战英雄");
            ui.Text("select.hint",ChoosingMap?"Esc 上一步  ·  ← → 切换战场  ·  Enter 开始战斗":"Esc 返回主菜单  ·  ← → 切换英雄  ·  Enter 下一步");
            if(showcase) showcase.SetVisible(!ChoosingMap);
            PublishSummary();
        }
        private void PublishSummary()
        {
            var hero=Hero; var map=Map;
            ui.Text("select.summary",ChoosingMap?$"出战英雄：{Name(hero)}    ·    战场：{map.Label}":$"已选英雄：{Name(hero)}  ·  {hero.role}");
        }
        private void PublishHeroes()
        {
            for(int i=0;i<HeroSlots;i++)
            {
                var hero=catalog.HeroAt(i); string p="select.hero."+i+".";
                ui.Visible(p+"visible",hero);
                ui.Visible(p+"selected",hero && i==HeroIndex);
                if(!hero) continue;
                ui.Text(p+"name",Name(hero)); ui.Text(p+"role",hero.role); ui.Texture(p+"portrait",hero.portrait);
            }
            PublishHeroDetail(Hero);
            if(showcase) showcase.Show(Hero);
            PublishSummary();
        }
        private void PublishHeroDetail(HeroDefinition hero)
        {
            ui.Text("select.hero.name",Name(hero)); ui.Text("select.hero.role",hero.role);
            ui.Text("select.hero.description",Brief(hero.description,HeroTextMax)); ui.Texture("select.hero.portrait",hero.portrait);
            var attack=hero.basicAttack;
            float interval=attack?attack.baseAttackTime:hero.attackInterval, range=attack?attack.range:hero.range;
            ui.Text("select.hero.stat.health",N(hero.health));
            ui.Text("select.hero.stat.mana",N(hero.baseMana));
            ui.Text("select.hero.stat.attack",N(hero.attackDamage));
            ui.Text("select.hero.stat.interval",N(interval)+" 秒");
            ui.Text("select.hero.stat.range",N(range)+" 米");
            ui.Text("select.hero.stat.move",N(hero.speed)+" 米/秒");
            bool melee=attack && attack.delivery==AttackDelivery.Melee;
            ui.Texture("select.attack.icon",attack?attack.icon:null);
            ui.Text("select.attack.name",(attack?attack.Label:"普通攻击")+"  ·  被动 · "+(melee?"近战":"远程弹道"));
            ui.Text("select.attack.text",attack?Brief(attack.description,AttackTextMax):"自动攻击攻击距离内的敌人。");
            for(int j=0;j<SkillSlots;j++)
            {
                var skill=hero.skills!=null && j<hero.skills.Length?hero.skills[j]:null; string p="select.skill."+j+".";
                ui.Visible(p+"visible",skill);
                if(!skill) continue;
                ui.Texture(p+"icon",skill.icon);
                ui.Text(p+"name",(string.IsNullOrWhiteSpace(skill.displayName)?skill.name:skill.displayName));
                ui.Text(p+"meta",SkillMeta(skill,j));
                ui.Text(p+"text",Brief(skill.description,SkillTextMax));
            }
        }
        private void PublishMaps()
        {
            for(int k=0;k<MapSlots;k++)
            {
                var map=catalog.MapAt(k); string p="select.map."+k+".";
                ui.Visible(p+"visible",map);
                ui.Visible(p+"selected",map && k==MapIndex);
                if(!map) continue;
                var kinds=Kinds(map.enemyRoster);
                ui.Text(p+"name",map.Label); ui.Text(p+"description",Brief(map.description,MapTextMax)); ui.Texture(p+"preview",map.preview);
                ui.Text(p+"info",MapInfo(map,kinds));
                for(int m=0;m<EnemySlots;m++)
                {
                    string e=p+"enemy."+m+".";
                    bool shown=m<kinds.Count;
                    ui.Visible(e+"visible",shown);
                    if(!shown) continue;
                    var (archetype,entry)=kinds[m];
                    ui.Texture(e+"portrait",archetype.portrait?archetype.portrait:archetype.attack?archetype.attack.icon:null);
                    ui.Text(e+"name",archetype.Label); ui.Text(e+"tag",Tag(entry));
                }
            }
            PublishSummary();
        }

        /// <summary>名册中出现的敌人种类（同一预制体只取第一条出场规则），顺序与名册一致。</summary>
        public static List<(EnemyArchetype archetype,EnemySpawnEntry entry)> Kinds(EnemyRoster roster)
        {
            var result=new List<(EnemyArchetype,EnemySpawnEntry)>();
            if(!roster || roster.entries==null) return result;
            foreach(var entry in roster.entries)
                if(entry!=null && entry.prefab && entry.weight>0 && !result.Exists(x=>x.Item1==entry.prefab)) result.Add((entry.prefab,entry));
            return result;
        }
        private static string MapInfo(GameMapDefinition map,List<(EnemyArchetype archetype,EnemySpawnEntry entry)> kinds)
        {
            int bosses=0; foreach(var kind in kinds) if((kind.entry.tiers&EnemyTierMask.Boss)!=0) bosses++;
            return "持续补怪 · 3:30 首领"+$"  ·  {kinds.Count} 种怪物  ·  {bosses} 种首领";
        }
        private static string Tag(EnemySpawnEntry entry)
        {
            bool normal=(entry.tiers&EnemyTierMask.Normal)!=0, elite=(entry.tiers&EnemyTierMask.Elite)!=0, boss=(entry.tiers&EnemyTierMask.Boss)!=0;
            if(!normal) return elite && boss?"精英 · 首领":boss?"首领":"精英";
            return entry.firstSecond>0?$"强度 {entry.firstSecond/60:0.##} 分钟起":"开局出现";
        }
        private static string SkillMeta(SkillDefinition skill,int slot)
        {
            var parts=new List<string> {(slot<SlotKeys.Length?SlotKeys[slot]+"  ·  ":"")+(skill.growth!=null && skill.growth.track==SkillLevelTrack.Ultimate?"终极技能":"普通技能")};
            parts.Add(AbilityTooltipBuilder.AimLabel(skill.aim));
            if(skill.requiredHits>0) parts.Add($"普攻叠 {skill.requiredHits} 层");
            if(skill.maxCharges>1) parts.Add($"{skill.maxCharges} 层充能 · 每层 {N(skill.rechargeSeconds>0?skill.rechargeSeconds:skill.cooldown)} 秒");
            else if(skill.cooldown>0) parts.Add($"冷却 {N(skill.cooldown)} 秒");
            if(skill.manaCost>0) parts.Add($"法力 {N(skill.manaCost)}");
            return string.Join("  ·  ",parts);
        }
        private static string Name(HeroDefinition hero)=>!hero?"":string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName;
        private static string Trim(string text)=>string.IsNullOrWhiteSpace(text)?"":text.Trim();
        private static readonly char[] SentenceEnds={'。','；','！','？'};
        /// <summary>把说明压缩到 max 个字符以内：优先在 max 之内最后一个句末标点（。；！？）处截断，否则截断并加“…”。</summary>
        public static string Brief(string text,int max)
        {
            text=Trim(text);
            if(max<=1 || text.Length<=max) return text;
            int end=text.LastIndexOfAny(SentenceEnds,max-1);
            if(end>=max/3) return text.Substring(0,end+1);
            return text.Substring(0,max-1).TrimEnd()+"…";
        }
        private static string N(float value)=>value.ToString("0.##",CultureInfo.InvariantCulture);
    }
}
