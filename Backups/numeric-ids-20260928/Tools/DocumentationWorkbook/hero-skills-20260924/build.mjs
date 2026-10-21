import fs from 'node:fs/promises';
import path from 'node:path';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const root='D:/UnityProject/myGame';
const output=path.join(root,'outputs/hero-skills-20260924');
const data=JSON.parse(await fs.readFile(path.join(root,'Logs/HeroSkillCatalog/source.json'),'utf8'));
const wb=Workbook.create();
const names=['ID查询','英雄','技能','英雄技能装配','技能阶段'];
const sheets=Object.fromEntries(names.map(n=>[n,wb.worksheets.add(n)]));
const zh={Self:'自身',Point:'目标点',Direction:'方向',Unit:'单位',Single:'单体',Area:'范围',All:'全体',Circle:'圆形',Cone:'扇形',Line:'矩形',Instant:'立即结算',PointProjectile:'飞向目标点',LinearProjectile:'直线首碰',ParabolicProjectile:'三维抛物线',Enemies:'敌方',Allies:'友方',InstantCircle:'即时圆形',PersistentCircle:'持续圆形',SelfCircle:'自身圆形',Damage:'伤害',Heal:'治疗',ApplyBuff:'施加Buff',Dispel:'驱散'};
const tr=v=>zh[v]??v;
const safeParse=s=>s?JSON.parse(s):{};
const effectText=e=>{
 const p=safeParse(e.parameters); let t=e.name;
 if(p.amount!==undefined)t+=` ${p.amount}`;
 if(e.buffId){const b=safeParse(e.buffParameters);t+=`（${e.buffId}，强度 ${p.buffScale??1}，持续 ${b.duration??'?'} 秒）`;}
 return t;
};
const legacyText=s=>(s.legacyEffects??[]).map(e=>`${tr(e.kind)} ${e.kind==='ApplyBuff'?e.buffId+' × '+e.buffScale:e.amount}`).join('；');
function col(n){let s='';for(n++;n;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;}
function table(name,headers,rows,widths){
 const sh=sheets[name],last=col(headers.length-1),end=5+rows.length;
 sh.showGridLines=false;
 sh.getRange(`A1:${last}${end}`).format.font={name:'Arial',size:11,color:'#253247'};
 sh.getRange('A1').values=[[name]];sh.getRange('A1').format.font={size:16,bold:true,color:'#243B63'};
 sh.getRange('A2').values=[['源自已保存的 Unity 配置，2026-09-24。蓝字为可编辑数据，灰底为计算检查。']];
 sh.getRange('A2').format.font={size:10,color:'#64748B'};
 sh.getRange(`A5:${last}${end}`).values=[headers,...rows];
 sh.getRange(`A5:${last}5`).format={fill:'#243B63',font:{bold:true,color:'#FFFFFF'},rowHeight:38,wrapText:true,verticalAlignment:'center'};
 sh.getRange(`A6:${last}${end}`).format={rowHeight:32,verticalAlignment:'center',font:{color:'#1F5CA4'}};
 sh.getRange(`A6:${last}${end}`).setNumberFormat('0.00');
 sh.getRange(`A6:A${end}`).setNumberFormat('@');
 headers.forEach((_,i)=>sh.getRange(`${col(i)}1:${col(i)}${end}`).format.columnWidth=widths[i]??18);
 const t=sh.tables.add(`A5:${last}${end}`,true,`Config${names.indexOf(name)}`);t.showFilterButton=true;
 sh.freezePanes.freezeRows(5);sh.freezePanes.freezeColumns(2);
 return {sh,end};
}
const h=table('英雄',['英雄ID','英雄名称','用途','基础生命','移动速度\n单位/秒','普攻伤害','普攻间隔\n秒','普攻距离\nUnity单位','资源路径','ID检查'],data.heroes.map(x=>[x.id,x.name,x.usage,x.health,x.speed,x.attackDamage,x.attackInterval,x.range,x.path,null]),[43,28,16,14,16,14,16,18,66,18]);
const s=table('技能',['技能ID','技能名称','用途','瞄准方式','施法距离\nUnity单位','冷却\n秒','前摇\n秒','后摇\n秒','资源消耗','最大充能','充能恢复\n秒；0用冷却','普攻命中要求','组合阶段数','资源路径','旧模型类型','旧模型效果','ID检查'],data.skills.map(x=>[x.id,x.name,x.usage,tr(x.aim),x.castRange,x.cooldown,x.castTime,x.recoveryTime,x.manaCost,x.maxCharges,x.rechargeSeconds,x.requiredHits,x.stageCount,x.path,x.stageCount?null:tr(x.legacyDelivery),x.stageCount?null:legacyText(x),null]),[49,24,16,15,18,12,12,12,14,14,18,18,18,66,18,56,18]);
const l=table('英雄技能装配',['英雄ID','英雄名称（查表）','槽位序号','默认输入','技能ID','技能名称（查表）','引用检查'],data.loadouts.map(x=>[x.heroId,null,x.slot,['Q','W','E','R'][x.slot-1]??`槽${x.slot}`,x.skillId,null,null]),[43,29,15,15,49,24,18]);
const stages=table('技能阶段',['技能ID','阶段序号','释放后延迟\n秒','执行次数','重复间隔\n秒','投递方式','目标范围','区域形状','目标关系','长度\nUnity单位','半径或半宽\nUnity单位','碰撞半径\nUnity单位','扇形半角\n度','飞行速度\n单位/秒','弧高\nUnity单位','飞行时长\n秒','发射高度\nUnity单位','落点高度\nUnity单位','范围效果（按顺序）','首碰效果（按顺序）'],data.stages.map(x=>[x.skillId,x.index,x.delay,x.repeatCount,x.repeatInterval,tr(x.delivery),tr(x.scope),tr(x.shape),tr(x.targets),x.range,x.radius,x.hitRadius,x.halfAngleDegrees,x.speed,x.arcHeight,x.flightSeconds,x.launchHeight,x.arrivalHeight,(x.effects??[]).map(effectText).join('\n'),(x.impactEffects??[]).map(effectText).join('\n')]),[49,13,18,13,16,22,14,14,14,16,18,18,16,18,16,18,18,18,65,65]);
stages.sh.getRange(`S6:T${stages.end}`).format.wrapText=true;
stages.sh.getRange(`A6:T${stages.end}`).format.rowHeight=62;
for(const [tab,columns] of [[h,['D','F']],[s,['J','L','M']],[l,['C']],[stages,['B','D']]])
 for(const c of columns)tab.sh.getRange(`${c}6:${c}${tab.end}`).setNumberFormat('0');
s.sh.getRange(`N6:P${s.end}`).format.wrapText=true;
h.sh.getRange(`I6:I${h.end}`).format.wrapText=true;
const bounds={hero:"'英雄'!$A$6:$A$1005",skill:"'技能'!$A$6:$A$1005"};
for(const [tab,c] of [[h,'J'],[s,'Q']]){
 for(let r=6;r<=tab.end;r++)tab.sh.getRange(`${c}${r}`).formulas=[[`=IF(A${r}="","缺少ID",IF(COUNTIFS(${bounds.hero},A${r})+COUNTIFS(${bounds.skill},A${r})=1,"唯一","ID重复"))`]];
 tab.sh.getRange(`${c}6:${c}${tab.end}`).format.fill='#F1F5F9';
 tab.sh.getRange(`${c}6:${c}${tab.end}`).conditionalFormats.add('containsText',{text:'重复',format:{fill:'#FEE2E2',font:{color:'#B91C1C',bold:true}}});
}
for(let r=6;r<=l.end;r++){
 l.sh.getRange(`B${r}`).formulas=[[`=IF(COUNTIFS(${bounds.hero},A${r})=1,INDEX('英雄'!$B$6:$B$1005,MATCH(A${r},${bounds.hero},0)),"英雄ID无效")`]];
 l.sh.getRange(`F${r}`).formulas=[[`=IF(COUNTIFS(${bounds.skill},E${r})=1,INDEX('技能'!$B$6:$B$1005,MATCH(E${r},${bounds.skill},0)),"技能ID无效")`]];
 l.sh.getRange(`G${r}`).formulas=[[`=IF(AND(COUNTIFS(${bounds.hero},A${r})=1,COUNTIFS(${bounds.skill},E${r})=1,COUNTIFS($A$6:$A$1005,A${r},$C$6:$C$1005,C${r})=1),"有效","引用或槽位重复")`]];
}
for(const c of ['B','F','G'])l.sh.getRange(`${c}6:${c}${l.end}`).format.fill='#F1F5F9';
const q=sheets.ID查询;q.showGridLines=false;q.tabColor='#243B63';
q.getRange('A1:B24').format.font={name:'Arial',size:11,color:'#253247'};
q.getRange('A1:A24').format.columnWidth=24;q.getRange('B1:B24').format.columnWidth=92;
q.getRange('A1:B24').format.rowHeight=28;
q.getRange('A1').values=[['英雄与技能配置目录']];q.getRange('A1').format.font={size:16,bold:true,color:'#243B63'};
q.getRange('A3:B4').values=[['快照日期','2026-09-24'],['输入英雄或技能ID','ability.crystal_arrow']];
q.getRange('B4').format={fill:'#FFF0C2',font:{color:'#1F5CA4'},rowHeight:36};
q.getRange('A6:A10').values=[['匹配数量'],['类型'],['名称'],['用途'],['Unity资源路径']];
q.getRange('B6').formulas=[[`=IF(B4="",0,COUNTIFS(${bounds.hero},B4)+COUNTIFS(${bounds.skill},B4))`]];
q.getRange('B7').formulas=[[`=IF(B4="","请输入ID",IF(B6=0,"未找到",IF(B6>1,"ID重复",IF(COUNTIFS(${bounds.hero},B4)=1,"英雄","技能"))))`]];
for(const [row,hcol,scol] of [[8,'B','B'],[9,'C','C'],[10,'I','N']])q.getRange(`B${row}`).formulas=[[`=IF(B6<>1,"请检查输入或重复ID",IF(B7="英雄",INDEX('英雄'!$${hcol}$6:$${hcol}$1005,MATCH(B4,${bounds.hero},0)),INDEX('技能'!$${scol}$6:$${scol}$1005,MATCH(B4,${bounds.skill},0))))`]];
q.getRange('B6:B10').format.fill='#F1F5F9';q.getRange('B10').format.wrapText=true;q.getRange('A10:B10').format.rowHeight=44;
q.getRange('A13:B21').values=[
 ['使用方式','筛选“英雄技能装配”的英雄ID，可查看该英雄每个槽位对应的技能ID。'],
 ['编号规则','沿用 Unity stableId。新英雄使用 hero.前缀，新技能使用 ability.前缀。统一小写，不复用、不随名称修改。'],
 ['历史编号','现有 skill.* 与随机字符串形式的技能ID继续保留。ID按不区分大小写检查唯一。'],
 ['关系规则','英雄、技能各一行；装配以“英雄ID + 槽位序号”为键。同一技能可被多个英雄引用。'],
 ['填写与扩展','在对应数据表末尾追加记录。新增行复制检查公式；查询和检查覆盖第6～1005行，超过1000条需扩展范围。'],
 ['生效方式','Excel 为设计文档，修改不会自动写回 Unity。当前运行数据仍以 Unity 配置资源为准。'],
 ['时间与单位','距离为 Unity 世界单位，时间为秒。前摇结束后释放；阶段延迟从释放点算，后摇不等待飞行。'],
 ['阶段字段','只使用与当前目标、形状、投递方式相关的字段。自带默认值不代表所有字段同时生效。'],
 ['历史示例','组合阶段数为0的历史技能使用旧模型效果；尚未归属英雄的技能也保留在技能总表。']
 ];q.getRange('B13:B21').format.wrapText=true;q.getRange('A13:B21').format.rowHeight=42;
q.getRange('A23:B23').values=[['配置统计',`${data.heroes.length} 个英雄定义，${data.skills.length} 个技能定义，${data.loadouts.length} 条装配，${data.stages.length} 个组合阶段`]];
// 验证查询会随输入变化，并显式覆盖缺失和重复ID，不仅检查初始缓存。
for(const [id,expected] of [['hero.frost_archer','英雄'],['ability.crystal_arrow','技能'],['missing.id','未找到'],['','请输入ID']]){
 q.getRange('B4').values=[[id]];wb.recalculate();if(q.getRange('B7').values[0][0]!==expected)throw Error('ID查询失败:'+id);
}
const original=h.sh.getRange('A6').values[0][0];h.sh.getRange('A6').values=[['ability.crystal_arrow']];q.getRange('B4').values=[['ability.crystal_arrow']];wb.recalculate();
if(q.getRange('B7').values[0][0]!=='ID重复')throw Error('重复ID未拒绝');h.sh.getRange('A6').values=[[original]];
q.getRange('B4').values=[['ability.crystal_arrow']];wb.recalculate();
for(const [tab,c] of [[h,'J'],[s,'Q']])for(const row of tab.sh.getRange(`${c}6:${c}${tab.end}`).values)if(row[0]!=='唯一')throw Error('ID校验失败');
for(const row of l.sh.getRange(`G6:G${l.end}`).values)if(row[0]!=='有效')throw Error('装配校验失败');
await fs.mkdir(output,{recursive:true});
console.log((await wb.inspect({kind:'table',range:'ID查询!A4:B10',include:'values,formulas',tableMaxRows:7,tableMaxCols:2,maxChars:2000})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!',options:{useRegex:true,maxResults:20},summary:'最终公式错误检查',maxChars:800})).ndjson);
const file=await SpreadsheetFile.exportXlsx(wb);const filename='英雄与技能配置.xlsx';await file.save(path.join(output,filename));
await fs.copyFile(path.join(output,filename),path.join(root,'Docs/游戏配置',filename));
console.log('WORKBOOK_VALIDATED heroes='+data.heroes.length+' skills='+data.skills.length+' loadouts='+data.loadouts.length+' stages='+data.stages.length);
// 此 Windows 环境原生渲染进程退出；最终视觉检查采用 Excel PDF 导出。
// 其他支持渲染的环境可显式启用，不把渲染成功当作导出成功的替代。
if(process.env.ARTIFACT_PREVIEW==='1') for(const [name,range] of [['ID查询','A1:B23'],['英雄',`A5:J${h.end}`],['技能',`A5:M${s.end}`],['英雄技能装配',`A5:G${l.end}`],['技能阶段',`A5:I${stages.end}`]]){
 const image=await wb.render({sheetName:name,range,scale:1,format:'png'});await fs.writeFile(path.join(output,name+'.png'),new Uint8Array(await image.arrayBuffer()));
}
