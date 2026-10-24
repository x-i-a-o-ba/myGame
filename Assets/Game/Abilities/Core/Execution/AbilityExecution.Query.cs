using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Abilities
{
    public sealed partial class AbilityExecution
    {
        private float3 DensePoint(Entity owner,float3 center,float searchRadius,float damageRadius)
        {
            using var sample=Query(owner,center,searchRadius,AreaShape.Circle,new float3(0,0,1),limit:32);
            float3 best=center;int maximum=0;
            foreach(var candidate in sample)
            {
                var point=manager.GetComponentData<LocalTransform>(candidate).Position;
                Collect(point,damageRadius,false);int count=0;
                for(int i=0;i<candidates.Length;i++)
                {
                    var other=candidates[i];
                    if(IsAlive(other) && Matches(owner,other,TargetRelation.Enemies) &&
                        math.distancesq(point.xz,manager.GetComponentData<LocalTransform>(other).Position.xz)<=damageRadius*damageRadius) count++;
                }
                if(count>maximum) {maximum=count;best=point;}
            }
            return best;
        }
        private struct QueryHit : System.IComparable<QueryHit>
        {
            public Entity Entity; public float Distance;
            public int CompareTo(QueryHit other) {int n=Distance.CompareTo(other.Distance);return n!=0?n:Entity.Index.CompareTo(other.Entity.Index);}
        }
        /// <summary>普攻、装备、怪物宿主复用的空间查询。结果按距离及实体编号稳定排序，调用方释放。</summary>
        public NativeArray<Entity> Query(Entity owner,float3 center,float radius,AreaShape shape,float3 direction,
            float halfAngle=180,Entity exclude=default,int limit=int.MaxValue,TargetRelation relation=TargetRelation.Enemies)
            =>QueryTeam(manager.Exists(owner)?manager.GetComponentData<AbilityUnit>(owner).Team:0,center,radius,shape,direction,halfAngle,exclude,limit,relation);
        /// <summary>来源已经死亡的地面区域使用施法时队伍快照；几何与存活过滤仍由核心执行。</summary>
        public NativeArray<Entity> QueryTeam(int team,float3 center,float radius,AreaShape shape,float3 direction,
            float halfAngle=180,Entity exclude=default,int limit=int.MaxValue,TargetRelation relation=TargetRelation.Enemies,float range=0,float innerRadius=0)
        {
            Collect(center,math.max(radius,range),false);
            using var found=new NativeList<QueryHit>(Allocator.Temp);
            for(int i=0;i<candidates.Length;i++)
            {
                Entity entity=candidates[i];
                if(entity==exclude || !IsAlive(entity)) continue;
                int other=manager.GetComponentData<AbilityUnit>(entity).Team;
                if(relation==TargetRelation.Enemies && team==other || relation==TargetRelation.Allies && team!=other) continue;
                var position=manager.GetComponentData<LocalTransform>(entity).Position;
                if(SkillTargeting.Contains(shape,radius,range>0?range:radius,halfAngle,innerRadius,center,direction,position))
                    found.Add(new QueryHit{Entity=entity,Distance=math.distancesq(center.xz,position.xz)});
            }
            found.AsArray().Sort();
            var result=new NativeArray<Entity>(math.min(math.max(0,limit),found.Length),Allocator.Temp);
            for(int i=0;i<result.Length;i++) result[i]=found[i].Entity;
            return result;
        }
    }
}
