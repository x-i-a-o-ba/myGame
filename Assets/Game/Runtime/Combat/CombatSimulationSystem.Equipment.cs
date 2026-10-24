using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Roguelike.Combat
{
    public partial class CombatSimulationSystem
    {
        private readonly List<Entity> procTargets=new List<Entity>(32);
        private void EquipmentTriggerEvent(Entity owner,Entity primary,EquipmentTrigger trigger,int serial,ref AbilityLibrary library,float now)
        {
            var manager=EntityManager;if(!manager.HasComponent<HeroEquipment>(owner)) return;
            var equipment=manager.GetComponentData<HeroEquipment>(owner);
            if(!equipment.Library.IsCreated) return;
            var inventory=manager.GetBuffer<EquipmentSlot>(owner);
            float3 start=manager.GetComponentData<LocalTransform>(primary!=Entity.Null && manager.Exists(primary)?primary:owner).Position;
            for(int slot=0;slot<inventory.Length;slot++)
            {
                int item=inventory[slot].Item;if(item<0) continue;
                ref var procs=ref equipment.Library.Value.Items[item].Procs;
                for(int p=0;p<procs.Length;p++)
                {
                    ref var proc=ref procs[p];if(proc.Trigger!=trigger) continue;
                    uint roll=math.hash(new uint3((uint)serial,(uint)(slot+1),(uint)(p+item*17+71)));
                    if((roll&0xffffff)/16777216f>=proc.Chance) continue;
                    procTargets.Clear();
                    if(proc.Chain)
                    {
                        float3 center=start;
                        if(proc.IncludePrimary && Alive(primary)) procTargets.Add(primary);
                        while(procTargets.Count<proc.MaxTargets)
                        {
                            using var choices=abilities.Query(owner,center,proc.Radius,AreaShape.Circle,new float3(0,0,1));
                            Entity next=Entity.Null;
                            foreach(var entity in choices)
                                if(!procTargets.Contains(entity) && (proc.IncludePrimary || entity!=primary)) {next=entity;break;}
                            if(next==Entity.Null) break;
                            procTargets.Add(next);center=manager.GetComponentData<LocalTransform>(next).Position;
                        }
                    }
                    else
                    {
                        using var choices=abilities.Query(owner,start,proc.Radius,AreaShape.Circle,new float3(0,0,1),180,
                            proc.IncludePrimary?Entity.Null:primary,proc.MaxTargets);
                        foreach(var target in choices) procTargets.Add(target);
                    }
                    float3 last=start;
                    foreach(var target in procTargets)
                    {
                        float3 point=manager.GetComponentData<LocalTransform>(target).Position;
                        if((proc.Chain || proc.Trigger==EquipmentTrigger.AbilityCast) && manager.HasBuffer<AbilityLink>(owner))
                        {
                            var links=manager.GetBuffer<AbilityLink>(owner);
                            if(links.Length<64) links.Add(new AbilityLink{From=proc.Chain?last:point+new float3(0,5,0),To=point,Color=new float4(.5f,.8f,1,1)});
                        }
                        last=point;
                        for(int effect=0;effect<proc.Effects.Length;effect++)
                            abilities.Apply(owner,target,EquipmentLogic.OnHitOrigin+item,proc.Effects[effect],ref library,now);
                    }
                }
            }
        }
        private void ProcessCastEvents(Entity owner,ref AbilityLibrary library,float now)
        {
            if(!EntityManager.HasBuffer<AbilityCastEvent>(owner)) return;
            var events=EntityManager.GetBuffer<AbilityCastEvent>(owner);
            for(int i=0;i<events.Length;i++)
                if(!events[i].Automatic) EquipmentTriggerEvent(owner,Entity.Null,EquipmentTrigger.AbilityCast,events[i].Serial,ref library,now);
            events.Clear();
        }
    }
}
