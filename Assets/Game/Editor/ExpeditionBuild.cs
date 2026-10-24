using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Roguelike.Editor
{
    public static class ExpeditionBuild
    {
        public static void ValidateTools()
        {
            Roguelike.Abilities.Editor.AbilityInspectorValidation.Validate();
            Roguelike.SkillAuthoring.Editor.RecipeEditorChecks.Validate();
            Roguelike.SkillAuthoring.Editor.HeroSkillDocumentation.Export();
        }
        public static void Build()
        {
            CombatValidation.ValidateExpeditionCore();
            GameFlowValidation.Validate();
            string output="Builds/Expedition";var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--expedition-build");if(index>=0 && index+1<args.Length) output=args[index+1];
            System.IO.Directory.CreateDirectory(output);
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=GameTools.BuildScenes,target=BuildTarget.StandaloneWindows64,
                locationPathName=output+"/Game.exe",options=BuildOptions.None});
            if(result.summary.result!=BuildResult.Succeeded) throw new InvalidOperationException("远征构建失败。");
            Debug.Log("EXPEDITION_BUILD_SUCCEEDED");
        }
    }
}
