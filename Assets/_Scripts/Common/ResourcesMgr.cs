using UnityEngine;
namespace Common
{
    /// <summary>旧调用兼容入口；资源 ID 和路径由 ProjectResourceCatalog.json 统一管理。</summary>
    public static class ResourcesMgr
    {
        public static T Load<T>(string id) where T : Object => Roguelike.Foundation.ResourcePaths.Load<T>(id);
    }
}
