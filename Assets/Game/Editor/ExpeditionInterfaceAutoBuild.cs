using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Roguelike.Editor
{
    /// <summary>
    /// 打开 Unity（或脚本重新编译）时检查界面版本：生成代码的 <see cref="ExpeditionSceneAuthoring.LayoutVersion"/>
    /// 与上次生成写下的版本不同，就自动执行“生成界面并装配战场”。有未保存场景时先询问保存，取消则跳过并提示。
    /// 批处理模式（命令行构建、验证）不自动执行；每个编辑器会话最多自动尝试一次。
    /// </summary>
    [InitializeOnLoad]
    internal static class ExpeditionInterfaceAutoBuild
    {
        internal const string StampPath="Assets/Game/UI/界面版本.txt";
        const string SessionKey="Roguelike.ExpeditionInterfaceAutoBuild.Tried";
        static ExpeditionInterfaceAutoBuild()
        {
            if(Application.isBatchMode) return;
            EditorApplication.delayCall+=Check;
        }
        static void Check()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {EditorApplication.delayCall+=Check;return;}
            if(Stamp()==ExpeditionSceneAuthoring.LayoutVersion || SessionState.GetBool(SessionKey,false)) return;
            SessionState.SetBool(SessionKey,true);
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.LogWarning("界面生成代码已更新，但当前场景未保存，已跳过自动生成。请保存后执行 Tools/Roguelike/远征/生成界面并装配战场。");
                return;
            }
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ExpeditionSceneAuthoring.Apply();
                Debug.Log("界面已按生成代码自动重新生成："+ExpeditionSceneAuthoring.LayoutVersion);
            }
            catch(System.Exception error) {Debug.LogException(error);}
            finally {if(setup!=null && setup.Length>0) EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        static string Stamp()=>File.Exists(StampPath)?File.ReadAllText(StampPath).Trim():"";
        internal static void WriteStamp()
        {
            File.WriteAllText(StampPath,ExpeditionSceneAuthoring.LayoutVersion);
            AssetDatabase.ImportAsset(StampPath);
        }
    }
}
