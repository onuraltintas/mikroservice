import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const file=name=>new URL(name,import.meta.url);
const read=name=>JSON.parse(readFileSync(file(name),'utf8'));
const count=text=>text.trim().split(/\s+/u).length;
const uuid=key=>{
  const hash=createHash('sha256').update(`error-analysis-v1:${key}`).digest('hex');
  return `${hash.slice(0,8)}-${hash.slice(8,12)}-5${hash.slice(13,16)}-8${hash.slice(17,20)}-${hash.slice(20,32)}`;
};
const bands={child:[[180,240],[240,300]],teen:[[240,320],[320,400]],'young-adult':[[300,380],[380,460]],adult:[[340,420],[420,500]]};
const ageLabels={child:'Çocuk',teen:'Genç','young-adult':'Genç yetişkin',adult:'Yetişkin'};
const themes={garden:'Bahçe',library:'Kütüphane',map:'Harita',memory:'Bellek',bridge:'Köprü',archive:'Mahalle arşivi',repair:'Onarım atölyesi'};
const pairs=read('./pairs.json');
for(const pair of pairs) if(!pair.original||!pair.wrong||pair.original===pair.wrong||/\s/u.test(pair.wrong)) throw new Error('Invalid curated pair');
const source=(age,key)=>['garden','library','map'].includes(key)?`../../grouping/v1/texts/${key}.txt`
  :['memory','bridge'].includes(key)?`../../guided-reading/v1/texts/child-5-${key}.txt`
  :`../../comprehension/v1/texts/${key==='archive'?'community-archive':'repair-workshop'}.txt`;
function annotate(text,level,required) {
  const seen=new Set();
  const candidates=text.trim().split(/\s+/u).flatMap((token,index)=>{
    const match=token.match(/\p{L}+/u); if(!match) return [];
    const pair=pairs.find(pair=>pair.original===match[0].toLocaleLowerCase('tr'));
    if(!pair||seen.has(pair.original)) return []; seen.add(pair.original);
    const wrong=match[0][0]===match[0][0].toLocaleUpperCase('tr')?pair.wrong[0].toLocaleUpperCase('tr')+pair.wrong.slice(1):pair.wrong;
    return [{wordIndex:index,originalWord:token,errorWord:token.slice(0,match.index)+wrong+token.slice(match.index+match[0].length),
      errorType:pair.type,explanation:`${pair.type==='transposition'?'Harflerin sırası':pair.type==='extra_letter'?'Fazladan harf':'Eksik harf'} düzeltilmeli. Doğru yazım: ${token}`}];
  });
  const selected=[];
  // Level 5 includes at least two transpositions; spread other targets across the text.
  if(level===5) selected.push(...candidates.filter(row=>row.errorType==='transposition').slice(-2));
  const rest=candidates.filter(row=>!selected.includes(row));
  const needed=required-selected.length;
  for(let i=0;i<needed&&rest.length;i++) selected.push(rest.splice(Math.floor((rest.length-1)*i/Math.max(1,needed-1)),1)[0]);
  if(selected.length!==required||level===5&&selected.filter(row=>row.errorType==='transposition').length<2) return null;
  return selected.sort((a,b)=>a.wordIndex-b.wordIndex);
}
const previous=existsSync(file('./settings.json'))?read('./settings.json'):[];
const settings=[],content=[];
for(const [age,lengths] of Object.entries(bands)) {
  const catalogUrl=file(`../../${age}-exercises/v1/catalog.json`), catalog=JSON.parse(readFileSync(catalogUrl,'utf8'));
  for(let level=4;level<=5;level++) {
    const row=catalog.find(row=>row.type==='ErrorAnalysis'&&row.difficultyLevel===level); if(!row) throw new Error(`Missing ${age}/${level}`);
    const old=previous.find(item=>item.id===row.id);
    const previousConfiguration=old?.previousConfiguration??structuredClone(row.configuration);
    const [min,max]=lengths[level-4], oldConfig=previousConfiguration.engineConfig;
    const map={};
    const keys=['garden','library','map',...(age==='child'||age==='teen'?['memory','bridge']:['archive','repair'])];
    for(const key of keys) {
      const path=source(age,key), original=readFileSync(file(path),'utf8').trim().replace(/\r\n/g,'\n');
      const selections=[...original.matchAll(/[.!?](?=\s|$)/gu)].map(match=>original.slice(0,match.index+1))
        .filter(text=>count(text)>=min&&count(text)<=max).map(text=>({text,errors:annotate(text,level,oldConfig.errorCount)}))
        .filter(row=>row.errors).sort((a,b)=>Math.abs(count(a.text)-(min+max)/2)-Math.abs(count(b.text)-(min+max)/2));
      if(!selections.length) throw new Error(`No annotated selection ${age}/${level}/${key}`);
      const chosen=selections[0],id=uuid(`${age}:${level}:${key}`); map[id]=chosen.errors;
      content.push({id,exerciseId:row.id,ageGroupId:row.targetAgeGroupId,age,level,title:`Dikkatli editör — ${themes[key]} — ${ageLabels[age]} Seviye ${level}`,
        content:chosen.text,wordCount:count(chosen.text),errors:chosen.errors,source:path,sourceSha256:createHash('sha256').update(original).digest('hex')});
    }
    row.configuration={engineType:'error_analysis',difficultyLevel:level,engineConfig:{
      content:{source:'reading_text',minWordCount:min,maxWordCount:max},errorCount:oldConfig.errorCount,timeLimit:oldConfig.timeLimit,errorsByTextId:map}};
    settings.push({id:row.id,ageGroupId:row.targetAgeGroupId,age,level,title:row.title,previousConfiguration,configuration:row.configuration});
  }
  writeFileSync(catalogUrl,JSON.stringify(catalog,null,2)+'\n');
  const md=file(`../../${age}-exercises/v1/catalog.md`);
  let markdown=readFileSync(md,'utf8');
  for(const row of catalog.filter(row=>row.type==='ErrorAnalysis')) {
    const config=row.configuration.engineConfig;
    const section=new RegExp(`(## Seviye ${row.difficultyLevel}[^]*?)(?=## Seviye ${row.difficultyLevel+1}|$)`);
    markdown=markdown.replace(section,block=>block.replace(/^.*\| ErrorAnalysis \|.*$/m,
      `| ${row.title} | ErrorAnalysis | ${config.content.minWordCount}–${config.content.maxWordCount} kelime; ${config.errorCount} hazırlanmış hata; ${config.timeLimit} sn; 5 metin |`));
  }
  writeFileSync(md,markdown.trimEnd()+'\n');
}
const json=value=>JSON.stringify(value,null,2);
writeFileSync(file('./settings.json'),json(settings)+'\n'); writeFileSync(file('./content.json'),json(content)+'\n');
writeFileSync(file('./apply.sql'),readFileSync(file('./apply.template.sql'),'utf8').replace('{{SETTINGS}}',json(settings)).replace('{{CONTENT}}',json(content)));
console.log(`Generated ${settings.length} settings and ${content.length} prepared proofreading texts.`);
