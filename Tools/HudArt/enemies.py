import os, json, math, uuid, re, sys
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from common import ROOT, load_guids, save_guids, guid_for, folder_meta
from ui import us
g=load_guids()
icons=json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'icons.json')))

def write(path,text):
    full=os.path.join(ROOT,path); os.makedirs(os.path.dirname(full),exist_ok=True)
    open(full,"w").write(text)
def native_meta(path,main=11400000):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: %d\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%(guid_for(path,g),main))
def mono_meta(path):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))
def prefab_meta(path):
    write(path+".meta","fileFormatVersion: 2\nguid: %s\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"%guid_for(path,g))

# ---------- 脚本与文件夹 meta ----------
for folder in ["Assets/Game/UI","Assets/Game/UI/Sprites","Assets/Game/UI/Icons",
               "Assets/Game/Abilities/Core/AutoAttack","Assets/Game/Runtime/Combat/Enemies",
               "Assets/Game/Enemies","Assets/Game/Enemies/Attacks","Assets/Game/Enemies/Materials","Assets/Game/Enemies/Prefabs",
               "Assets/Game/Skills/Foundation/AutoAttacks"]:
    os.makedirs(os.path.join(ROOT,folder),exist_ok=True); folder_meta(folder,g)
SCRIPTS={}
for script in ["Assets/Game/Abilities/Core/AutoAttack/AutoAttackDefinition.cs","Assets/Game/Abilities/Core/AutoAttack/AutoAttackLogic.cs",
               "Assets/Game/Runtime/Combat/Enemies/EnemyArchetype.cs","Assets/Game/Runtime/Combat/Enemies/EnemyRoster.cs"]:
    if not os.path.exists(os.path.join(ROOT,script)+".meta"): mono_meta(script)
    SCRIPTS[os.path.basename(script)[:-3]]=re.search(r"guid: (\w+)",open(os.path.join(ROOT,script)+".meta").read()).group(1)

# ---------- 自动攻击模块 ----------
def attack_asset(path,stable,name,icon,desc,delivery,rng,acq,bat,point,back,speed,buffer,counts):
    text=f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['AutoAttackDefinition']}, type: 3}}
  m_Name: {us(os.path.basename(path)[:-6])}
  m_EditorClassIdentifier: Roguelike.Abilities.Core::Roguelike.Abilities.AutoAttackDefinition
  stableId: {stable}
  displayName: {us(name)}
  icon: {{fileID: 2800000, guid: {icons[icon]}, type: 3}}
  description: {us(desc)}
  delivery: {delivery}
  range: {rng}
  acquisitionRange: {acq}
  baseAttackTime: {bat}
  attackPoint: {point}
  backswing: {back}
  projectileSpeed: {speed}
  rangeBuffer: {buffer}
  autoAcquire: 1
  countsAsBasicAttackHit: {counts}
