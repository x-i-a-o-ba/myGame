"""One-time asset creation using Unity's existing scene header and known URP shader GUID.
Refuses to overwrite existing outputs. Runtime mesh is Unity's built-in cube.
"""
from pathlib import Path
import uuid
import re

ROOT = Path(__file__).resolve().parents[1]
def guid():
    return uuid.uuid4().hex

def meta(path, asset_guid, file_id=None):
    importer = ('NativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: ' + str(file_id) + '\n') if file_id else 'DefaultImporter:\n  externalObjects: {}\n'
    Path(str(path) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + asset_guid + '\n' + importer, encoding='utf-8')

scene = ROOT / 'Assets/Game/Scenes/Benchmark10k.unity'
material = ROOT / 'Assets/Game/Materials/BenchmarkEnemy.mat'
script = ROOT / 'Assets/Game/Runtime/Benchmark/BenchmarkRunner.cs'
if scene.exists() or material.exists():
    raise SystemExit('Assets already exist; edit with Unity instead of regenerating GUIDs.')
scene.parent.mkdir(parents=True, exist_ok=True)
material.parent.mkdir(parents=True, exist_ok=True)
script_guid, material_guid = guid(), guid()
script_meta = Path(str(script) + '.meta')
if script_meta.exists():
    script_guid = re.search(r'^guid: ([a-f0-9]{32})$', script_meta.read_text(), re.M).group(1)
else:
    script_meta.write_text(f'fileFormatVersion: 2\nguid: {script_guid}\nMonoImporter:\n  externalObjects: {{}}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n', encoding='utf-8')
material.write_text('''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_Name: BenchmarkEnemy
  m_Shader: {fileID: 4800000, guid: 650dd9526735d5b46b79224bc6e94025, type: 3}
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {}
  disabledShaderPasses: []
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Surface: 0
    - _Cull: 2
    - _ZWrite: 1
    - _SrcBlend: 1
    - _DstBlend: 0
    - _AlphaClip: 0
    m_Colors:
    - _BaseColor: {r: 0.1, g: 0.8, b: 0.5, a: 1}
''', encoding='utf-8')
meta(material, material_guid, 2100000)
header = (ROOT / 'Assets/Scenes/Game.unity').read_text(encoding='utf-8').split('--- !u!1 &')[0]
body = '''--- !u!1 &1000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 1001}
  - component: {fileID: 1002}
  m_Layer: 0
  m_Name: Benchmark 10k - movement and rendering only
  m_TagString: Untagged
  m_IsActive: 1
--- !u!4 &1001
Transform:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1000}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_Children: []
  m_Father: {fileID: 0}
--- !u!114 &1002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 1000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: SCRIPT_GUID, type: 3}
  m_Name:
  m_EditorClassIdentifier:
  enemyCount: 10000
  enemyMaterial: {fileID: 2100000, guid: MATERIAL_GUID, type: 2}
  renderEntities: 1
  syntheticDamage: 0
  warmupSeconds: 10
  sampleSeconds: 60
--- !u!1660057539 &9223372036854775807
SceneRoots:
  m_ObjectHideFlags: 0
  m_Roots:
  - {fileID: 1001}
'''
scene.write_text(header + body.replace('SCRIPT_GUID', script_guid).replace('MATERIAL_GUID', material_guid), encoding='utf-8')
meta(scene, guid())
print('Created benchmark scene, material, and stable GUID references.')
