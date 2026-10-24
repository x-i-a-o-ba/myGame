using System.Collections.Generic;
using Roguelike.Abilities;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike.Combat
{
    /// <summary>有界表现消费者。预警和反馈合并为一个动态网格，不创建死亡实体或每个预警对象；绝不参与命中。</summary>
    public sealed class CombatFeedbackView:System.IDisposable
    {
        struct Number {public Entity Target;public Vector3 Position;public float Damage,Until;public string Text;public bool Elite,Dirty;}
        struct Link {public AbilityLink Data;public float Until;}
        readonly List<Link> links=new List<Link>(64);
        struct Fragment {public Vector3 Position;public float Began;}
        struct Cue {public MonsterAbilities.Visual Visual;public float Until;}
        readonly List<Number> numbers=new List<Number>(32);
        readonly List<Fragment> fragments=new List<Fragment>(128);
        readonly List<Cue> cues=new List<Cue>(64);
        readonly List<Vector3> vertices=new List<Vector3>(16000);
        readonly List<Color32> colors=new List<Color32>(16000);
        readonly List<int> indices=new List<int>(24000);
        readonly Mesh mesh;readonly Material material;readonly Camera camera;
        GUIStyle numberStyle,streakStyle;
        int kills,castSerial;float shakeUntil,flashUntil;bool tide;
        public CombatFeedbackView(Material material,Camera camera)
        {this.material=material;this.camera=camera;mesh=new Mesh{name="有界战斗反馈网格"};mesh.indexFormat=IndexFormat.UInt32;mesh.MarkDynamic();}
        public void Step(GameCombatController game,GameCombatSession session)
        {
            var manager=session.World.EntityManager;var system=session.World.GetExistingSystemManaged<CombatSimulationSystem>();
            float now=Time.unscaledTime;var feedback=manager.GetBuffer<AbilityFeedback>(session.Entity);
            foreach(var hit in feedback)
            {
                if(hit.Killed && hit.Damage==0 && fragments.Count<128) fragments.Add(new Fragment{Position=hit.Position,Began=now});
                if(hit.Damage<=0 || hit.Origin<0 && hit.TargetKind==0) continue;
                int existing=-1;
                for(int i=0;i<numbers.Count;i++)
                    if(numbers[i].Target==hit.Target && numbers[i].Until-now>.35f) {existing=i;break;}
                if(existing>=0)
                {var n=numbers[existing];n.Damage+=hit.Damage;n.Dirty=true;numbers[existing]=n;}
                else if(numbers.Count<32) numbers.Add(new Number{Target=hit.Target,Position=hit.Position,Damage=hit.Damage,Dirty=true,Until=now+.8f,Elite=hit.TargetKind>0});
            }
            feedback.Clear();
            var linkEvents=manager.GetBuffer<AbilityLink>(session.Entity);
            foreach(var link in linkEvents) if(links.Count<64) links.Add(new Link{Data=link,Until=now+.18f});linkEvents.Clear();
            for(int i=links.Count-1;i>=0;i--) if(now>=links[i].Until) links.RemoveAt(i);
            var events=manager.GetBuffer<SkillCue>(session.Entity);
            foreach(var e in events)
            {
                var library=system.GetLibrary(session.Entity,e.ProgramVersion);ref var stage=ref library.Value.Skills[e.Skill].Stages[e.Stage];
                if(stage.Scope!=TargetScope.Area || cues.Count>=64) continue;
                var c=library.Value.Skills[e.Skill].IndicatorColor;
                cues.Add(new Cue{Until=now+Mathf.Max(.25f,stage.RepeatInterval),Visual=new MonsterAbilities.Visual{Center=e.Position,Direction=e.Direction,
                    Shape=stage.Shape,Radius=e.Radius,Range=e.Range,HalfAngle=stage.HalfAngleDegrees,InnerRadius=e.InnerRadius,Color=c}});
            }
            events.Clear();
            var runtime=manager.GetComponentData<AbilityRuntime>(session.Entity);
            if(runtime.CastSerial!=castSerial) {castSerial=runtime.CastSerial;if(runtime.CastSlot==3) shakeUntil=now+.22f;}
            if(game.Director.TideStarted && !tide) {tide=true;shakeUntil=now+.3f;}
            int current=game.State.Kills;
            if(current/100>kills/100) flashUntil=now+.45f;kills=current;
            if(now<shakeUntil && !game.Paused) camera.transform.position+=new Vector3(Mathf.Sin(now*82),Mathf.Cos(now*69),0)*.12f*((shakeUntil-now)/.3f);
            for(int i=numbers.Count-1;i>=0;i--)
            {
                var n=numbers[i];
                if(now>=n.Until) {numbers.RemoveAt(i);continue;}
                if(n.Dirty) {n.Text=Mathf.RoundToInt(n.Damage).ToString();n.Dirty=false;numbers[i]=n;}
            }
            for(int i=fragments.Count-1;i>=0;i--) if(now>=fragments[i].Began+.55f) fragments.RemoveAt(i);
            for(int i=cues.Count-1;i>=0;i--) if(now>=cues[i].Until) cues.RemoveAt(i);
            vertices.Clear();colors.Clear();indices.Clear();
            foreach(var v in system.Monsters.Visuals) Shape(v);
            foreach(var c in cues) Shape(c.Visual);
            foreach(var link in links)
            {
                Vector3 a=link.Data.From,b=link.Data.To,mid=(a+b)*.5f+Vector3.up*.35f+Vector3.right*.2f;
                Edge(a,mid,new Color(.5f,.8f,1,.95f));Edge(mid,b,new Color(.5f,.8f,1,.95f));
            }
            foreach(var f in fragments)
            {
                float age=(now-f.Began)/.55f;
                for(int n=0;n<3;n++)
                {
                    float a=n*Mathf.PI*2/3;Vector3 p=f.Position+new Vector3(Mathf.Cos(a),1-age,Mathf.Sin(a))*age;
                    float size=(1-age)*.2f;Triangle(p+Vector3.up*size,p+Vector3.right*size,p-Vector3.right*size,new Color(.8f,.58f,.3f,1-age));
                }
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            if(material && indices.Count>0) Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }
        void Shape(MonsterAbilities.Visual v)
        {
            var color=new Color(v.Color.x,v.Color.y,v.Color.z,v.Warning?.35f:.22f);
            Vector3 center=v.Center;center.y=.09f;Vector3 direction=v.Direction;direction.y=0;
            if(direction.sqrMagnitude<.001f) direction=Vector3.forward;else direction.Normalize();
            if(v.Shape==AreaShape.Line)
            {
                Vector3 side=Vector3.Cross(Vector3.up,direction)*v.Radius,end=center+direction*v.Range;
                Quad(center-side,center+side,end+side,end-side,color);
                Edge(center-side,end-side,new Color(color.r,color.g,color.b,.85f));Edge(center+side,end+side,new Color(color.r,color.g,color.b,.85f));return;
            }
            float extent=v.Shape==AreaShape.Cone?v.HalfAngle:180;
            float radius=v.Shape==AreaShape.Cone?v.Range:v.Radius,inner=v.Shape==AreaShape.Ring?v.InnerRadius:0;
            int segments=Mathf.Max(8,Mathf.CeilToInt(32*extent/180));
            float heading=Mathf.Atan2(direction.z,direction.x);
            for(int n=0;n<segments;n++)
            {
                float a=heading+Mathf.Deg2Rad*(-extent+n*2*extent/segments),b=heading+Mathf.Deg2Rad*(-extent+(n+1)*2*extent/segments);
                Vector3 da=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),db=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Quad(center+da*inner,center+da*radius,center+db*radius,center+db*inner,color);
                Edge(center+da*radius,center+db*radius,new Color(color.r,color.g,color.b,.9f));
                if(inner>0) Edge(center+da*inner,center+db*inner,new Color(color.r,color.g,color.b,.8f));
                if(v.Warning) Edge(center+da*radius*v.Progress,center+db*radius*v.Progress,new Color(1,.85f,.45f,.85f));
            }
        }
        void Edge(Vector3 a,Vector3 b,Color color)
        {Vector3 side=Vector3.Cross(Vector3.up,(b-a).normalized);if(side.sqrMagnitude<.01f) side=Vector3.right;side=side.normalized*.055f;Quad(a-side,a+side,b+side,b-side,color);}
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color)
        {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(n);indices.Add(n+1);indices.Add(n+2);}
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color) {Triangle(a,b,c,color);Triangle(a,c,d,color);}
        public void DrawGUI()
        {
            if(!camera) return;
            if(numberStyle==null) {numberStyle=new GUIStyle(GUI.skin.label){fontSize=19,fontStyle=FontStyle.Bold,alignment=TextAnchor.MiddleCenter};streakStyle=new GUIStyle(numberStyle){fontSize=36};}
            foreach(var n in numbers)
            {
                Vector3 p=camera.WorldToScreenPoint(n.Position+Vector3.up*(1.2f+(Time.unscaledTime-n.Until+.8f)*1.5f));if(p.z<0) continue;
                numberStyle.normal.textColor=n.Elite?new Color(1,.75f,.3f):Color.white;
                GUI.Label(new Rect(p.x-50,Screen.height-p.y-18,100,36),n.Text,numberStyle);
            }
            if(Time.unscaledTime<flashUntil)
            {
                GUI.color=new Color(1,.8f,.3f,(flashUntil-Time.unscaledTime)*.2f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
                streakStyle.normal.textColor=new Color(1,.8f,.3f);GUI.Label(new Rect(Screen.width/2-150,125,300,70),(kills/100*100)+" 击杀",streakStyle);
            }
        }
        public void Dispose() {Object.Destroy(mesh);numbers.Clear();fragments.Clear();cues.Clear();}
    }
}
