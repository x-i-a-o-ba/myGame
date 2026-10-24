using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Mathematics;

namespace Roguelike.Flow
{
    /// <summary>一次营地的随机货架和遗物候选。金币与装备写入仍交给共享装备规则。</summary>
    public sealed class CampState
    {
        public readonly int[] Stock={-1,-1,-1,-1,-1};
        public readonly List<RelicDefinition> RelicOptions=new List<RelicDefinition>(4);
        public bool RelicChosen,RouteChosen;
        public int Refreshes;
        public int RefreshPrice=>50*(Refreshes+1);
        private Random random;
        public CampState(RunState run,ExpeditionCatalog catalog,EquipmentDefinition[] items)
        {
            random=Random.CreateFromIndex(run.Seed++);
            var choices=new List<RelicDefinition>();
            foreach(var relic in catalog.relics) if(relic && !run.Relics.Contains(relic)) choices.Add(relic);
            int count=run.Stage==2 && run.DangerousRoute?4:3;
            while(RelicOptions.Count<count && choices.Count>0)
            {int index=random.NextInt(choices.Count);RelicOptions.Add(choices[index]);choices.RemoveAt(index);}
            RouteChosen=run.Stage!=1;
            Roll(run,items);
        }
        public void Roll(RunState run,EquipmentDefinition[] items)
        {
            var eligible=new List<int>();
            for(int slot=0;slot<Stock.Length;slot++)
            {
                float roll=random.NextFloat();
                var quality=run.Stage==1?(roll<.6f?EquipmentQuality.Common:roll<.95f?EquipmentQuality.Rare:EquipmentQuality.Legendary):
                    roll<.3f?EquipmentQuality.Common:roll<.75f?EquipmentQuality.Rare:EquipmentQuality.Legendary;
                eligible.Clear();
                for(int i=0;i<items.Length;i++)
                {
                    if(!items[i] || items[i].quality!=quality) continue;
                    if(items[i].unique && System.Array.IndexOf(run.EquipmentIds,items[i].configId)>=0) continue;
                    bool duplicate=false;
                    for(int j=0;j<slot;j++) if(items[i].unique && Stock[j]==i) duplicate=true;
                    if(!duplicate) eligible.Add(i);
                }
                if(eligible.Count==0)
                    for(int i=0;i<items.Length;i++) if(items[i] && !items[i].unique) eligible.Add(i);
                Stock[slot]=eligible.Count==0?-1:eligible[random.NextInt(eligible.Count)];
            }
        }
        public bool CanDepart=>RelicChosen && RouteChosen;
    }
}