"""
    write(path,text); native_meta(path)
    return guid_for(path,g)
A="Assets/Game/Enemies/Attacks/"
attacks={
 "冰箭普攻":attack_asset("Assets/Game/Skills/Foundation/AutoAttacks/冰箭普攻.asset","attack.frost_arrow","冰箭","冰箭普攻",
    "被动：自动向攻击距离内的敌人射出追踪冰箭。命中计入普攻命中，可为冰霜专注叠层。",1,20,22,0.12,0.03,0.05,38,1.5,1),
 "腐爪":attack_asset(A+"腐爪.asset","attack.rot_claw","腐爪","腐爪","近战：前摇结束立即抓伤目标。",0,1.25,14,1.0,0.35,0.4,0,0.5,0),
 "迅捷撕咬":attack_asset(A+"迅捷撕咬.asset","attack.swift_bite","迅捷撕咬","腐爪","近战：短前摇、短间隔的快速撕咬。",0,1.1,14,0.75,0.2,0.3,0,0.5,0),
 "骨箭":attack_asset(A+"骨箭.asset","attack.bone_arrow","骨箭","骨箭","弹道：前摇结束射出追踪骨箭，命中后结算伤害。",1,12,16,1.6,0.4,0.4,14,1.5,0),
 "蛮角冲撞":attack_asset(A+"蛮角冲撞.asset","attack.horn_gore","蛮角顶撞","蛮角冲撞","近战：冲锋间隙用犄角顶撞贴身目标。",0,1.4,14,1.2,0.4,0.5,0,0.5,0),
 "毒囊自爆":attack_asset(A+"毒囊自爆.asset","attack.toxic_burst","毒囊自爆","毒囊自爆","自爆：贴近后开始前摇，前摇结束对爆炸半径内的英雄造成伤害并死亡；前摇期间被拉开距离会取消。",0,1.6,14,1.0,0.8,0,0,0.8,0),
 "岩石重击":attack_asset(A+"岩石重击.asset","attack.rock_slam","岩石重击","岩石重击","近战：慢速但沉重的拳击。",0,1.6,14,1.6,0.5,0.6,0,0.5,0),
 "深渊横扫":attack_asset(A+"深渊横扫.asset","attack.abyss_sweep","深渊横扫","深渊横扫","近战：首领的大范围横扫，攻击距离更长。",0,2.2,18,1.3,0.45,0.5,0,0.6,0),
 "巫妖霜弹":attack_asset(A+"巫妖霜弹.asset","attack.lich_bolt","巫妖霜弹","巫妖霜弹","弹道：远距离射出霜弹，命中后结算伤害。",1,16,20,1.4,0.5,0.5,16,1.5,0),
}

# ---------- 材质 ----------
enemy_mat=open(ROOT+"/Assets/Game/Materials/GameEnemy.mat").read()
proj_mat=open(ROOT+"/Assets/Game/Materials/GameProjectile.mat").read()
def material(template,old_name,name,color,old_colors):
    text=template.replace("m_Name: "+old_name,"m_Name: "+us(name))
    r,gg,b=color
    for old in old_colors:
        text=text.replace(old,"{r: %s, g: %s, b: %s, a: 1}"%(r,gg,b))
    path="Assets/Game/Enemies/Materials/%s.mat"%name
    write(path,text); native_meta(path,2100000)
    return guid_for(path,g)
ENEMY_COLORS=["_BaseColor: {r: 0.72, g: 0.18, b: 0.13, a: 1}","_Color: {r: 0.72, g: 0.17999998, b: 0.12999997, a: 1}"]
def emat(name,color):
    return material(enemy_mat,"GameEnemy",name,color,[c.split(": ",1)[1] for c in ENEMY_COLORS])
hostile=material(proj_mat,"GameProjectile","敌方弹体",(0.86,0.32,1.0),["{r: 1, g: 0.79, b: 0.19, a: 1}","{r: 1, g: 0.78999996, b: 0.18999997, a: 1}"])

# ---------- 预制体 ----------
MESH={"cube":10202,"cylinder":10206,"sphere":10207,"capsule":10208}
def fid(key): return int(uuid.uuid5(uuid.NAMESPACE_URL,key).int%(2**62))+1000000
def quat(ex,ey,ez):
    def q(axis,deg):
        h=math.radians(deg)/2; s=math.sin(h); c=math.cos(h)
        return [s if axis==0 else 0, s if axis==1 else 0, s if axis==2 else 0, c]
    def mul(a,b):
        ax,ay,az,aw=a; bx,by,bz,bw=b
        return [aw*bx+ax*bw+ay*bz-az*by, aw*by-ax*bz+ay*bw+az*bx, aw*bz+ax*by-ay*bx+az*bw, aw*bw-ax*bx-ay*by-az*bz]
    r=mul(mul(q(1,ey),q(0,ex)),q(2,ez))
    return [round(v,6)+0.0 for v in r]
def num(v): 
    v=round(v,4)
    return str(int(v)) if v==int(v) else str(v)
def v3(a): return "{x: %s, y: %s, z: %s}"%tuple(num(x) for x in a)
def renderer(go,rid,mat):
    return f"""--- !u!23 &{rid}
