using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    public partial class CombatSimulationSystem
    {
        private readonly Dictionary<Entity,HashSet<Entity>> basicPierced=new Dictionary<Entity,HashSet<Entity>>();
        private int ExtraBasicTargets(Entity owner)=>EntityManager.HasComponent<AbilityTraits>(owner)?EntityManager.GetComponentData<AbilityTraits>(owner).ExtraBasicTargets:0;
        private void ReleaseSecondaryAttacks(Entity owner,Entity primary,in AbilityUnit hero,in AutoAttackData attack,in CombatSettings settings,
            ref AbilityLibrary library,float now,ref CombatPlayer player,EntityCommandBuffer commands)
        {
            if(attack.Delivery==AttackDelivery.Projectile)
            {
                int count=attack.SplitTargets+hero.ExtraTargets+ExtraBasicTargets(owner);
                if(count<=0) return;
                using var targets=abilities.Query(owner,player.Position,attack.Range,AreaShape.Circle,player.Facing,180,primary,count);
                foreach(var target in targets)
                {
                    var projectile=commands.Instantiate(settings.ProjectilePrefab);
                    commands.SetComponent(projectile,new CombatProjectile {Target=target,Damage=hero.Attack*attack.SplitDamageRatio,
                        Speed=attack.ProjectileSpeed,Lifetime=5,Piercing=hero.PierceBasics,RemainingRange=attack.Range,
                        Direction=math.normalizesafe(EntityManager.GetComponentData<LocalTransform>(target).Position-player.Position)});
                    commands.SetComponent(projectile,LocalTransform.FromPositionRotationScale(player.Position,quaternion.identity,.2f));
                }
            }
            else if(attack.CleaveRadius>0)
            {
                using var targets=abilities.Query(owner,player.Position,attack.CleaveRadius,AreaShape.Cone,player.Facing,
                    attack.CleaveHalfAngle,primary,attack.CleaveTargets+hero.ExtraTargets+ExtraBasicTargets(owner));
                foreach(var target in targets) HeroAttackHit(owner,target,hero.Attack*attack.CleaveDamageRatio,false,ref library,now,ref player);
            }
        }
        private void BasicSplash(Entity owner,float3 position,in AbilityUnit hero,ref AbilityLibrary library,float now,Entity primary)
        {
            if(hero.AttackSplashRadius<=0 || hero.AttackSplashRatio<=0) return;
            using var targets=abilities.Query(owner,position,hero.AttackSplashRadius,AreaShape.Circle,new float3(0,0,1),180,primary);
            foreach(var target in targets) abilities.Apply(owner,target,-1,new EffectData{Kind=EffectKind.Damage,Amount=hero.Attack*hero.AttackSplashRatio},ref library,now);
        }
        private void StepPiercingBasic(Entity owner,Entity bullet,ref CombatProjectile projectile,ref LocalTransform transform,
            ref AbilityLibrary library,float now,float dt,ref CombatPlayer player,EntityCommandBuffer destroy)
        {
            if(!basicPierced.TryGetValue(bullet,out var hits)) basicPierced[bullet]=hits=new HashSet<Entity>();
            float distance=math.min(projectile.RemainingRange,projectile.Speed*dt);
            float3 start=transform.Position,end=start+projectile.Direction*distance;
            using var targets=abilities.Query(owner,(start+end)*.5f,distance*.5f+.6f,AreaShape.Circle,projectile.Direction);
            foreach(var target in targets)
            {
                if(hits.Contains(target) || !SkillTargeting.FirstContact(start,end,EntityManager.GetComponentData<LocalTransform>(target).Position,.6f,out _)) continue;
                hits.Add(target);
                HeroAttackHit(owner,target,projectile.Damage,projectile.CountsAsBasicAttackHit,ref library,now,ref player);
                projectile.CountsAsBasicAttackHit=false;
            }
            transform.Position=end; projectile.RemainingRange-=distance;
            if(projectile.Lifetime<=0 || projectile.RemainingRange<=0) {basicPierced.Remove(bullet);destroy.DestroyEntity(bullet);}
            else {EntityManager.SetComponentData(bullet,transform);EntityManager.SetComponentData(bullet,projectile);}
        }
    }
}
