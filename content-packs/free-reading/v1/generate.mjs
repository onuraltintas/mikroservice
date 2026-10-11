import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';

const file = name => new URL(name, import.meta.url);
const read = name => JSON.parse(readFileSync(file(name), 'utf8'));
const words = text => text.trim().split(/\s+/u).length;
const uuid = key => {
  const hash = createHash('sha256').update(`free-reading-v1:${key}`).digest('hex');
  return `${hash.slice(0,8)}-${hash.slice(8,12)}-5${hash.slice(13,16)}-8${hash.slice(17,20)}-${hash.slice(20,32)}`;
};
const bands = {
  child: [[150,200,2,2],[200,275,3,3],[275,350,3,4],[350,450,4,5],[450,550,5,6]],
  teen: [[200,300,2,2],[300,400,3,3],[400,500,3,4],[500,650,4,5],[650,800,5,6]],
  'young-adult': [[300,400,2,3],[400,550,3,4],[550,700,4,5],[700,850,5,6],[850,1000,6,7]],
  adult: [[350,450,2,3],[450,600,3,4],[600,800,4,5],[800,1000,5,6],[1000,1200,6,7]]
};
const titles = { garden: 'Bir bahçede ortak gözlem', library: 'Kütüphaneyi birlikte düzenlemek', map: 'Yol gösteren harita',
  memory: 'Anıları birlikte derlemek', bridge: 'Köprüde ortak karar', exhibition: 'Nesnelerin hikâyelerini anlatmak',
  route: 'Okul girişini araştırmak', archive: 'Ortak bir arşiv kurmak', repair: 'Tamir atölyesinde kararlar' };
for (const key of ['garden','library','map']) {
  const original = readFileSync(file(`../../grouping/v1/texts/${key}.txt`),'utf8').trim().replace(/\r\n/g,'\n');
  const continuation = readFileSync(file(`./texts/${key}-continuation.txt`),'utf8').trim();
  writeFileSync(file(`./texts/${key}-long.txt`), `${original}\n\n${continuation}\n`);
}
const source = (age, key) => ['garden','library','map'].includes(key)
  ? ['child','teen'].includes(age) ? `../../grouping/v1/texts/${key}.txt` : `./texts/${key}-long.txt`
  : ['memory','bridge'].includes(key) ? `../../guided-reading/v1/texts/child-5-${key}.txt`
  : ['exhibition','route'].includes(key) ? `../../guided-reading/v1/texts/teen-5-${key}.txt`
  : `../../comprehension/v1/texts/${key === 'archive' ? 'community-archive' : 'repair-workshop'}.txt`;
const previous = existsSync(file('./settings.json')) ? read('./settings.json') : [];
const settings = [], content = [];
for (const [age, levels] of Object.entries(bands)) {
  const catalogUrl = file(`../../${age}-exercises/v1/catalog.json`);
  const catalog = JSON.parse(readFileSync(catalogUrl,'utf8'));
  const prototype = catalog.find(row => row.type === 'FreeReading');
  if (!prototype) throw new Error(`Missing ${age} FreeReading prototype`);
  for (let level=1; level<=5; level++) {
    let row = catalog.find(row => row.type === 'FreeReading' && row.difficultyLevel === level);
    const added = !row;
    if (added) {
      row = structuredClone(prototype);
      row.id = uuid(`${age}:exercise:${level}`);
      row.difficultyLevel = level;
      row.title = `Bağımsız Okuma Pratiği — Seviye ${level}`;
      row.description = 'Sorusuz bağımsız okuma pratiği. Tempo, okumayı bitirme beyanına dayanır; anlama ölçülmez.';
      catalog.push(row);
    }
    const saved = previous.find(item => item.id === row.id);
    const baseline = saved ? saved.previousConfiguration : added ? null : structuredClone(row.configuration);
    const [min,max,minMinutes,maxMinutes] = levels[level-1];
    row.configuration = { engineType:'free_reading', difficultyLevel:level, engineConfig: {
      content:{ source:'reading_text', minWordCount:min, maxWordCount:max }, mode:'free', readingPurpose:'practice',
      timing:{ minReadingTimeMs:10000, maxReadingTimeMs:0,
        suggestedMinReadingTimeMs:minMinutes*60000, suggestedMaxReadingTimeMs:maxMinutes*60000 }
    } };
    settings.push({ id:row.id, ageGroupId:row.targetAgeGroupId, age, level, title:row.title, description:row.description,
      previousConfiguration:baseline, configuration:row.configuration });
    const keys = ['garden','library','map', ...(age === 'child' ? ['memory','bridge'] : age === 'teen' ? ['exhibition','route'] : ['archive','repair'])];
    for (const key of keys) {
      const path = source(age,key);
      const original = readFileSync(file(path),'utf8').trim().replace(/\r\n/g,'\n');
      const paragraphs = original.split(/\n\s*\n/u);
      const candidates = paragraphs.map((_,index)=>paragraphs.slice(0,index+1).join('\n\n'))
        .filter(text=>words(text)>=min && words(text)<=max);
      candidates.sort((a,b)=>Math.abs(words(a)-(min+max)/2)-Math.abs(words(b)-(min+max)/2));
      if (!candidates.length) throw new Error(`No complete selection ${age}/${level}/${key}: source ${words(original)} words`);
      content.push({ id:uuid(`${age}:text:${level}:${key}`), exerciseId:row.id, ageGroupId:row.targetAgeGroupId, age, level,
        title:`${titles[key]} — ${age} S${level} seçkisi`, content:candidates[0], wordCount:words(candidates[0]), source:path,
        sourceSha256:createHash('sha256').update(original).digest('hex') });
    }
  }
  writeFileSync(catalogUrl, JSON.stringify(catalog,null,2)+'\n');
  const mdUrl=file(`../../${age}-exercises/v1/catalog.md`);
  let markdown=readFileSync(mdUrl,'utf8');
  const distribution=levels.map((_,i)=>catalog.filter(row=>row.difficultyLevel===i+1).length);
  markdown=markdown.replace(/toplam \d+ egzersiz\. Seviye dağılımı: [\d /]+\./,
    `toplam ${catalog.length} egzersiz. Seviye dağılımı: ${distribution.join(' / ')}.`)
    .replace('Anlama ve değerlendirmeli serbest okumada mevcut sorular;', 'Serbest Okuma sorusuz bağımsız pratiktir. Anlamada mevcut sorular;');
  for (let level=1;level<=5;level++) {
    const row=catalog.find(item=>item.type==='FreeReading' && item.difficultyLevel===level);
    const section=new RegExp(`(## Seviye ${level}[^]*?)(?=## Seviye ${level+1}|$)`);
    markdown=markdown.replace(section, block=> {
      const line=`| ${row.title} | FreeReading | \`${JSON.stringify(row.configuration.engineConfig)}\` |`;
      return block.includes('| FreeReading |') ? block.replace(/^.*\| FreeReading \|.*$/m,line) : block.trimEnd()+'\n'+line+'\n\n';
    });
  }
  writeFileSync(mdUrl,markdown.trimEnd()+'\n');
}
const json=data=>JSON.stringify(data,null,2);
writeFileSync(file('./settings.json'),json(settings)+'\n');
writeFileSync(file('./content.json'),json(content)+'\n');
writeFileSync(file('./apply.sql'),readFileSync(file('./apply.template.sql'),'utf8')
  .replace('{{SETTINGS}}',json(settings)).replace('{{CONTENT}}',json(content)));
console.log(`Generated ${settings.length} settings and ${content.length} question-free scoped selections.`);
