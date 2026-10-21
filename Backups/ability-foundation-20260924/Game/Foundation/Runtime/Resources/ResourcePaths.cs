using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roguelike.Foundation
{
    /// <summary>资源加载唯一入口。文本清单保存 ID→路径，字典只缓存路径，资源按需交给 Unity 加载。</summary>
    public static class ResourcePaths
    {
        public const string ManifestPath = "ProjectResourceCatalog";
        // 旧音频 API 的命名适配也集中在此。返回清单 ID，实际文件路径仍以文本清单为准。
        public static string BackgroundMusicId(string name) => "Music/BK/" + name;
        public static string SoundId(string name) => "Music/Sound/" + name;
        [Serializable] public sealed class Entry { public string id, path, kind; }
        [Serializable] public sealed class Manifest { public int managedVersion; public Entry[] entries; }
        private static Dictionary<string, Entry> entries;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => entries = null;
        public static string Resolve(string id, string kind = "resource")
        {
            EnsureLoaded();
            if (!entries.TryGetValue(id, out var entry) || entry.kind != kind)
                throw new InvalidOperationException("资源 ID 未登记或类型不符：" + id);
            return entry.path;
        }
        private static void EnsureLoaded()
        {
            if (entries != null) return;
            var text = Resources.Load<TextAsset>(ManifestPath);
            if (!text) throw new InvalidOperationException("缺少 Resources/ProjectResourceCatalog.json");
            var parsed = JsonUtility.FromJson<Manifest>(text.text);
            var map = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in parsed.entries)
            {
                if (string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.path) || map.ContainsKey(entry.id))
                    throw new InvalidOperationException("资源清单存在空路径或重复 ID：" + entry.id);
                map.Add(entry.id, entry);
            }
            entries = map;
        }
        public static T Load<T>(string id) where T : Object
        {
            var result = Resources.Load<T>(Resolve(id));
            if (!result) throw new InvalidOperationException("无法加载资源：" + id);
            return result;
        }
        public static IEnumerator LoadAsync<T>(string id, Action<T> complete) where T : Object
        {
            var request = Resources.LoadAsync<T>(Resolve(id));
            yield return request;
            if (!(request.asset is T value)) { Debug.LogError("无法加载资源：" + id); complete(null); yield break; }
            complete(value);
        }
    }
}
