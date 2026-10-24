using System;
using System.Collections.Generic;
using Unity.Entities;

namespace Roguelike.Abilities
{
    /// <summary>
    /// 会话内不可变计划仓库。初始库借用，替换成功后接管新库；宿主完成所有 Job 后才可调用或释放。
    /// Buff 目录在一局内冻结，因此 ActiveBuff 的索引仍可使用当前库，不需要追随旧计划。
    /// </summary>
    internal sealed class RecipeProgramStore : IDisposable
    {
        private sealed class Entry
        {
            public BlobAssetReference<AbilityLibrary> Library;
            public bool Owned;
        }
        private sealed class Owner
        {
            public int Current=1;
            public readonly Dictionary<int,Entry> Versions=new Dictionary<int,Entry>();
        }
        private readonly Dictionary<Entity,Owner> owners=new Dictionary<Entity,Owner>();
        private readonly HashSet<int> used=new HashSet<int>();
        private readonly List<int> retired=new List<int>();

        public int Ensure(Entity owner,BlobAssetReference<AbilityLibrary> library)
        {
            if(owners.TryGetValue(owner,out var state))
            {
                if(!state.Versions[state.Current].Library.Equals(library))
                    throw new InvalidOperationException("拥有者的技能库已在程序仓库外被替换；必须通过 ReplaceLibrary 提交。");
                return state.Current;
            }
            // 借用不能指向另一拥有者会被回收的替换版本，否则其旧弹体结束会使借用悬空。
            foreach(var existing in owners.Values)
                foreach(var entry in existing.Versions.Values)
                    if(entry.Owned && entry.Library.Equals(library))
                        throw new InvalidOperationException("不能借用其他拥有者的替换库；请独立编译其初始计划。");
            state=new Owner();
            state.Versions.Add(1,new Entry { Library=library,Owned=false });
            owners.Add(owner,state);
            return 1;
        }
        public BlobAssetReference<AbilityLibrary> Get(Entity owner,int version)
        {
            if(!owners.TryGetValue(owner,out var state) || !state.Versions.TryGetValue(version,out var entry))
                throw new InvalidOperationException($"技能程序版本不可用：拥有者 {owner}，版本 {version}。");
            return entry.Library;
        }
        public int Adopt(Entity owner,BlobAssetReference<AbilityLibrary> library)
        {
            var state=owners[owner];
            // 不允许一个 Blob 被仓库接管多次，或把原来的借用引用改为拥有。
            foreach(var item in owners.Values)
                foreach(var entry in item.Versions.Values)
                    if(entry.Library.Equals(library)) throw new InvalidOperationException("替换计划必须是独立的新编译库。");
            int version=checked(state.Current+1);
            state.Versions.Add(version,new Entry { Library=library,Owned=true });
            state.Current=version;
            return version;
        }
        public void Prune(Entity owner,DynamicBuffer<ScheduledSkillStage> scheduled,
            DynamicBuffer<SkillFlight> flights,DynamicBuffer<SkillCue> cues)
        {
            var state=owners[owner]; used.Clear(); used.Add(state.Current);
            for(int i=0;i<scheduled.Length;i++) used.Add(scheduled[i].ProgramVersion);
            for(int i=0;i<flights.Length;i++) used.Add(flights[i].ProgramVersion);
            for(int i=0;i<cues.Length;i++) used.Add(cues[i].ProgramVersion);
            retired.Clear();
            foreach(var pair in state.Versions)
                if(pair.Value.Owned && !used.Contains(pair.Key)) retired.Add(pair.Key);
            foreach(int version in retired)
            {
                var entry=state.Versions[version];
                if(entry.Owned && entry.Library.IsCreated) entry.Library.Dispose();
                state.Versions.Remove(version);
            }
        }
        public static bool Compatible(ref AbilityLibrary previous,ref AbilityLibrary next,out string failure)
        {
            failure=null;
            if(previous.Skills.Length!=next.Skills.Length) { failure="换装不能增减技能目录；请保持当前英雄及槽位顺序。"; return false; }
            for(int i=0;i<previous.Skills.Length;i++)
                if(previous.Skills[i].ConfigId<=0 || previous.Skills[i].ConfigId!=next.Skills[i].ConfigId)
                { failure=$"技能目录位置 {i} 的数字 ID 改变或未登记，无法保留冷却。"; return false; }
            if(previous.Buffs.Length!=next.Buffs.Length) { failure="局内换装不能增减 Buff 目录。"; return false; }
            for(int i=0;i<previous.Buffs.Length;i++)
            {
                ref var a=ref previous.Buffs[i]; ref var b=ref next.Buffs[i];
                if(a.ConfigId<=0 || a.ConfigId!=b.ConfigId || a.Duration!=b.Duration || a.Interval!=b.Interval || a.PeriodicDamage!=b.PeriodicDamage ||
                   a.SpeedPercent!=b.SpeedPercent || a.AttackFlat!=b.AttackFlat || a.Regen!=b.Regen || a.Stacking!=b.Stacking ||
                   a.MaxStacks!=b.MaxStacks || a.Dispellable!=b.Dispellable || a.Controls!=b.Controls || a.SourcePolicy!=b.SourcePolicy ||
                   a.PeriodicAttackRatio!=b.PeriodicAttackRatio || a.DamageReduction!=b.DamageReduction || a.RegenMaxHealth!=b.RegenMaxHealth ||
                   a.DamageTaken!=b.DamageTaken || a.Reflect!=b.Reflect || a.ExtraTargets!=b.ExtraTargets || a.ControlImmune!=b.ControlImmune ||
                   a.PierceBasics!=b.PierceBasics || a.AttackSplashRadius!=b.AttackSplashRadius || a.AttackSplashRatio!=b.AttackSplashRatio ||
                   a.Modifiers.Length!=b.Modifiers.Length)
                { failure=$"Buff 目录位置 {i} 的定义改变；已有状态必须保留旧语义，请重开会话。"; return false; }
                for(int m=0;m<a.Modifiers.Length;m++)
                    if(a.Modifiers[m].Attribute!=b.Modifiers[m].Attribute || a.Modifiers[m].Operation!=b.Modifiers[m].Operation || a.Modifiers[m].Value!=b.Modifiers[m].Value)
                    { failure=$"Buff 目录位置 {i} 的属性贡献改变，请重开会话。"; return false; }
            }
            return true;
        }
        public void Dispose()
        {
            foreach(var state in owners.Values)
                foreach(var entry in state.Versions.Values)
                    if(entry.Owned && entry.Library.IsCreated) entry.Library.Dispose();
            owners.Clear();
        }
        public void ReleaseOwner(Entity owner)
        {
            if(!owners.TryGetValue(owner,out var state)) return;
            foreach(var entry in state.Versions.Values)
                if(entry.Owned && entry.Library.IsCreated) entry.Library.Dispose();
            owners.Remove(owner);
        }
    }

#if UNITY_EDITOR
    /// <summary>纯编译数据兼容检查入口，不创建 World、不执行 Step，也不进入游戏模式。</summary>
    public static class RecipeProgramStoreValidation
    {
        public static void Run()
        {
            int assertions=0;
            var baseline=Build(0);
            try
            {
                for(int scenario=0;scenario<=5;scenario++)
                {
                    var candidate=Build(scenario);
                    try
                    {
                        bool compatible=RecipeProgramStore.Compatible(ref baseline.Value,ref candidate.Value,out string reason);
                        if(compatible!=(scenario==0) || (scenario!=0 && string.IsNullOrWhiteSpace(reason)))
                            throw new InvalidOperationException($"程序目录兼容检查失败：场景 {scenario}，{reason}");
                        assertions++;
                    }
                    finally { candidate.Dispose(); }
                }
                // 第一帧注册及接管仅检验仓库所有权，仍不创建 ECS World。
                var owner=new Entity {Index=1,Version=1};
                var store=new RecipeProgramStore();
                var replacement=Build(0); bool adopted=false;
                try
                {
                    if(store.Ensure(owner,baseline)!=1) throw new InvalidOperationException("初始借用版本编号错误。");
                    if(store.Adopt(owner,replacement)!=2) throw new InvalidOperationException("第一次换装版本编号错误。");
                    adopted=true;
                    if(!store.Get(owner,1).Equals(baseline) || !store.Get(owner,2).Equals(replacement))
                        throw new InvalidOperationException("换装未保留初始和当前版本。");
                    assertions++;
                    bool duplicateRejected=false;
                    try { store.Adopt(owner,replacement); } catch(InvalidOperationException) { duplicateRejected=true; }
                    if(!duplicateRejected) throw new InvalidOperationException("同一替换库重复接管未被拒绝。");
                    assertions++;
                    bool foreignBorrowRejected=false;
                    try { store.Ensure(new Entity {Index=2,Version=1},replacement); }
                    catch(InvalidOperationException) { foreignBorrowRejected=true; }
                    if(!foreignBorrowRejected) throw new InvalidOperationException("跨拥有者借用可回收库未被拒绝。");
                    assertions++;
                    store.ReleaseOwner(owner);
                    // 若错误释放借用库，访问其配置会由 Entities 安全检查报告。
                    if(baseline.Value.Skills[0].ConfigId!=1000) throw new InvalidOperationException("借用库被错误改动。");
                    assertions++;
                }
                finally { store.Dispose(); if(!adopted) replacement.Dispose(); }
            }
            finally { baseline.Dispose(); }
            UnityEngine.Debug.Log($"[RecipeProgramStoreValidation] {assertions} 项纯数据兼容与所有权检查通过；未创建 World、未运行游戏。");
        }
        private static BlobAssetReference<AbilityLibrary> Build(int scenario)
        {
            using var builder=new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var library=ref builder.ConstructRoot<AbilityLibrary>();
            var skills=builder.Allocate(ref library.Skills,2);
            skills[0].ConfigId=scenario==5?1001:1000; skills[1].ConfigId=scenario==5?1000:1001;
            var buffs=builder.Allocate(ref library.Buffs,2);
            buffs[0].ConfigId=scenario==2?5099:scenario==3?5001:5000;
            buffs[1].ConfigId=scenario==3?5000:5001;
            buffs[0].Duration=scenario==1?3:2;
            var modifiers=builder.Allocate(ref buffs[0].Modifiers,1);
            modifiers[0].Value=scenario==4?.5f:.25f;
            return builder.CreateBlobAssetReference<AbilityLibrary>(Unity.Collections.Allocator.Persistent);
        }
    }
#endif
}
