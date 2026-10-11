import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const file = name => new URL(name,import.meta.url);
const read = name => JSON.parse(readFileSync(file(name),'utf8'));
const uuid = key => {
  const hash=createHash('sha256').update(`rsvp-v2:${key}`).digest('hex');
  return `${hash.slice(0,8)}-${hash.slice(8,12)}-5${hash.slice(13,16)}-8${hash.slice(17,20)}-${hash.slice(20,32)}`;
};
const bands={child:{4:[180,240],5:[240,300]},teen:{3:[360,440],4:[450,540],5:[560,680]},
  'young-adult':{3:[580,640],4:[680,750],5:[770,850]},adult:{3:[480,540],4:[580,650],5:[690,800]}};
const ages={child:'Çocuk',teen:'Genç','young-adult':'Genç yetişkin',adult:'Yetişkin'};
const source=read('../../comprehension/v1/content.json');
const previous=existsSync(file('settings.json')) ? read('settings.json') : [];
const settings=[],content=[];
for(const [age,levels] of Object.entries(bands)) {
  const catalogUrl=file(`../../${age}-exercises/v1/catalog.json`);
  const catalog=JSON.parse(readFileSync(catalogUrl,'utf8'));
  for(const [levelString,[min,max]] of Object.entries(levels)) {
    const level=Number(levelString),row=catalog.find(item=>item.type==='RSVP'&&item.difficultyLevel===level);
    if(!row) throw new Error(`Missing RSVP cell ${age}/${level}`);
    const saved=previous.find(item=>item.id===row.id);
    const baseline=saved ? saved.previousConfiguration : structuredClone(row.configuration);
    const duration=baseline.engineConfig.timing.durationMs;
    row.configuration={engineType:'text_stream',difficultyLevel:level,engineConfig:{mode:'rsvp',readingPurpose:'evaluation',
      content:{source:'reading_text',minWordCount:min,maxWordCount:max,questionCount:5},
      timing:{durationMs:duration,intervalMs:0},visuals:{showFixation:true},adaptive:{enabled:false}}};
    settings.push({id:row.id,ageGroupId:row.targetAgeGroupId,age,level,title:row.title,
      previousTitle:saved?.previousTitle??row.title,previousConfiguration:baseline,configuration:row.configuration});
    const unique=new Map();
    for(const text of source) if(text.ageGroupId===row.targetAgeGroupId && text.wordCount>=min && text.wordCount<=max && text.questions.length>=5)
      if(!unique.has(text.content)) unique.set(text.content,text);
    const selections=[...unique.values()].slice(0,2);
    if(selections.length!==2) throw new Error(`Need two distinct evidenced selections ${age}/${level}`);
    for(const [index,text] of selections.entries()) {
      const questions=text.questions.slice(0,5).map((q,position)=>({...q,id:uuid(`${age}:${level}:${index}:question:${position}`),orderIndex:position}));
      if(questions.some(q=>!text.content.includes(q.evidence))) throw new Error('Question evidence missing');
      const title=text.title.split(' — ')[0];
      content.push({id:uuid(`${age}:${level}:text:${index}`),exerciseId:row.id,ageGroupId:row.targetAgeGroupId,age,level,
        title:`${title} — ${ages[age]} Seviye ${level} seri sunum`,content:text.content,wordCount:text.wordCount,
        sourceTextId:text.id,questions});
    }
  }
  writeFileSync(catalogUrl,JSON.stringify(catalog,null,2)+'\n');
  const markdownUrl=file(`../../${age}-exercises/v1/catalog.md`);
  let markdown=readFileSync(markdownUrl,'utf8');
  for(const level of Object.keys(levels).map(Number)) {
    const row=catalog.find(item=>item.type==='RSVP'&&item.difficultyLevel===level);
    const line=age==='child' ? `| RSVP | ${level} | \`${JSON.stringify(row.configuration.engineConfig)}\` |`
      : `| ${row.title} | RSVP | \`${JSON.stringify(row.configuration.engineConfig)}\` |`;
    const section=age==='child' ? new RegExp(`^\\| RSVP \\| ${level} \\|.*$`,'m')
      : new RegExp(`(## Seviye ${level}[^]*?)(?=## Seviye ${level+1}|$)`);
    markdown=age==='child' ? markdown.replace(section,line)
      : markdown.replace(section,block=>block.replace(/^.*\| RSVP \|.*$/m,line));
  }
  writeFileSync(markdownUrl,markdown);
}
const json=data=>JSON.stringify(data,null,2);
writeFileSync(file('settings.json'),json(settings)+'\n');
writeFileSync(file('content.json'),json(content)+'\n');
writeFileSync(file('apply.sql'),readFileSync(file('apply.template.sql'),'utf8').replace('{{SETTINGS}}',json(settings)).replace('{{CONTENT}}',json(content)));
console.log(`Generated ${settings.length} settings, ${content.length} texts and ${content.length*5} evidenced questions.`);