MeshRenderer:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 1
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 1
  m_RayTracingMode: 2
  m_RayTraceProcedural: 0
  m_RayTracingAccelStructBuildFlagsOverride: 0
  m_RayTracingAccelStructBuildFlags: 1
  m_SmallMeshCulling: 1
  m_ForceMeshLod: -1
  m_MeshLodSelectionBias: 0
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
  - {{fileID: 2100000, guid: {mat}, type: 2}}
  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 1
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: 0.5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""
def gameobject(go,components,name):
    comps="".join("  - component: {fileID: %d}\n"%c for c in components)
    return f"""--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
{comps}  m_Layer: 0
  m_Name: {us(name) if any(ord(c)>127 for c in name) else name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
"""
def transform(tid,go,pos,rot,scale,children,father):
    q=quat(*rot)
    kids="".join("  - {fileID: %d}\n"%c for c in children) if children else None
    return f"""--- !u!4 &{tid}
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  serializedVersion: 2
  m_LocalRotation: {{x: {num(q[0])}, y: {num(q[1])}, z: {num(q[2])}, w: {num(q[3])}}}
  m_LocalPosition: {v3(pos)}
  m_LocalScale: {v3(scale)}
  m_ConstrainProportionsScale: 0
  m_Children:{chr(10)+kids if kids else ' []'+chr(10)}  m_Father: {{fileID: {father}}}
  m_LocalEulerAnglesHint: {v3(rot)}
"""
PREFABS={}
def enemy(cid,file,stable,name,desc,color,behavior,attack,parts,**stats):
    mat=emat(file,color)
    path="Assets/Game/Enemies/Prefabs/%s.prefab"%file
    key=path
    root_go,root_tf,root_mb=fid(key+"go"),fid(key+"tf"),fid(key+"mb")
    blocks=[]; child_tfs=[]
    for i,(pname,shape,pos,rot,scale) in enumerate(parts):
        go,tf,mf,mr=(fid("%s/%d/%s"%(key,i,s)) for s in ("go","tf","mf","mr"))
        child_tfs.append(tf)
        blocks.append(gameobject(go,[tf,mf,mr],pname))
        blocks.append(transform(tf,go,pos,rot,scale,[],root_tf))
        blocks.append(f"""--- !u!33 &{mf}
MeshFilter:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
  m_Mesh: {{fileID: {MESH[shape]}, guid: 0000000000000000e000000000000000, type: 0}}
""")
        blocks.append(renderer(go,mr,mat))
    s=dict(healthMultiplier=1,damageMultiplier=1,speedMultiplier=1,experienceMultiplier=1,goldMultiplier=1,bodyScale=1,preferredDistance=0.85,retreatDistance=5,
           chargeTriggerRange=10,chargeSpeedMultiplier=4,chargeDuration=0.7,chargeCooldown=5,chargeDamageMultiplier=2.5,explosionRadius=3.5,explosionDamageMultiplier=5)
    s.update(stats)
    if "goldMultiplier" not in stats: s["goldMultiplier"]=s["experienceMultiplier"]  # 击杀金币倍率默认与经验倍率一致
    fields="".join("  %s: %s\n"%(k,num(v)) for k,v in s.items())
    head=gameobject(root_go,[root_tf,root_mb],file)+transform(root_tf,root_go,(0,0,0),(0,0,0),(1,1,1),child_tfs,0)
    mb=f"""--- !u!114 &{root_mb}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_go}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['EnemyArchetype']}, type: 3}}
  m_Name: 
  m_EditorClassIdentifier: Roguelike.Runtime::Roguelike.Combat.EnemyArchetype
  configId: {cid}
  stableId: {stable}
  displayName: {us(name)}
  description: {us(desc)}
  portrait: {{fileID: 2800000, guid: {icons[name]}, type: 3}}
  behavior: {behavior}
  attack: {{fileID: 11400000, guid: {attacks[attack]}, type: 2}}
{fields}"""
    write(path,"%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"+head+mb+"".join(blocks))
    prefab_meta(path)
    PREFABS[cid]=dict(path=path,guid=guid_for(path,g),component=root_mb,name=name,stable=stable)

C,S,Y,K="cube","sphere","cylinder","capsule"
enemy(3001,"腐化食尸鬼","enemy.ghoul","腐化食尸鬼","最常见的近战怪物：成群追击英雄，贴身后用腐爪攻击。",(0.44,0.56,0.3),0,"腐爪",[
    ("躯干",K,(0,-0.12,0),(18,0,0),(0.62,0.34,0.55)),
    ("头",S,(0,0.24,0.2),(0,0,0),(0.36,0.34,0.36)),
    ("左臂",C,(-0.33,-0.12,0.14),(35,0,8),(0.13,0.46,0.13)),
    ("右臂",C,(0.33,-0.12,0.14),(35,0,-8),(0.13,0.46,0.13))])
