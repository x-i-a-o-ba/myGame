using System;
using Roguelike.Abilities;
using Roguelike.Combat;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Roguelike.Editor
{
    public static partial class CombatValidation
    {
        static BlobAssetReference<MonsterAbilityLibrary> MonsterLibrary(params MonsterAbilityData[] specs)
        {
            using var builder=new BlobBuilder(Allocator.Temp);ref var root=ref builder.ConstructRoot<MonsterAbilityLibrary>();
            var values=builder.Allocate(ref root.Abilities,specs.Length);for(int i=0;i<specs.Length;i++) values[i]=specs[i];
            return builder.CreateBlobAssetReference<MonsterAbilityLibrary>(Allocator.Persistent);
        }
        static void ValidateMonsters(SkillCatalog catalog)
        {
            foreach(var guid in AssetDatabase.FindAssets("t:MonsterAbilityDefinition"))
            {AssetDatabase.LoadAssetAtPath<MonsterAbilityDefinition>(AssetDatabase.GUIDToAssetPath(guid)).Compile(catalog);Check(true,"怪物模块可编译");}
            using var f=new Fixture(catalog);
            var manager=f.Manager;var hero=f.Owner(new[]{0});
            manager.AddComponentData(hero,new CombatSettings{MaxAlive=12,ArenaHalfSize=100,EnemyHealth=10,EnemyAttackDamage=10,EnemySpeed=2,HealthMultiplier=1});
            manager.AddComponentData(hero,new CombatDifficultySettings{HealthGrowth=1,DamageGrowth=1,MaxSpeed=5});
            manager.AddComponentData(hero,new CombatDirectorState{Stage=3,BossSpawned=true});
            manager.AddBuffer<CombatEnemyKind>(hero);
            using var query=manager.CreateEntityQuery(typeof(CombatEnemy),typeof(AbilityUnit),typeof(LocalTransform));
            var host=new MonsterAbilities(manager,f.Execution);
            Entity Enemy(float3 point,BlobAssetReference<MonsterAbilityLibrary> library)
            {var e=f.Unit(point,1);manager.AddComponentData(e,new CombatEnemy{Tier=EnemyTier.Normal,Stage=3,ConfigId=3001});manager.AddComponentData(e,new MonsterBrain{Library=library});return e;}
            void Reset()
            {host.Clear();manager.DestroyEntity(query);var unit=f.State(hero);unit.Health=unit.MaxHealth=100;manager.SetComponentData(hero,unit);manager.SetComponentData(hero,LocalTransform.FromPosition(float3.zero));}
            void Step(float now,float delta=.1f) {f.Execution.RebuildIndex();host.Step(hero,query,now,delta,ref f.Library.Value);}
            var area=new MonsterAbilityData{Template=MonsterAbilityTemplate.TelegraphArea,Shape=AreaShape.Circle,Radius=3,Range=3,TriggerRange=10,
                Cooldown=10,Warning=.5f,DamageRatio=2,RepeatCount=1,MinimumStage=1};
            using(var blob=MonsterLibrary(area))
            {
                Enemy(new float3(0,0,2),blob);Step(0);Check(host.ActiveWarnings==1 && f.State(hero).Health==100,"预警先展示，不提前伤害");
                Step(.49f);Check(f.State(hero).Health==100,"预警时间边界前不结算");Step(.51f);
                Check(f.State(hero).Health==80 && host.CastCount==1,"预警结束复用共享伤害结算");
                Reset();var e=Enemy(new float3(0,0,2),blob);Step(0);manager.DestroyEntity(e);Step(.6f);
                Check(f.State(hero).Health==100 && host.ActiveWarnings==0,"施法者死亡取消未释放技能");
                Reset();for(int i=0;i<80;i++) Enemy(new float3(0,0,2),blob);Step(0);
                Check(host.ActiveWarnings==48 && host.DeferredCount==32,"普通预警硬预算并给高优先级保留十六格");Reset();
            }
            var zone=area;zone.Template=MonsterAbilityTemplate.OnDeath;zone.DeathAction=MonsterDeathAction.GroundZone;zone.Duration=3;zone.Interval=1;zone.Warning=0;
            using(var blob=MonsterLibrary(zone))
            {
                var e=Enemy(new float3(0,0,2),blob);Step(0);host.OnDeath(e,0,ref f.Library.Value);manager.DestroyEntity(e);Step(.1f);Step(1.2f);
                Check(f.State(hero).Health==60 && host.ActiveZones==1,"死亡区域保存来源攻击和队伍，跨帧继续结算");Step(3.1f);Check(host.ActiveZones==0,"持续区域到期释放");Reset();
            }
            var aura=area;aura.Template=MonsterAbilityTemplate.Aura;aura.HealMaxHealth=.03f;aura.Duration=2;aura.Interval=1;aura.Warning=0;
            using(var blob=MonsterLibrary(aura))
            {
                var e=Enemy(new float3(0,0,2),blob);var unit=f.State(e);unit.Health=50;manager.SetComponentData(e,unit);Step(0);
                Check(Mathf.Approximately(f.State(e).Health,53) && f.State(hero).Health==100,"治疗光环只回复友方最大生命比例");Reset();
            }
            var dash=area;dash.Template=MonsterAbilityTemplate.Dash;dash.Range=4;dash.Radius=.5f;dash.Speed=10;dash.Warning=0;
            using(var blob=MonsterLibrary(dash))
            {
                var e=Enemy(new float3(0,0,4),blob);Step(0,.2f);Step(.2f,.2f);Step(.4f,.2f);
                Check(f.State(hero).Health==80 && manager.GetComponentData<LocalTransform>(e).Position.z<.1f,"冲锋沿锁定方向移动且只碰撞一次");Reset();
            }
            var shot=area;shot.Template=MonsterAbilityTemplate.ProjectileBarrage;shot.ProjectileCount=1;shot.Range=10;shot.Radius=1;shot.Speed=10;shot.Warning=0;
            using(var blob=MonsterLibrary(shot))
            {
                Enemy(new float3(0,0,4),blob);Step(0,.2f);Step(.2f,.2f);Step(.4f,.2f);
                Check(f.State(hero).Health==80 && host.ActiveProjectiles==0,"直线怪物弹体首碰一次后回收");Reset();
            }
            using(var empty=MonsterLibrary())
            {
                var e=Enemy(new float3(0,0,2),empty);var b=manager.GetComponentData<MonsterBrain>(e);
                b.Affixes=MonsterAffix.Rage|MonsterAffix.Shield|MonsterAffix.Fortified;manager.SetComponentData(e,b);
                var unit=f.State(e);unit.Health=40;manager.SetComponentData(e,unit);Step(0);
                unit=f.State(e);Check(unit.Shield==20 && unit.BaseSpeed==15 && unit.AttackSpeedBonus==.5f,"狂怒与护盾词缀写入单位权威状态");
                Check(unit.DamageReduction==.3f && unit.SlowImmune && unit.DisplacementImmune,"坚韧减伤与移动免疫生效");
                Step(.2f);Check(f.State(e).BaseSpeed==15,"半血狂怒不会重复叠乘");Reset();
                var prefab=Enemy(new float3(50,0,50),empty);manager.AddComponent<Prefab>(prefab);
                manager.GetBuffer<CombatEnemyKind>(hero).Add(new CombatEnemyKind{ConfigId=3001,Prefab=prefab,Tiers=EnemyTierMask.Normal,Weight=1,Health=1,Damage=1,Speed=1,Scale=1});
                var summon=area;summon.Template=MonsterAbilityTemplate.Summon;summon.SummonCount=60;summon.SummonId=3001;summon.Warning=0;
                using(var blob=MonsterLibrary(summon))
                {Enemy(new float3(0,0,2),blob);Step(0);Check(query.CalculateEntityCount()==12,"召唤潮服从全局存活上限");Reset();}
                var split=area;split.Template=MonsterAbilityTemplate.OnDeath;split.DeathAction=MonsterDeathAction.Split;split.SummonCount=2;split.SummonId=3001;split.SummonHealthRatio=.2f;split.Warning=0;
                using(var blob=MonsterLibrary(split))
                {
                    var source=Enemy(new float3(0,0,2),blob);Step(0);host.OnDeath(source,0,ref f.Library.Value);manager.DestroyEntity(source);
                    using var children=query.ToEntityArray(Allocator.Temp);
                    Check(children.Length==2 && f.State(children[0]).MaxHealth==20 && manager.GetComponentData<MonsterBrain>(children[0]).Summoned,"死亡分裂保留生命比例并阻止递归分裂");
                    Reset();
                }
                manager.DestroyEntity(prefab);
            }
        }
    }
}
