using System;
using System.Collections.Generic;
using System.IO;
using Roguelike.Abilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>隔离编辑器中的框架集成检查：配置往返、Undo、编译拒绝及几何语义；不运行游戏。</summary>
    public static class SkillAuthoringValidation
    {
        private static int checks;
        public static void Run()
        {
            checks=0;
            SkillAuthoringSceneTools.EnsureScene();
            string sceneText=File.ReadAllText(SkillAuthoringSceneTools.ScenePath);
            SkillAuthoringSceneTools.EnsureScene();
            Check(sceneText==File.ReadAllText(SkillAuthoringSceneTools.ScenePath),"重复搭建不覆盖场景");
            foreach(var buildScene in EditorBuildSettings.scenes) Check(buildScene.path!=SkillAuthoringSceneTools.ScenePath,"工作台未进入玩家构建");
            var scene=EditorSceneManager.OpenScene(SkillAuthoringSceneTools.ScenePath,OpenSceneMode.Single);
            var workspace=SkillAuthoringSceneTools.Find(scene);
            Check(workspace.skill && workspace.catalog && workspace.caster && workspace.target,"场景显式引用");
            var cube=GameObject.Find("1×1×1 Unity 单位参照"); Check(cube && cube.transform.lossyScale==Vector3.one,"一单位立方体");
            var initialPreview=SkillAuthoringSceneGUI.Get(workspace,true); Check(initialPreview.Valid,"示例技能编译");
            var inspector=UnityEditor.Editor.CreateEditor(workspace); Check(inspector is SkillAuthoringWorkspaceEditor,"中文工作台 Inspector 注册"); UnityEngine.Object.DestroyImmediate(inspector);
            var skillInspector=UnityEditor.Editor.CreateEditor(workspace.skill); Check(skillInspector.GetType().Name=="SkillDefinitionChineseInspector","复用既有中文技能 Inspector"); UnityEngine.Object.DestroyImmediate(skillInspector);
            string path=SkillAuthoringSceneTools.ContentRoot+"/校验临时技能.asset";
            string tempScene=AssetDatabase.GenerateUniqueAssetPath(SkillAuthoringSceneTools.ContentRoot+"/校验临时场景.unity");
            string copyPath=null;
            var skill=SkillAuthoringAssets.CreateAtPath(path,workspace.skill);
            path=AssetDatabase.GetAssetPath(skill);
            var temporaryCatalog=ScriptableObject.CreateInstance<SkillCatalog>();
            temporaryCatalog.hideFlags=HideFlags.HideAndDontSave;
            try
            {
                Check(skill.stableId!=workspace.skill.stableId && skill.stages[0].effects[0]==workspace.skill.stages[0].effects[0],"复制新 ID，效果保持共享");
                workspace.skill=skill;
                Check(RecipeAuthoringCommands.Enabled(skill),"工作台正式示例使用当前数字配方");
                float recipeRadius=skill.recipe.nodes[0].radius+1;
                int recipeRevision=skill.recipe.revision;
                skill.recipe.nodes[0].radius=recipeRadius;
                EditorUtility.SetDirty(skill);SkillAuthoringAssets.Save(skill);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
                skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);workspace.skill=skill;
                Check(skill.recipe.nodes[0].radius==recipeRadius && skill.recipe.revision==recipeRevision+1,"当前配方编辑保存并重新导入保留参数与修订");
                var recipePreview=SkillAuthoringSceneGUI.Get(workspace,true);
                Check(recipePreview.Valid && Mathf.Approximately(recipePreview.Stages[0].Radius,recipeRadius),"当前配方保存后驱动工作台预览");
                // 下方独立覆盖旧阶段兼容与几何；不能让已启用的配方掩盖测试阶段。
                skill.recipe.enabled=false;
                skill.stages=new[]
                {
                    new SkillStage {shape=AreaShape.Circle,radius=5,effects=skill.stages[0].effects},
                    new SkillStage {shape=AreaShape.Cone,range=10,halfAngleDegrees=35,effects=skill.stages[0].effects},
                    new SkillStage {shape=AreaShape.Line,range=10,radius=2,effects=skill.stages[0].effects},
                    new SkillStage {delivery=DeliveryKind.LinearProjectile,shape=AreaShape.Circle,range=10,radius=7,hitRadius=1,speed=20,effects=skill.stages[0].effects}
                };
                skill.indicatorColor=new Color(0.2f,0.7f,0.8f,0.6f);
                var preview=SkillAuthoringSceneGUI.Get(workspace,true); Check(preview.Valid && preview.Stages.Length==4,"四阶段快照");
                Check(preview.Stages[0].IndicatorColor==skill.indicatorColor,"指示器颜色进入编译快照");
                var points=new List<Vector3>(); var origin=new Vector3(3,2,4);
                SkillPreviewGeometry.AreaOutline(preview.Stages[0],origin,Vector3.forward,points);
                foreach(var point in points) Near(Vector3.Distance(point,origin),5,"圆形边界半径为 5 世界单位");
                workspace.transform.localScale=Vector3.one*3;
                SkillAuthoringSceneGUI.Placement(workspace,preview.Stages[0],out _,out _,out _,out var center);
                SkillPreviewGeometry.AreaOutline(preview.Stages[0],center,Vector3.forward,points); Near(Vector3.Distance(points[0],center),5,"父缩放不放大范围");
                workspace.transform.localScale=Vector3.one;
                SkillPreviewGeometry.AreaOutline(preview.Stages[2],Vector3.zero,Vector3.forward,points);
                Near(Vector3.Distance(points[0],points[3]),4,"直线总宽为半宽两倍"); Near(Vector3.Distance(points[0],points[1]),10,"直线长度");
                var stageData=new StageData{Shape=AreaShape.Cone,Range=10,HalfAngleDegrees=35};
                Check(SkillTargeting.Contains(ref stageData,Vector3.zero,Vector3.forward,Quaternion.Euler(0,34,0)*Vector3.forward*9),"扇形半角内包含");
                Check(!SkillTargeting.Contains(ref stageData,Vector3.zero,Vector3.forward,Quaternion.Euler(0,36,0)*Vector3.forward*9),"扇形半角外排除");
                SkillPreviewGeometry.ProjectileOutline(preview.Stages[3],Vector3.zero,Vector3.forward,points);
                float minZ=float.MaxValue,maxZ=float.MinValue; foreach(var point in points){minZ=Mathf.Min(minZ,point.z);maxZ=Mathf.Max(maxZ,point.z);}
                Near(minZ,-1,"弹体起点圆帽"); Near(maxZ,11,"弹体终点圆帽，宽度不取爆炸半径 7");
                var all=preview.Stages[0];all.Scope=TargetScope.All;SkillPreviewGeometry.AreaOutline(all,origin,Vector3.forward,points);Check(points.Count==0,"全体无虚假有限边界");
                var projectile=preview.Stages[0];projectile.Aim=AimMode.Direction;projectile.Delivery=DeliveryKind.PointProjectile;
                Check(SkillPreviewGeometry.AreaCenter(projectile,Vector3.zero,Vector3.right*5)==Vector3.right*5,"方向瞄准的落点弹体使用落点中心");
                Check(SkillPreviewGeometry.DeliveryDirection(projectile,Vector3.zero,Vector3.right*5,Vector3.forward)==Vector3.right,"落点弹体使用实际飞行方向");
                Undo.IncrementCurrentGroup(); SkillAuthoringSceneGUI.SetFloat(skill,"stages.Array.data[0].radius",6); Undo.FlushUndoRecordObjects();
                Near(skill.stages[0].radius,6,"句柄写回技能"); Undo.PerformUndo();Near(skill.stages[0].radius,5,"Undo 恢复技能");Undo.PerformRedo();Near(skill.stages[0].radius,6,"Redo 恢复技能");
                workspace.stageIndex=2;workspace.showAllStages=true;workspace.target.position=new Vector3(9,0,3);
                EditorUtility.SetDirty(skill);AssetDatabase.SaveAssetIfDirty(skill);
                Check(EditorSceneManager.SaveScene(scene,tempScene,true),"工作环境另存成功");
                scene=EditorSceneManager.OpenScene(tempScene,OpenSceneMode.Single);workspace=SkillAuthoringSceneTools.Find(scene);
                Check(workspace.skill==skill && workspace.stageIndex==2 && workspace.showAllStages && workspace.target.position==new Vector3(9,0,3),"关闭重开恢复预览引用、阶段、目标锚点");
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);skill=AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);Near(skill.stages[0].radius,6,"技能文件重新导入保留参数");
                workspace.skill=skill; workspace.catalog=temporaryCatalog;
                SkillAuthoringAssets.Register(workspace);Check(temporaryCatalog.skills.Length==1 && temporaryCatalog.skills[0]==skill,"显式登记成功");
                SkillAuthoringAssets.Register(workspace);Check(temporaryCatalog.skills.Length==1,"重复登记不新增条目");
                var copy=SkillAuthoringAssets.CreateAtPath(SkillAuthoringSceneTools.ContentRoot+"/校验冲突技能.asset",skill);copyPath=AssetDatabase.GetAssetPath(copy);
                copy.stableId=skill.stableId; workspace.skill=copy; bool rejected=false;
                try{SkillAuthoringAssets.Register(workspace);}catch(InvalidOperationException){rejected=true;}
                Check(rejected && temporaryCatalog.skills.Length==1,"重复 ID 登记被拒绝且库未部分写入");
                workspace.skill=skill; skill.stages[0].radius=-1;Check(!SkillAuthoringSceneGUI.Get(workspace,true).Valid,"非法半径被编译拒绝");skill.stages[0].radius=6;
                skill.indicatorColor=new Color(float.NaN,0,0,1);Check(!SkillAuthoringSceneGUI.Get(workspace,true).Valid,"非法颜色被编译拒绝");
            }
            finally
            {
                EditorSceneManager.OpenScene(SkillAuthoringSceneTools.ScenePath,OpenSceneMode.Single);
                AssetDatabase.DeleteAsset(tempScene);AssetDatabase.DeleteAsset(path);if(copyPath!=null) AssetDatabase.DeleteAsset(copyPath);
                UnityEngine.Object.DestroyImmediate(temporaryCatalog);Undo.ClearAll();
            }
            var gameCatalog=AssetDatabase.LoadAssetAtPath<SkillCatalog>("Assets/GameData/目录/游戏技能库.asset");
            using(var blob=gameCatalog.Compile())
            {
                for(int i=0;i<blob.Value.Skills.Length;i++)
                {var snapshot=SkillAimPreview.From(ref blob.Value.Skills[i]);Check(snapshot.IndicatorColor==gameCatalog.skills[i].indicatorColor,"Game 技能颜色快照一致");}
            }
            Debug.Log("SKILL_AUTHORING_VALIDATION_PASSED checks="+checks);
        }
        private static void Check(bool condition,string label){if(!condition) throw new InvalidOperationException("技能工作台检查失败："+label);checks++;}
        private static void Near(float actual,float expected,string label)=>Check(Mathf.Abs(actual-expected)<0.001f,label+$"，实际 {actual} / 预期 {expected}");
    }
}