enemy(3002,"迅捷猎犬","enemy.hound","迅捷猎犬","体型小、移动极快的近战怪物，生命与伤害较低，经常从侧翼包抄。",(0.62,0.36,0.22),0,"迅捷撕咬",[
    ("躯干",K,(0,-0.08,-0.04),(90,0,0),(0.42,0.4,0.42)),
    ("头",S,(0,0.08,0.4),(0,0,0),(0.32,0.3,0.36)),
    ("左耳",C,(-0.1,0.26,0.36),(0,0,-15),(0.07,0.14,0.07)),
    ("右耳",C,(0.1,0.26,0.36),(0,0,15),(0.07,0.14,0.07)),
    ("左前腿",C,(-0.14,-0.34,0.22),(0,0,0),(0.1,0.32,0.1)),
    ("右前腿",C,(0.14,-0.34,0.22),(0,0,0),(0.1,0.32,0.1)),
    ("左后腿",C,(-0.14,-0.34,-0.28),(0,0,0),(0.1,0.32,0.1)),
    ("右后腿",C,(0.14,-0.34,-0.28),(0,0,0),(0.1,0.32,0.1)),
    ("尾巴",C,(0,0.02,-0.48),(-40,0,0),(0.06,0.06,0.24))],
    healthMultiplier=0.55,damageMultiplier=0.7,speedMultiplier=1.7,experienceMultiplier=0.8,bodyScale=0.85)
enemy(3003,"骷髅弓手","enemy.skeleton_archer","骷髅弓手","远程游击：停在攻击距离边缘射出骨箭，英雄靠近时后撤。",(0.86,0.82,0.7),1,"骨箭",[
    ("躯干",Y,(0,-0.12,0),(0,0,0),(0.3,0.36,0.26)),
    ("头",S,(0,0.36,0),(0,0,0),(0.3,0.32,0.3)),
    ("长弓",C,(0.3,0,0.14),(0,0,0),(0.05,0.82,0.05)),
    ("弓臂上",C,(0.3,0.34,0.08),(-25,0,0),(0.05,0.16,0.05)),
    ("弓臂下",C,(0.3,-0.34,0.08),(25,0,0),(0.05,0.16,0.05)),
    ("箭袋",Y,(-0.12,0.05,-0.18),(15,0,0),(0.12,0.22,0.12))],
    healthMultiplier=0.7,damageMultiplier=0.8,speedMultiplier=0.9,preferredDistance=0.85,retreatDistance=5)
enemy(3004,"蛮牛冲锋者","enemy.bull_charger","蛮牛冲锋者","进入冲锋距离后直线突进，撞到英雄造成高额伤害；冲锋间隙用犄角近战。",(0.46,0.25,0.16),2,"蛮角冲撞",[
    ("躯干",C,(0,-0.12,-0.05),(0,0,0),(0.72,0.56,0.86)),
    ("头",C,(0,-0.02,0.5),(10,0,0),(0.46,0.38,0.3)),
    ("左角",Y,(-0.3,0.22,0.56),(0,0,55),(0.08,0.16,0.08)),
    ("右角",Y,(0.3,0.22,0.56),(0,0,-55),(0.08,0.16,0.08)),
    ("驼峰",S,(0,0.18,0.12),(0,0,0),(0.6,0.36,0.5))],
    healthMultiplier=1.8,damageMultiplier=1.2,speedMultiplier=0.8,experienceMultiplier=1.6,bodyScale=1.25,
    chargeTriggerRange=10,chargeSpeedMultiplier=4,chargeDuration=0.7,chargeCooldown=5,chargeDamageMultiplier=2.5)
enemy(3005,"爆裂毒囊","enemy.toxic_sac","爆裂毒囊","冲向英雄并在贴近后自爆，对爆炸范围内造成大量伤害；在前摇结束前击杀或拉开距离即可避免。",(0.56,0.8,0.2),3,"毒囊自爆",[
    ("毒囊",S,(0,-0.04,0),(0,0,0),(0.82,0.78,0.82)),
    ("脓包1",S,(0.26,0.22,0.14),(0,0,0),(0.28,0.28,0.28)),
    ("脓包2",S,(-0.28,0.16,-0.1),(0,0,0),(0.24,0.24,0.24)),
    ("脓包3",S,(0.04,0.34,-0.2),(0,0,0),(0.2,0.2,0.2)),
    ("左足",C,(-0.2,-0.42,0.06),(0,0,0),(0.12,0.14,0.12)),
    ("右足",C,(0.2,-0.42,0.06),(0,0,0),(0.12,0.14,0.12))],
    healthMultiplier=0.5,damageMultiplier=1,speedMultiplier=1.25,experienceMultiplier=0.6,bodyScale=0.9,
    explosionRadius=3.5,explosionDamageMultiplier=5)
