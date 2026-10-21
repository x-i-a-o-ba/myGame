using UnityEngine;

namespace Roguelike.Abilities
{
    /// <summary>可复用效果模块的编辑期共同边界。编译成非托管数据，运行期不为每个目标构建 SO 实例。</summary>
    public abstract class EffectDefinition : ScriptableObject
    {
        [InspectorName("作用对象"), Tooltip("Selected 使用本段选定目标；Source 在一次阶段内只向施法者应用一次。")]
        public EffectTarget target;
        /// <summary>
        /// 纯编译函数，不修改 SO。库编译器负责解析跨资源引用。
        /// 特殊模块可复用现有操作；新增操作必须同时增加 EffectKind 和对应执行器，继承本类不会自动产生新运行行为。
        /// </summary>
        public abstract EffectData Compile();
        public virtual BuffDefinition ReferencedBuff => null;
    }
}
