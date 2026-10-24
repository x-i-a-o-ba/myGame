using System.Collections.Generic;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Combat;
using Roguelike.Flow;
using Roguelike.Foundation.Editor;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>开局选择流程各 Inspector 共用的中文字段与分组绘制。</summary>
    internal static class GameFlowInspectorLayout
    {
        public static void Field(SerializedObject serialized,string name,string label,string tooltip=null)
        {
            var property=serialized.FindProperty(name);
            if(property==null) { EditorGUILayout.HelpBox("缺少字段："+name,MessageType.Error); return; }
            EditorGUILayout.PropertyField(property,new GUIContent(label,tooltip??property.tooltip),true);
        }
        public static void Section(string text) { EditorGUILayout.Space(7); EditorGUILayout.LabelField(text,EditorStyles.boldLabel); }
        public static void Errors(IReadOnlyCollection<string> errors,string valid)
        {
            if(errors.Count==0) EditorGUILayout.HelpBox(valid,MessageType.None);
            foreach(var error in errors) EditorGUILayout.HelpBox(error,MessageType.Error);
        }
        public static string HeroName(HeroDefinition hero)=>!hero?"（空）":string.IsNullOrWhiteSpace(hero.displayName)?hero.name:hero.displayName;

        /// <summary>对象引用数组的中文列表：每行“名称 序号”，可上移、删除与添加（不超过 max 项）。</summary>
        public static void ReferenceList(SerializedProperty array,string item,int max,string tooltip)
        {
            int remove=-1,up=-1;
            for(int i=0;i<array.arraySize;i++)
                using(new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(array.GetArrayElementAtIndex(i),new GUIContent(item+" "+(i+1),tooltip));
                    using(new EditorGUI.DisabledScope(i==0)) if(GUILayout.Button("上移",GUILayout.Width(40))) up=i;
                    if(GUILayout.Button("删除",GUILayout.Width(40))) remove=i;
                }
            if(up>0) array.MoveArrayElement(up,up-1);
            if(remove>=0)
            {
                // 先清空引用再删除，兼容“删除非空引用只清空不移除”的旧行为。
                array.GetArrayElementAtIndex(remove).objectReferenceValue=null;
                array.DeleteArrayElementAtIndex(remove);
            }
            using(new EditorGUI.DisabledScope(array.arraySize>=max))
                if(GUILayout.Button(array.arraySize>=max?$"已达上限（{max} 个{item}）":"添加"+item))
                {
                    array.InsertArrayElementAtIndex(array.arraySize);
                    array.GetArrayElementAtIndex(array.arraySize-1).objectReferenceValue=null;
                }
        }
    }

    /// <summary>战场定义的中文 Inspector：分组显示展示与战斗字段，并校验场景 ID、名册与难度曲线。</summary>
    [CustomEditor(typeof(GameMapDefinition))]
    [CanEditMultipleObjects]
    internal sealed class GameMapDefinitionChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("战场定义：选择界面第 2 步的一张战场卡。开始战斗时加载“战场场景 ID”指向的场景，并用本战场的敌人名册与关卡难度曲线"+
                "覆盖该场景战斗控制器上的配置，所以不同战场刷出不同的怪物。加入“开局选择目录”后才会出现在选择界面；对应战场场景的战斗控制器“战场定义”也应指向本资源。"+
                "修改后重新进入战场生效。",MessageType.Info);
            GameFlowInspectorLayout.Section("身份");
            Field("configId","数字配置 ID","战场使用 7000–7999，需登记在 Docs/游戏配置/编号登记.json（category 为 map）。");
            Field("stableId","内部兼容键","日志与验证使用，例如 map.shadow_wastes；不随名称变化。");
            GameFlowInspectorLayout.Section("选择界面展示");
            Field("displayName","战场名称","选择界面战场卡与战斗中右上角“战场 · 名称”显示。");
            Field("description","简介",$"战场卡上的简介；超过 {SelectSceneController.MapTextMax} 个字符时在之前最后一个句末标点处截断显示，完整文本保留在资源中。");
            Field("preview","预览图","战场卡顶部的预览图（建议 16:9）；开局选择目录要求必填。");
            GameFlowInspectorLayout.Section("战斗");
            Field("sceneId","战场场景 ID","资源清单（Resources/ProjectResourceCatalog.json）中的场景 ID，格式 Scene.<场景名>；开始战斗时加载，场景须在 Build Settings 中启用。");
            Field("enemyRoster","敌人名册",$"本战场刷出的怪物（必填）；选择界面列出其中的怪物种类，最多显示 {SelectSceneController.EnemySlots} 种。");
            Field("difficulty","关卡难度曲线","分钟难度成长、精英与首领数值；开局选择目录要求必填。为空时使用战场场景控制器上的曲线。");
            serializedObject.ApplyModifiedProperties();
            if(targets.Length==1) Report((GameMapDefinition)target);
        }

        private static void Report(GameMapDefinition map)
        {
            GameFlowInspectorLayout.Section("校验与预览");
            var errors=new List<string>();
            string error=map.Validate();
            if(error!=null) errors.Add(error);
            else if((error=SceneCatalogLookup.Check(map.sceneId))!=null) errors.Add(error);
            if(!map.difficulty) errors.Add("缺少关卡难度曲线（开局选择目录要求必填）。");
            if(!map.preview) errors.Add("缺少预览图（开局选择目录要求必填）。");
            if(errors.Count>0) { GameFlowInspectorLayout.Errors(errors,null); return; }
            var kinds=SelectSceneController.Kinds(map.enemyRoster);
            EditorGUILayout.HelpBox($"配置有效。场景 {SceneCatalogLookup.Resolve(map.sceneId)}；持续补怪 / 计时首领；"+
                $"{kinds.Count} 种怪物：{string.Join("、",kinds.Select(k=>k.archetype.Label))}。",MessageType.None);
            if(kinds.Count>SelectSceneController.EnemySlots)
                EditorGUILayout.HelpBox($"名册有 {kinds.Count} 种怪物，选择界面只显示前 {SelectSceneController.EnemySlots} 种（战斗仍按完整名册刷怪）。",MessageType.Warning);
            foreach(var kind in kinds)
                if(!kind.archetype.portrait) EditorGUILayout.HelpBox("怪物缺少头像，选择界面改用攻击模块图标："+kind.archetype.Label,MessageType.Warning);
        }

        private void Field(string name,string label,string tooltip)=>GameFlowInspectorLayout.Field(serializedObject,name,label,tooltip);
    }

    /// <summary>开局选择目录的中文 Inspector：按界面顺序编辑英雄与战场列表，并校验目录与各战场。</summary>
    [CustomEditor(typeof(StartSelectionCatalog))]
    internal sealed class StartSelectionCatalogChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("开局选择目录：主菜单“开始游戏”后，选择界面先列出这里的英雄，再列出这里的战场；列表顺序即界面顺序，第一项为默认选中。"+
                "每个战场引用自己的场景与敌人名册。选择场景的“开局选择流程”引用本资源，修改后重新进入选择界面生效。",MessageType.Info);
            var heroes=serializedObject.FindProperty("heroes");
            var maps=serializedObject.FindProperty("maps");
            if(heroes==null || maps==null) { EditorGUILayout.HelpBox("缺少字段：heroes / maps",MessageType.Error); return; }
            GameFlowInspectorLayout.Section($"可选英雄（{heroes.arraySize} / {SelectSceneController.HeroSlots}）");
            GameFlowInspectorLayout.ReferenceList(heroes,"英雄",SelectSceneController.HeroSlots,"英雄需填写名称、头像、定位标签与简介，技能槽 1–4 个。");
            GameFlowInspectorLayout.Section($"可选战场（{maps.arraySize} / {SelectSceneController.MapSlots}）");
            GameFlowInspectorLayout.ReferenceList(maps,"战场",SelectSceneController.MapSlots,"战场需有场景 ID、敌人名册、关卡难度曲线与预览图。");
            serializedObject.ApplyModifiedProperties();
            var catalog=(StartSelectionCatalog)target;
            GameFlowInspectorLayout.Section("校验与预览");
            string error=catalog.Validate();
            var errors=new List<string>();
            if(error!=null) errors.Add(error);
            else
                foreach(var map in catalog.maps)
                {
                    string scene=SceneCatalogLookup.Check(map.sceneId);
                    if(scene!=null) errors.Add(map.Label+"："+scene);
                }
            if(errors.Count>0) { GameFlowInspectorLayout.Errors(errors,null); return; }
            EditorGUILayout.HelpBox("目录有效。",MessageType.None);
            for(int i=0;i<catalog.heroes.Length;i++)
            {
                var hero=catalog.heroes[i];
                EditorGUILayout.LabelField($"英雄 {i+1}：{GameFlowInspectorLayout.HeroName(hero)} · {hero.role} · {hero.skills.Length} 个技能",EditorStyles.wordWrappedMiniLabel);
            }
            var owners=new Dictionary<EnemyArchetype,string>();
            for(int i=0;i<catalog.maps.Length;i++)
            {
                var map=catalog.maps[i];
                var kinds=SelectSceneController.Kinds(map.enemyRoster);
                EditorGUILayout.LabelField($"战场 {i+1}：{map.Label} → {map.sceneId} · {kinds.Count} 种怪物",EditorStyles.wordWrappedMiniLabel);
                foreach(var kind in kinds)
                {
                    if(owners.TryGetValue(kind.archetype,out var other))
                        EditorGUILayout.HelpBox($"怪物 {kind.archetype.Label} 同时出现在 {other} 与 {map.Label}：不同战场通常使用不同的怪物。",MessageType.Warning);
                    else owners.Add(kind.archetype,map.Label);
                }
            }
        }
    }

    /// <summary>开局选择流程（SelectSceneController）的中文 Inspector：中文引用字段、目录校验与运行时状态。</summary>
    [CustomEditor(typeof(SelectSceneController))]
    internal sealed class SelectSceneControllerChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("开局选择流程：第 1 步选英雄，第 2 步选战场，“开始战斗”后记录选择并加载所选战场的场景。"+
                "键盘：← → 切换，Enter 下一步 / 开始战斗，Esc 上一步 / 返回主菜单（返回主菜单的 select.menu 由同一 Canvas 上的场景导航处理）。修改后重新进入选择界面生效。",MessageType.Info);
            GameFlowInspectorLayout.Field(serializedObject,"ui","UI 管理器","场景根 Canvas 上的 UIManager：选择面板的展示键与按钮命令都经过它。");
            GameFlowInspectorLayout.Field(serializedObject,"catalog","开局选择目录","可选英雄与战场，顺序即界面顺序。");
            GameFlowInspectorLayout.Field(serializedObject,"showcase","英雄展示台","第 1 步在界面中间展示所选英雄的模型；为空时只显示界面。");
            serializedObject.ApplyModifiedProperties();
            if(targets.Length!=1) return;
            var controller=(SelectSceneController)target;
            GameFlowInspectorLayout.Section("校验");
            var errors=new List<string>();
            if(!controller.ui) errors.Add("缺少 UI 管理器。");
            if(!controller.catalog) errors.Add("缺少开局选择目录。");
            else
            {
                string error=controller.catalog.Validate();
                if(error!=null) errors.Add(error);
            }
            if(controller.showcase && !controller.showcase.HasBindings) errors.Add("英雄展示台缺少旋转台或模型节点。");
            GameFlowInspectorLayout.Errors(errors,"引用有效。");
            if(Application.isPlaying && controller.IsReady)
                EditorGUILayout.HelpBox($"运行中：第 {(controller.ChoosingMap?2:1)} 步；英雄 {GameFlowInspectorLayout.HeroName(controller.Hero)}；战场 {(controller.Map?controller.Map.Label:"（空）")}"+
                    (controller.Starting?"；正在进入战场":""),MessageType.None);
        }
    }

    /// <summary>英雄展示台（HeroShowcase）的中文 Inspector。</summary>
    [CustomEditor(typeof(HeroShowcase))]
    internal sealed class HeroShowcaseChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("选择界面的英雄展示台：显示所选英雄的展示预制体（英雄没有展示预制体时显示模型节点下的默认外形）并缓慢旋转；第 2 步（选择战场）时隐藏旋转台。只负责外形，不含战斗数据。",MessageType.Info);
            GameFlowInspectorLayout.Field(serializedObject,"turntable","旋转台","展示模型随它绕竖直轴旋转；第 2 步时隐藏。");
            GameFlowInspectorLayout.Field(serializedObject,"model","模型节点","与战场 Player 节点同尺寸；子物体渲染器是默认外形，展示预制体实例化到这里的原点。");
            GameFlowInspectorLayout.Field(serializedObject,"spinSpeed","旋转速度（度/秒）","0 表示不旋转；负数反向旋转。");
            serializedObject.ApplyModifiedProperties();
            if(targets.Length!=1) return;
            var showcase=(HeroShowcase)target;
            var errors=new List<string>();
            if(!showcase.HasBindings) errors.Add("缺少旋转台或模型节点。");
            else if(!showcase.model.IsChildOf(showcase.turntable)) errors.Add("模型节点不在旋转台下：模型不会随旋转台转动，第 2 步也不会隐藏。");
            GameFlowInspectorLayout.Errors(errors,"引用有效。");
        }
    }

    /// <summary>
    /// 战场场景战斗控制器（GameCombatController）的中文 Inspector：按声明顺序绘制全部序列化字段（保留原分组标题与范围限制），
    /// 字段名替换为中文，并校验“战场定义”与当前场景是否一致。
    /// </summary>
    [CustomEditor(typeof(GameCombatController))]
    internal sealed class GameCombatControllerChineseInspector : UnityEditor.Editor
    {
        private static readonly Dictionary<string,(string label,string tooltip)> Labels=new Dictionary<string,(string,string)>
        {
            ["gameCamera"]=("战斗相机","俯视跟随英雄的相机；为空时使用 Main Camera。"),
            ["playerMaterial"]=("英雄材质（场景制作）","重建场景展示时给英雄默认外形使用；运行时不读取。"),
            ["enemyMaterial"]=("统一敌人材质","未配置名册时统一敌人的材质；敌方弹体材质为空时也用它。"),
            ["projectileMaterial"]=("英雄弹体材质","英雄远程普攻与技能弹体的材质。"),
            ["groundMaterial"]=("地面材质（场景制作）","重建场景展示时给场地地面使用；运行时不读取。"),
            ["markerMaterial"]=("地面物品标记材质","地面掉落装备标记使用的材质。"),
            ["enemyProjectileMaterial"]=("敌方弹体材质",null),
            ["map"]=("战场定义",null),
            ["hero"]=("默认英雄","直接打开本场景时出战的英雄；从选择界面进入时替换为所选英雄。"),
            ["skillCatalog"]=("技能库","英雄技能槽引用的技能必须都在这里登记。"),
            ["progressionRules"]=("经验与等级规则",null),
            ["spawnDirector"]=("刷怪导演","目标存活数、潮涌、精英节奏与软分离，重开生效。"),
            ["difficulty"]=("关卡难度曲线（无战场定义时）","战场定义没有难度曲线时使用；两者都为空时使用默认曲线并输出警告。"),
            ["enemyRoster"]=("敌人名册（无战场定义时）","战场定义没有名册时使用；两者都为空时只刷场景统一敌人（近战，使用下方攻击距离与间隔）。"),
            ["equipmentCatalog"]=("装备库",null),
            ["shop"]=("商店位置",null),
            ["arenaHalfSize"]=("场地半宽（米）","场地是以原点为中心的正方形，边长为该值的两倍；英雄移动与指示器限制在边界内。"),
            ["safeSpawnRadius"]=("安全刷怪半径（米）","敌人不会刷在离英雄这个距离以内。"),
            ["enemyHealth"]=("敌人基础生命（无难度曲线时）","仅在没有任何难度曲线时作为默认曲线的t=0基础生命。"),
            ["enemySpeed"]=("敌人基础移速（米/秒，无难度曲线时）","仅在没有任何难度曲线时作为默认曲线的t=0基础移速。"),
            ["enemyAttackRange"]=("统一敌人攻击距离（米）",null),
            ["enemyAttackDamage"]=("敌人基础攻击力（无难度曲线时）","仅在没有任何难度曲线时作为默认曲线的t=0基础攻击力。"),
            ["enemyAttackInterval"]=("统一敌人攻击间隔（秒）",null),
            ["projectileSpeed"]=("默认弹道速度（米/秒）",null),
            ["cameraZoom"]=("相机缩放（正交尺寸）","数值越大看到的范围越大；运行中可用鼠标滚轮调整（12–160）。"),
            ["playerView"]=("英雄显示节点","英雄外形的父节点：英雄有展示预制体时实例化到这里并隐藏默认外形。"),
            ["destinationMarker"]=("移动目标标记","右键移动时在目标点显示的标记。"),
            ["hud"]=("战斗 HUD","场景 Canvas 上的战斗界面适配器。"),
            ["abilityIndicator"]=("技能指示器","施法瞄准与范围圈显示。"),
            ["enemyMesh"]=("统一敌人网格","未配置名册时统一敌人使用的网格。"),
            ["projectileMesh"]=("弹体网格","英雄与敌方弹体共用的网格。"),
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("战场场景的战斗控制器：开局创建 ECS 战斗会话。从选择界面进入时，英雄替换为所选英雄；所选战场的场景就是本场景时替换“战场定义”。"+
                "直接打开本场景时使用下方配置。标题带“重开生效”的分组在下一局生效。",MessageType.Info);
            var property=serializedObject.GetIterator();
            for(bool enter=true;property.NextVisible(enter);enter=false)
            {
                if(property.propertyPath=="m_Script") continue;
                if(Labels.TryGetValue(property.propertyPath,out var text))
                    EditorGUILayout.PropertyField(property,new GUIContent(text.label,text.tooltip??property.tooltip),true);
                else EditorGUILayout.PropertyField(property,true);
            }
            serializedObject.ApplyModifiedProperties();
            if(targets.Length==1) Report((GameCombatController)target);
        }

        private static void Report(GameCombatController controller)
        {
            GameFlowInspectorLayout.Section("战场校验");
            var errors=new List<string>();
            var map=controller.map;
            string scene=controller.gameObject.scene.name;
            if(!map) EditorGUILayout.HelpBox("未指定战场定义：使用上方的敌人名册与难度曲线；从选择界面进入本场景时不会替换战场。",MessageType.Warning);
            else
            {
                string error=map.Validate();
                if(error!=null) errors.Add(error);
                else if(!string.IsNullOrEmpty(scene) && map.sceneId!="Scene."+scene)
                    errors.Add($"战场定义 {map.Label} 的场景 ID 是 {map.sceneId}，与本场景 {scene} 不符：进入该战场会加载另一个场景。");
            }
            if(!controller.hero || !controller.skillCatalog) errors.Add("缺少默认英雄或技能库。");
            if(errors.Count>0) { GameFlowInspectorLayout.Errors(errors,null); return; }
            var roster=controller.ActiveRoster;
            var difficulty=map && map.difficulty?map.difficulty:controller.difficulty;
            EditorGUILayout.HelpBox($"生效配置：战场 {(map?map.Label:"（无）")}；敌人名册 {(roster?roster.name:"（无，统一敌人）")}；"+
                $"关卡难度曲线 {(difficulty?difficulty.name:"（无，默认曲线）")}；默认英雄 {GameFlowInspectorLayout.HeroName(controller.hero)}。",MessageType.None);
        }
    }

    /// <summary>Player 集成检查（GameCombatProbe）的中文 Inspector。</summary>
    [CustomEditor(typeof(GameCombatProbe))]
    internal sealed class GameCombatProbeChineseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("Player 集成检查：只在命令行带 --game-smoke 时运行，正常游戏中立即停用。结果写入 --game-output 目录（默认 persistentDataPath/GameFoundation）。",MessageType.Info);
            GameFlowInspectorLayout.Field(serializedObject,"selectionCatalog","开局选择目录","英雄切换检查从这里取第二位英雄（磐锋卫士），并核对本场景的战场定义在目录中。");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
