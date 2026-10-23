using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Roguelike.Combat;
using Roguelike.Flow;
using Roguelike.Foundation.Editor;
using Roguelike.Foundation.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Roguelike.Editor
{
    /// <summary>
    /// 开局选择流程的编辑器校验：开局选择目录与各战场、战场之间的怪物差异、编号登记、主菜单 / 选择场景 / 各战场场景的引用、
    /// 选择面板绑定与按钮、场景导航路由，以及相关中文 Inspector 是否生效。
    /// 只读取资源并依次打开场景检查，不修改也不保存；GameTools.Build 构建前调用，也可用 -executeMethod Roguelike.Editor.GameFlowValidation.Validate 单独运行。
    /// </summary>
    public static class GameFlowValidation
    {
        public const string CatalogPath="Assets/GameData/目录/开局选择目录.asset";
        public const string MainScenePath="Assets/Scenes/Main.unity", SelectScenePath="Assets/Scenes/SelectScene.unity";
        public const string SelectPanelId="UI.SelectScene.SelectPanel", ControlsPanelId="UI.Game.Controls", DeathPanelId="UI.Game.DeathOverlay";
        public const string LeaveCommand="game.leave", ReselectCommand="game.select";
        /// <summary>熔火裂谷（本次新增战场）的内部兼容键：要求它的名册至少 8 种怪物并覆盖全部敌人行为。</summary>
        public const string EmberRiftMap="map.ember_rift";
        private const string LedgerPath="Docs/游戏配置/编号登记.json";
        private static int checks;

        [MenuItem("Tools/Roguelike/维护/检查开局选择流程")]
        public static void Validate()
        {
            if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("已取消开局选择流程校验：校验需要依次打开场景，请先保存或放弃当前场景的修改。");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            checks=0;
            try
            {
                var catalog=ValidateCatalog();
                ValidateLedger(catalog);
                ValidateMain();
                ValidateSelect(catalog);
                foreach(var map in catalog.maps) ValidateBattlefield(catalog,map);
            }
            finally { if(!Application.isBatchMode && setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            Debug.Log("GAME_FLOW_VALIDATION_PASSED checks="+checks);
        }

        private static StartSelectionCatalog ValidateCatalog()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<StartSelectionCatalog>(CatalogPath);
            Check(catalog,"缺少开局选择目录："+CatalogPath);
            string error=catalog.Validate();
            Check(error==null,"开局选择目录无效："+error);
            Check(catalog.heroes.Length>=2 && catalog.maps.Length>=2,"开局选择目录至少需要两位英雄和两个战场（--select-smoke 会切换到第二位英雄与第二个战场）。");
            Inspector(catalog,"StartSelectionCatalogChineseInspector");
            foreach(var hero in catalog.heroes) Check(hero.skills.All(skill=>skill),"英雄技能槽存在空技能："+hero.name);
            var scenes=new HashSet<string>(StringComparer.Ordinal);
            var owners=new Dictionary<int,string>();
            foreach(var map in catalog.maps)
            {
                Inspector(map,"GameMapDefinitionChineseInspector");
                Check(scenes.Add(map.sceneId),"多个战场使用同一场景："+map.sceneId);
                var kinds=SelectSceneController.Kinds(map.enemyRoster);
                Check(kinds.Count>0,"战场名册没有可出场的怪物："+map.name);
                foreach(var (archetype,_) in kinds)
                {
                    Check(archetype.portrait,"怪物缺少头像（选择界面战场卡显示）："+archetype.name);
                    Check(!owners.TryGetValue(archetype.configId,out string other),$"怪物 {archetype.Label} 同时出现在战场 {other} 与 {map.Label}：每个战场应使用不同的怪物。");
                    owners[archetype.configId]=map.Label;
                }
                if(map.stableId==EmberRiftMap)
                {
                    var behaviors=new HashSet<EnemyBehavior>(kinds.Select(kind=>kind.archetype.behavior));
                    Check(kinds.Count>=8 && behaviors.Count==Enum.GetValues(typeof(EnemyBehavior)).Length,"熔火裂谷名册需要至少 8 种怪物并覆盖近战、远程、冲锋、自爆四种行为。");
                    Check(kinds.Any(kind=>(kind.entry.tiers&EnemyTierMask.Elite)!=0) && kinds.Any(kind=>(kind.entry.tiers&EnemyTierMask.Boss)!=0),"熔火裂谷名册需要精英与首领条目。");
                }
            }
            Check(catalog.maps.Any(map=>map.stableId==EmberRiftMap),"开局选择目录缺少熔火裂谷（"+EmberRiftMap+"）。");
            return catalog;
        }

        /// <summary>目录中的战场与其名册里的怪物都必须登记在数字编号登记中，且所有者与内部兼容键一致。</summary>
        private static void ValidateLedger(StartSelectionCatalog catalog)
        {
            var ledger=JsonUtility.FromJson<CombatValidation.ItemLedger>(File.ReadAllText(LedgerPath));
            Check(ledger!=null && ledger.entries!=null,"编号登记无法读取："+LedgerPath);
            foreach(var map in catalog.maps)
            {
                Check(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(map,out string guid,out long local),"无法读取战场资源 GUID："+map.name);
                string key=guid+":"+local;
                Check(ledger.entries.Any(entry=>entry.category=="map" && entry.id==map.configId && entry.assetKey==key && entry.stableId==map.stableId && !entry.retired),
                    $"战场 {map.name} 未正确登记在 {LedgerPath}（category 为 map、编号 {map.configId}、assetKey {key}）。");
                foreach(var (archetype,_) in SelectSceneController.Kinds(map.enemyRoster))
                    Check(ledger.entries.Any(entry=>entry.category=="monster" && entry.id==archetype.configId && entry.assetKey=="monster:"+archetype.configId && entry.stableId==archetype.stableId && !entry.retired),
                        $"怪物 {archetype.name} 未正确登记在 {LedgerPath}（category 为 monster、编号 {archetype.configId}）。");
            }
        }

        private static void ValidateMain()
        {
            var scene=EditorSceneManager.OpenScene(MainScenePath,OpenSceneMode.Single);
            var navigation=Navigation(scene);
            Route(navigation,"main.start",SceneRouteAction.LoadScene,"Scene.SelectScene",false);
            Route(navigation,"main.quit",SceneRouteAction.Quit,null,false);
            Buttons(scene,("main.start",0),("main.quit",0));
        }

        private static void ValidateSelect(StartSelectionCatalog catalog)
        {
            var scene=EditorSceneManager.OpenScene(SelectScenePath,OpenSceneMode.Single);
            var controllers=All<SelectSceneController>(scene).ToArray();
            Check(controllers.Length==1,"选择场景必须恰好有一个开局选择流程组件。");
            var controller=controllers[0];
            Inspector(controller,"SelectSceneControllerChineseInspector");
            Check(controller.catalog==catalog,"选择场景的开局选择流程必须引用 "+CatalogPath);
            var navigation=Navigation(scene);
            Check(controller.ui && controller.ui.gameObject==navigation.gameObject,"开局选择流程必须引用场景唯一的 UI 管理器（与场景导航在同一 Canvas 上）。");
            Route(navigation,SelectSceneController.MenuCommand,SceneRouteAction.LoadScene,"Scene.Main",false);
            foreach(string command in new[] {SelectSceneController.HeroCommand,SelectSceneController.MapCommand,SelectSceneController.NextCommand,SelectSceneController.BackCommand,SelectSceneController.StartCommand})
                Check(navigation.Find(command)<0,"场景导航不应处理选择命令 "+command+"（由开局选择流程处理）。");
            var showcase=controller.showcase;
            Check(showcase && showcase.HasBindings && showcase.model.IsChildOf(showcase.turntable),"英雄展示台缺少旋转台或模型节点，或模型节点不在旋转台下。");
            Check(showcase.model.GetComponentsInChildren<Renderer>(true).Length>0,"英雄展示台的模型节点下缺少默认外形。");
            Inspector(showcase,"HeroShowcaseChineseInspector");
            Check(All<Camera>(scene).Any(camera=>camera.enabled && camera.CompareTag("MainCamera")),"选择场景缺少启用的 Main Camera（--select-smoke 截图使用）。");
            var panel=Panel(scene,SelectPanelId);
            Check(panel.transform.IsChildOf(controller.ui.transform),"选择面板必须位于开局选择流程引用的 UI 管理器之下。");
            foreach(string key in SelectSceneController.RequiredKeys()) Binding(panel,key);
            var buttons=new List<(string,int)> {(SelectSceneController.NextCommand,0),(SelectSceneController.BackCommand,0),(SelectSceneController.StartCommand,0),(SelectSceneController.MenuCommand,0)};
            for(int i=0;i<SelectSceneController.HeroSlots;i++) buttons.Add((SelectSceneController.HeroCommand,i));
            for(int k=0;k<SelectSceneController.MapSlots;k++) buttons.Add((SelectSceneController.MapCommand,k));
            Buttons(scene,buttons.ToArray());
        }

        private static void ValidateBattlefield(StartSelectionCatalog catalog,GameMapDefinition map)
        {
            string sceneError=SceneCatalogLookup.Check(map.sceneId);
            Check(sceneError==null,map.name+" 的场景 ID 无效："+sceneError);
            string name=SceneCatalogLookup.Resolve(map.sceneId);
            string path=GameTools.BuildScenes.FirstOrDefault(scenePath=>Path.GetFileNameWithoutExtension(scenePath)==name);
            Check(path!=null,"战场场景不在 Windows 构建场景列表（GameTools.BuildScenes）中："+name);
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            var controllers=All<GameCombatController>(scene).ToArray();
            Check(controllers.Length==1,"战场场景必须恰好有一个战斗控制器："+name);
            var controller=controllers[0];
            Inspector(controller,"GameCombatControllerChineseInspector");
            Check(controller.map==map,$"战场场景 {name} 的战斗控制器“战场定义”必须指向 {map.name}。");
            Check(controller.ActiveRoster==map.enemyRoster,$"战场场景 {name} 必须使用 {map.name} 的敌人名册。");
            CombatSceneAuthoring.Validate(controller); checks++;
            foreach(var hero in catalog.heroes)
            {
                try { hero.ResolveSkills(controller.skillCatalog); checks++; }
                catch(InvalidOperationException error) { Check(false,$"英雄 {hero.name} 的技能无法在 {name} 的技能库中解析：{error.Message}"); }
            }
            var navigation=Navigation(scene);
            var leave=Route(navigation,LeaveCommand,SceneRouteAction.LoadScene,"Scene.SelectScene",true);
            Check(leave.confirmSeconds<=3,$"{name} 的“返回选择”确认时间应不超过 3 秒（--select-smoke 等待 3.4 秒检查超时取消）。");
            Route(navigation,ReselectCommand,SceneRouteAction.LoadScene,"Scene.SelectScene",false);
            var controls=Panel(scene,ControlsPanelId);
            Check(controls.texts.Any(binding=>binding.key=="top.map" && binding.target),"控制面板缺少战场名称文本（top.map）："+name);
            var confirm=controls.toggles.Where(binding=>binding.key==SceneNavigation.ConfirmKey(LeaveCommand) && binding.target).ToArray();
            Check(confirm.Any(binding=>!binding.invert && !binding.target.activeSelf) && confirm.Any(binding=>binding.invert),
                "控制面板的“返回选择”按钮需要 game.leave.confirm 正反两个显隐绑定，且确认提示默认隐藏："+name);
            Check(controls.GetComponentsInChildren<UIAction>(true).Any(action=>action.command==LeaveCommand && action.GetComponent<Button>()),"控制面板缺少“返回选择”按钮（game.leave）："+name);
            var death=Panel(scene,DeathPanelId);
            Check(death.GetComponentsInChildren<UIAction>(true).Any(action=>action.command==ReselectCommand && action.GetComponent<Button>()),"结算面板缺少“返回选择”按钮（game.select）："+name);
            if(name=="Game")
            {
                var probe=controller.GetComponent<GameCombatProbe>();
                Check(probe && probe.selectionCatalog==catalog,"Game 的 Player 集成检查组件必须引用开局选择目录（英雄切换检查使用）。");
                Inspector(probe,"GameCombatProbeChineseInspector");
            }
        }

        /// <summary>场景唯一的场景导航：与 UI 管理器同在根 Canvas 上，路由表无错误，并使用中文 Inspector。</summary>
        private static SceneNavigation Navigation(Scene scene)
        {
            var navigations=All<SceneNavigation>(scene).ToArray();
            Check(navigations.Length==1,scene.name+" 必须恰好有一个场景导航组件。");
            var navigation=navigations[0];
            Check(navigation.GetComponent<UIManager>() && navigation.GetComponent<Canvas>() && navigation.GetComponent<Canvas>().isRootCanvas,scene.name+" 的场景导航必须挂在带 UI 管理器的根 Canvas 上。");
            var errors=SceneNavigationChecks.Validate(navigation);
            Check(errors.Count==0,scene.name+" 的场景导航路由无效："+string.Join("；",errors));
            Inspector(navigation,"SceneNavigationChineseInspector");
            return navigation;
        }

        private static SceneNavigation.Route Route(SceneNavigation navigation,string command,SceneRouteAction action,string sceneId,bool confirm)
        {
            string scene=navigation.gameObject.scene.name;
            Check(SceneNavigationChecks.TryGet(navigation,command,out var route),scene+" 的场景导航缺少命令 "+command);
            Check(route.action==action && (action!=SceneRouteAction.LoadScene || route.sceneId==sceneId),$"{scene} 的 {command} 应{(action==SceneRouteAction.Quit?"退出游戏":"加载 "+sceneId)}。");
            Check(confirm?route.confirmSeconds>0:route.confirmSeconds==0,$"{scene} 的 {command} {(confirm?"需要二次确认（确认时间大于 0）":"应立即执行（确认时间为 0）")}。");
            return route;
        }

        private static UIPanel Panel(Scene scene,string panelId)
        {
            var panels=All<UIPanel>(scene).Where(panel=>panel.panelId==panelId).ToArray();
            Check(panels.Length==1,$"{scene.name} 必须恰好有一个 {panelId} 面板。");
            Check(PrefabUtility.IsPartOfPrefabInstance(panels[0]),$"{scene.name} 的 {panelId} 必须是面板 Prefab 实例。");
            return panels[0];
        }

        /// <summary>选择面板的展示键：步骤与 visible / selected 为显隐绑定，portrait / icon / preview 为图片绑定，其余为文本绑定。</summary>
        private static void Binding(UIPanel panel,string key)
        {
            string part=key.Substring(key.LastIndexOf('.')+1);
            bool found;
            if(key.StartsWith("select.step.",StringComparison.Ordinal) || part=="visible" || part=="selected")
                found=panel.toggles.Any(binding=>binding.key==key && !binding.invert && binding.target && binding.target.transform.IsChildOf(panel.transform));
            else if(part=="portrait" || part=="icon" || part=="preview")
                found=panel.images.Any(binding=>binding.key==key && binding.target && binding.target.transform.IsChildOf(panel.transform));
            else found=panel.texts.Any(binding=>binding.key==key && binding.target && binding.target.transform.IsChildOf(panel.transform));
            Check(found,"选择面板缺少展示键绑定："+key);
        }

        private static void Buttons(Scene scene,params (string command,int value)[] expected)
        {
            var actions=All<UIAction>(scene).Where(action=>action.GetComponent<Button>()).ToArray();
            foreach(var (command,value) in expected)
                Check(actions.Any(action=>action.command==command && action.value==value),$"{scene.name} 缺少按钮 {command}（参数 {value}）。");
        }

        /// <summary>目标对象的 Inspector 必须是项目的中文 Inspector（按类型名核对，跨程序集的类型为 internal）。</summary>
        private static void Inspector(UnityEngine.Object target,string expected)
        {
            var editor=UnityEditor.Editor.CreateEditor(target);
            try { Check(editor && editor.GetType().Name==expected,$"{target.GetType().Name} 未使用中文 Inspector {expected}。"); }
            finally { if(editor) UnityEngine.Object.DestroyImmediate(editor); }
        }

        private static IEnumerable<T> All<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<T>(true));

        private static void Check(bool passed,string message)
        {
            if(!passed) throw new InvalidOperationException("开局选择流程校验失败："+message);
            checks++;
        }
    }
}
