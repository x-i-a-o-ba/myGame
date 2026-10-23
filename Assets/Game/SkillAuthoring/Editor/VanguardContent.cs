using System;
using System.IO;
using System.Linq;
using Roguelike.Abilities;
using Roguelike.Combat;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Roguelike.SkillAuthoring.Editor
{
    /// <summary>首版内容的可重复生成入口；已有资源在原路径更新，保留 GUID。</summary>
    public static class VanguardContent
    {
        const string Root="Assets/GameData/";
        static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(Root+path);
        static T Asset<T>(string path) where T:ScriptableObject
        {
            var value=Load<T>(path); if(value) return value;
            Directory.CreateDirectory(Path.GetDirectoryName(Root+path)); AssetDatabase.Refresh();
            value=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value,Root+path); return value;
        }
        static void Dirty(UnityEngine.Object o)=>EditorUtility.SetDirty(o);
        static RecipeEffectUse Use(int id,float amount=-1)=>new RecipeEffectUse{effectId=id,overrideAmount=amount>=0,amount=Mathf.Max(0,amount)};
        static RecipeNode Node(int query,params RecipeEffectUse[] effects)=>new RecipeNode{deliveryModuleId=10000,queryModuleId=query,effects=effects};
        static SkillDefinition Skill(int id,string title,string key,AimMode aim,float range,float cd,float mana,string description,params RecipeNode[] nodes)
        {
            var s=Asset<SkillDefinition>("技能/"+id+"_"+title+".asset");
            s.configId=id;s.stableId=key;s.displayName=title;s.description=description;s.aim=aim;s.castRange=range;s.cooldown=cd;s.manaCost=mana;
            s.modules=Load<SkillModuleCatalog>("目录/组合模块目录.asset");s.recipe=new SkillRecipe{enabled=true,recipeId=id,authorNamespace="official",nodes=nodes};
            for(int i=0;i<nodes.Length;i++) nodes[i].nodeId=i+1;
            s.growth=new SkillLevelGrowth{track=SkillLevelTrack.Basic};s.indicatorColor=new Color(1,.68f,.2f);
            s.icon=Icon(title,id-1008);Dirty(s);return s;
        }
        // 小尺寸程序化徽记：统一金属底、不同轮廓；不复用其他英雄的技能图标。
        static Texture2D Icon(string name,int kind)
        {
            string path=Root+"表现/磐锋/"+name+".png";Directory.CreateDirectory(Path.GetDirectoryName(path));
            var t=new Texture2D(96,96,TextureFormat.RGBA32,false);
            for(int y=0;y<96;y++)for(int x=0;x<96;x++)
            {
                float dx=x-47.5f,dy=y-47.5f,r=Mathf.Sqrt(dx*dx+dy*dy);
                bool mark=kind==0?(Mathf.Abs(dx-dy*.4f)<6 && Math.Abs(dy)<31 || Math.Abs(dy+16)<4 && Math.Abs(dx)<25):
                    kind==1?(Math.Abs(dx)<25-Mathf.Max(0,-dy)*.45f && Math.Abs(dy)<30):
                    kind==2?(r>22 && r<29 || Math.Abs(dx)<4 || Math.Abs(dy)<4):
                    kind==3?(Math.Abs(dx)<6 && dy>-20 && dy<34 || dy< -12 && Math.Abs(dx)<(-dy-10)*.8f):
                    (Math.Abs(dx)<25 && Math.Abs(dy)<27 && !(Math.Abs(dy)<5 && Math.Abs(dx)>4));
                t.SetPixel(x,y,r>45?new Color(.08f,.1f,.15f):r>41?new Color(.7f,.47f,.15f):mark?new Color(1,.8f,.35f):new Color(.13f,.2f,.26f));
            }
            t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static GameObject Model()
        {
            string folder=Root+"表现/磐锋";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            Material Mat(string name,Color color)
            {
                string path=folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;Dirty(m);return m;
            }
            var steel=Mat("磐锋甲胄",new Color(.22f,.29f,.34f));var gold=Mat("磐锋刃锋",new Color(.88f,.64f,.25f));
            var root=new GameObject("磐锋卫士展示");
            void Part(string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
            {
                var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(root.transform,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            }
            Part("胸甲",PrimitiveType.Capsule,Vector3.zero,new Vector3(1.25f,1,1.1f),steel);
            Part("头盔",PrimitiveType.Cube,new Vector3(0,1.05f,0),new Vector3(.85f,.65f,.8f),gold);
            Part("左肩甲",PrimitiveType.Cube,new Vector3(-.8f,.55f,0),new Vector3(.6f,.5f,.8f),gold);
            Part("右肩甲",PrimitiveType.Cube,new Vector3(.8f,.55f,0),new Vector3(.6f,.5f,.8f),gold);
            Part("重剑",PrimitiveType.Cube,new Vector3(1.1f,.25f,.65f),new Vector3(.22f,2.3f,.5f),gold);
            Part("剑格",PrimitiveType.Cube,new Vector3(1.1f,-.45f,.65f),new Vector3(.8f,.18f,.5f),steel);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,folder+"/磐锋卫士展示.prefab");UnityEngine.Object.DestroyImmediate(root);return prefab;
        }
        public static void Inspect()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            Selection.activeObject=Load<SkillDefinition>("技能/1001_冰霜扇击.asset");
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }
        public static void InspectRoster()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");
            Selection.activeGameObject=UnityEngine.Object.FindFirstObjectByType<HeroRoster>().gameObject;
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }
        public static void Generate()
        {
            var modules=Load<SkillModuleCatalog>("目录/组合模块目录.asset");
            var catalog=Load<SkillCatalog>("目录/游戏技能库.asset");var frost=Load<HeroDefinition>("英雄/2000_寒冰射手.asset");
            var volley=frost.skills[1];var n=volley.recipe.nodes[0];n.deliveryModuleId=10002;n.queryModuleId=10101;n.projectileCount=7;n.spreadDegrees=70;n.hitRadius=.22f;n.speed=40;
            volley.recipe.revision++;volley.description="朝指定方向散射 7 支冰箭。每支箭命中首个敌人后消失，造成伤害并减速 35%，持续 2.5 秒；不同箭可命中同一目标。箭数与散射角可在配方中配置。";Dirty(volley);
            var guard=Asset<BuffDefinition>("Buff/5009_磐石守势.asset");guard.configId=5009;guard.stableId="buff.stone_guard";guard.displayName="磐石守势";guard.duration=4;guard.healthRegen=16;
            guard.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.MoveSpeed,operation=ModifierOperation.Percent,value=-.2f}};Dirty(guard);
            var breakBuff=Asset<BuffDefinition>("Buff/5010_破阵压制.asset");breakBuff.configId=5010;breakBuff.stableId="buff.line_break";breakBuff.displayName="破阵压制";breakBuff.duration=3;
            breakBuff.modifiers=new[]{new AttributeModifier{attribute=AttributeKind.MoveSpeed,operation=ModifierOperation.Percent,value=-.35f},new AttributeModifier{attribute=AttributeKind.AttackDamage,operation=ModifierOperation.Percent,value=-.2f}};Dirty(breakBuff);
            var g=Asset<ApplyBuffEffect>("效果/4011_施加磐石守势.asset");g.configId=4011;g.stableId="effect.stone_guard";g.buff=guard;Dirty(g);
            var b=Asset<ApplyBuffEffect>("效果/4012_施加破阵压制.asset");b.configId=4012;b.stableId="effect.line_break";b.buff=breakBuff;Dirty(b);
            modules.buffs=modules.buffs.Concat(new[]{guard,breakBuff}).Distinct().ToArray();catalog.buffs=catalog.buffs.Concat(new[]{guard,breakBuff}).Distinct().ToArray();
            foreach(var effect in new EffectDefinition[]{g,b}) if(!modules.effects.Any(e=>e.definition==effect)) modules.effects=modules.effects.Append(new EffectModule{definition=effect}).ToArray();
            var qn=Node(10104,Use(4004,90),Use(4012));qn.range=6;qn.halfAngleDegrees=60;
            var q=Skill(1008,"破阵重劈","ability.vanguard_cleave",AimMode.Direction,6,5,25,"普攻命中三次蓄满后，消耗蓄势向前重劈；压低敌人攻击力 20% 与移速 35%，持续 3 秒。",qn);q.requiredHits=3;
            var w=Skill(1009,"磐石战歌","ability.vanguard_guard",AimMode.Self,0,14,40,"立即回复生命，随后 4 秒每秒回复 16 点生命；守势期间移速降低 20%。适合在刃阵中迎战。",Node(10106,Use(4009,45),Use(4011)));
            var en=Node(10103,Use(4004,25));en.radius=4.5f;en.repeatCount=6;en.repeatInterval=.5f;
            var e=Skill(1010,"回环刃阵","ability.vanguard_field",AimMode.Self,0,10,45,"在施放位置留下半径 4.5 米的刃阵，每 0.5 秒伤害一次，共 6 次；刃阵固定在地面，英雄可以离开。",en);
            var rn=Node(10103,Use(4004,240),Use(4012));rn.deliveryModuleId=10003;rn.radius=5;rn.arcHeight=7;rn.flightSeconds=.65f;
            var r=Skill(1011,"震岳落剑","ability.vanguard_fall",AimMode.Point,12,28,80,"向目标区域投下重剑，0.65 秒后造成范围伤害并压制敌人的攻击与移速；用于封路和配合刃阵。",rn);r.growth.track=SkillLevelTrack.Ultimate;
            var attack=Asset<AutoAttackDefinition>("普攻/英雄/裂刃普攻.asset");attack.stableId="attack.vanguard";attack.displayName="裂刃";attack.description="近战普攻；命中为破阵重劈积蓄战意。";attack.icon=q.icon;attack.delivery=AttackDelivery.Melee;attack.range=2.8f;attack.acquisitionRange=8;attack.baseAttackTime=.75f;attack.attackPoint=.18f;attack.backswing=.25f;Dirty(attack);
            var hero=Asset<HeroDefinition>("英雄/2002_磐锋卫士.asset");hero.configId=2002;hero.stableId="hero.stone_vanguard";hero.displayName="磐锋卫士";hero.skills=new[]{q,w,e,r};hero.basicAttack=attack;
            hero.health=620;hero.healthPerLevel=52;hero.baseMana=240;hero.manaPerLevel=14;hero.baseManaRegen=4;hero.manaRegenPerLevel=.2f;hero.speed=8;hero.range=2.8f;hero.attackDamage=48;hero.attackPerLevel=4;hero.attackInterval=.75f;hero.baseHealthRegen=3;hero.regenPerLevel=.2f;hero.attackWhileMoving=false;hero.portrait=Icon("磐锋卫士",4);hero.visualPrefab=Model();hero.baseStats=new[]{new StatModifier(StatKind.DamageReduction,12)};Dirty(hero);
            catalog.skills=catalog.skills.Concat(new[]{q,w,e,r}).Distinct().ToArray();Dirty(catalog);Dirty(modules);AssetDatabase.SaveAssets();NumericConfigIds.AssignMissing();
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Game.unity");var controller=UnityEngine.Object.FindFirstObjectByType<GameCombatController>();
            var existing=UnityEngine.Object.FindFirstObjectByType<HeroRoster>();if(existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var root=new GameObject("英雄选择",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(HeroRoster));
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=30;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            var roster=root.GetComponent<HeroRoster>();roster.controller=controller;roster.heroes=new[]{frost,hero};
            var font=UnityEngine.Object.FindObjectsByType<Text>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(t=>t.font).FirstOrDefault(f=>f);
            if(!font) font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(roster.heroes[i].displayName,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(root.transform,false);
                var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-22,-105-i*42);rect.sizeDelta=new Vector2(250,36);
                go.GetComponent<Image>().color=new Color(.08f,.12f,.17f,.95f);UnityEventTools.AddIntPersistentListener(go.GetComponent<Button>().onClick,roster.Select,i);
                var label=new GameObject("名称",typeof(RectTransform),typeof(Text));label.transform.SetParent(go.transform,false);var lr=(RectTransform)label.transform;lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
                var text=label.GetComponent<Text>();text.font=font;text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;text.color=new Color(1,.8f,.4f);text.text=roster.heroes[i].displayName+" · 选择并重开";text.raycastTarget=false;
            }
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("VANGUARD_CONTENT_READY");
        }
    }
}