enemy(3006,"岩石魔像","enemy.rock_golem","岩石魔像","高生命、低速度的坦克怪物，会挡住其他怪物的去路，攻击缓慢但沉重。第 6 波起出现。",(0.48,0.46,0.44),0,"岩石重击",[
    ("躯干",C,(0,0.04,0),(0,0,4),(0.78,0.58,0.54)),
    ("头",C,(0,0.42,0.06),(0,12,0),(0.3,0.24,0.28)),
    ("左臂",C,(-0.52,-0.06,0.04),(0,0,6),(0.26,0.7,0.3)),
    ("右臂",C,(0.52,-0.06,0.04),(0,0,-6),(0.26,0.7,0.3)),
    ("左腿",C,(-0.18,-0.36,0),(0,0,0),(0.26,0.3,0.28)),
    ("右腿",C,(0.18,-0.36,0),(0,0,0),(0.26,0.3,0.28))],
    healthMultiplier=3,damageMultiplier=1.5,speedMultiplier=0.65,experienceMultiplier=2.5,bodyScale=1.4)
enemy(3007,"深渊领主","enemy.abyss_lord","深渊领主","首领：攻击距离更长的近战巨兽，生命极高。",(0.55,0.12,0.18),0,"深渊横扫",[
    ("躯干",K,(0,-0.06,0),(0,0,0),(0.66,0.42,0.6)),
    ("头",S,(0,0.36,0.1),(0,0,0),(0.4,0.36,0.4)),
    ("左角",Y,(-0.18,0.52,0.08),(-20,0,28),(0.07,0.12,0.07)),
    ("右角",Y,(0.18,0.52,0.08),(-20,0,-28),(0.07,0.12,0.07)),
    ("左翼",C,(-0.46,0.14,-0.22),(0,-30,-20),(0.6,0.42,0.04)),
    ("右翼",C,(0.46,0.14,-0.22),(0,30,20),(0.6,0.42,0.04)),
    ("巨剑",C,(0.4,-0.04,0.28),(70,0,0),(0.07,0.8,0.12))],
    healthMultiplier=1.2,damageMultiplier=1.2,speedMultiplier=0.9,experienceMultiplier=1.5,bodyScale=1.2)
enemy(3008,"霜骨巫妖","enemy.frost_lich","霜骨巫妖","远程游击的精英与首领：远距离发射霜弹，英雄靠近时后撤。第 10 波起出现。",(0.6,0.5,0.9),1,"巫妖霜弹",[
    ("长袍",Y,(0,-0.12,0),(0,0,0),(0.5,0.38,0.5)),
    ("胸甲",S,(0,0.2,0),(0,0,0),(0.42,0.32,0.36)),
    ("头",S,(0,0.42,0.02),(0,0,0),(0.28,0.3,0.28)),
    ("冠冕",Y,(0,0.58,0.02),(0,0,0),(0.24,0.04,0.24)),
    ("法杖",Y,(0.36,-0.02,0.12),(0,0,0),(0.05,0.48,0.05)),
    ("法球",S,(0.36,0.5,0.12),(0,0,0),(0.18,0.18,0.18))],
    healthMultiplier=1.4,damageMultiplier=1.3,speedMultiplier=0.85,experienceMultiplier=2,bodyScale=1.1,preferredDistance=0.8,retreatDistance=6)

# ---------- 名册 ----------
ROSTER=[(3001,10,1,0,3),(3002,6,2,0,3),(3003,5,3,0,3),(3004,3,4,0,3),(3005,3,5,0,1),(3006,1.5,6,0,3),(3007,1,1,0,4),(3008,1,10,0,6)]
entries="".join(f"""  - prefab: {{fileID: {PREFABS[c]['component']}, guid: {PREFABS[c]['guid']}, type: 3}}
    weight: {num(w)}
    firstWave: {f}
    lastWave: {l}
    tiers: {t}
""" for c,w,f,l,t in ROSTER)
rpath="Assets/Game/Enemies/敌人名册.asset"
write(rpath,f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {SCRIPTS['EnemyRoster']}, type: 3}}
  m_Name: {us("敌人名册")}
  m_EditorClassIdentifier: Roguelike.Runtime::Roguelike.Combat.EnemyRoster
  entries:
{entries}  eliteTint: {{r: 1, g: 0.78, b: 0.28, a: 1}}
  eliteTintStrength: 0.45
  bossTint: {{r: 1, g: 0.35, b: 0.3, a: 1}}
  bossTintStrength: 0.15
""")
native_meta(rpath)
save_guids(g)
json.dump(dict(roster=guid_for(rpath,g),hostile=hostile,attacks=attacks,prefabs=PREFABS,scripts=SCRIPTS),open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'enemies.json'),"w"),ensure_ascii=False,indent=1)
print("ok",len(PREFABS),"prefabs")
