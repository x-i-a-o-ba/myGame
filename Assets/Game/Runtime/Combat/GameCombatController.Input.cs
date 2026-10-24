using Roguelike.Foundation.UI;
using Roguelike.Abilities;
using Unity.Mathematics;
using UnityEngine;

namespace Roguelike.Combat
{
    public sealed partial class GameCombatController
    {
        [Tooltip("世界方向移动、四个技能输入槽、属性和暂停键；修改后重开生效。")]
        public CombatInputProfile inputProfile;
        public bool Paused {get;private set;}
        public bool StatsOpen {get;private set;}
        public bool ExternalMovementInput {get;set;}
        private UIManager ExpeditionUI=>hud?hud.GetComponent<UIManager>():null;
        public string KeyLabel(int slot)=>AbilityKey(slot).ToString();
        private static readonly KeyCode[] DefaultAbilityKeys={KeyCode.Q,KeyCode.E,KeyCode.R,KeyCode.F};
        private KeyCode AbilityKey(int slot)=>inputProfile?inputProfile.abilities[slot]:DefaultAbilityKeys[slot];
        private void UpdateExpeditionInput()
        {
            if(!IsReady) return;
            StepExpedition();
            if(PendingFormSlot>=0)
            {
                if(Input.GetKeyDown(KeyCode.Alpha1)) ChooseForm(0);
                else if(Input.GetKeyDown(KeyCode.Alpha2)) ChooseForm(1);
            }
            if(Input.GetKeyDown(inputProfile?inputProfile.pause:KeyCode.Escape)) SetPaused(!Paused);
            if(!Running || Paused) { SetMovement(Vector2.zero); return; }
            if(!ExternalMovementInput) SetMovement(new Vector2(
                (Input.GetKey(inputProfile?inputProfile.right:KeyCode.D)?1:0)-(Input.GetKey(inputProfile?inputProfile.left:KeyCode.A)?1:0),
                (Input.GetKey(inputProfile?inputProfile.forward:KeyCode.W)?1:0)-(Input.GetKey(inputProfile?inputProfile.backward:KeyCode.S)?1:0)));
            bool learn=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
            for(int i=0;i<4;i++) if(Input.GetKeyDown(AbilityKey(i))) {if(learn) LearnSkill(i);else CastAbility(i);}
            if(Input.GetKeyDown(inputProfile?inputProfile.statistics:KeyCode.Tab))
            { StatsOpen=!StatsOpen; ExpeditionUI?.Visible("expedition.stats",StatsOpen); }
            if(!hud.BlocksPointer(Input.mousePosition)) cameraZoom=Mathf.Clamp(cameraZoom-Input.mouseScrollDelta.y*Mathf.Max(2,cameraZoom*.08f),12,160);
        }
        public void SetMovement(Vector2 direction)
        {
            if(!IsReady) return;
            var manager=session.World.EntityManager;
            manager.SetComponentData(session.Entity,new HeroSteering {Direction=Paused?float2.zero:(float2)Vector2.ClampMagnitude(direction,1),Paused=Paused});
        }
        public void SetPaused(bool paused)
        {
            if(!Running) return;
            Paused=paused; SetMovement(Vector2.zero);
            ExpeditionUI?.Visible("expedition.pause",paused);
        }
        public int PendingFormSlot
        {
            get
            {
                if(!IsReady) return -1;
                var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);
                for(int i=0;i<slots.Length;i++)
                    if(slots[i].FormChoice==0 && hero.skills[i].forms.Length==2 &&
                        slots[i].Level>=SkillLevels.MaxLevel(ref session.Library.Value.Skills[slots[i].Definition])) return i;
                return -1;
            }
        }
        public bool ChooseForm(int option)
        {
            int slot=PendingFormSlot;
            if(slot<0 || option<0 || option>1) return false;
            if(!session.TryApplyForm(slot,hero.skills[slot].forms[option],out string failure)) {ShowNotice(failure,2);return false;}
            var slots=session.World.EntityManager.GetBuffer<AbilitySlot>(session.Entity);var value=slots[slot];
            value.FormChoice=option+1;slots[slot]=value;nextHudUpdate=0;
            ShowNotice("技能形态："+hero.skills[slot].forms[option].displayName,3);return true;
        }
        private void PublishFormChoices()
        {
            int slot=PendingFormSlot;var ui=ExpeditionUI;if(!ui) return;
            ui.Visible("expedition.form",slot>=0);
            if(slot<0) return;
            ui.Text("expedition.form.title",hero.skills[slot].displayName+"已满级 · 选择形态");
            for(int i=0;i<2;i++)
            {
                var form=hero.skills[slot].forms[i];
                ui.Text("expedition.form."+i,(i+1)+"  "+form.displayName+"\n"+form.description);
            }
        }
    }
}
