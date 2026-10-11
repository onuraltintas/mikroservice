import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const root=new URL('../../content-packs/rsvp/v2/',import.meta.url);
const settings=JSON.parse(readFileSync(new URL('settings.json',root)));
const content=JSON.parse(readFileSync(new URL('content.json',root)));
const template=readFileSync(new URL('apply.template.sql',root),'utf8');
const apply=readFileSync(new URL('apply.sql',root),'utf8');
const typeId='30000000-0000-0000-0000-000000000001';
let container;
const docker=args=>spawnSync('docker',args,{encoding:'utf8',timeout:60000});
const sql=(input,success=true)=>{
  const result=spawnSync('docker',['exec','-i',container,'psql','-X','-qAt','-v','ON_ERROR_STOP=1','-U','postgres'],{input,encoding:'utf8',timeout:60000});
  if(success) assert.equal(result.status,0,result.stderr||result.error?.message);return result;
};
before(async()=>{
  const started=docker(['run','-d','--name',`rsvp-test-${process.pid}-${Date.now()}`,'-e','POSTGRES_HOST_AUTH_METHOD=trust','postgres:16-alpine']);
  assert.equal(started.status,0,started.stderr);container=started.stdout.trim();assert.match(container,/^[0-9a-f]{64}$/);
  for(let i=0;i<150;i++){if(docker(['exec',container,'pg_isready','-U','postgres']).status===0)return;
    await new Promise(resolve=>setTimeout(resolve,200));}throw new Error('Disposable PostgreSQL not ready');
});
after(()=>{if(container) assert.equal(docker(['rm','-fv',container]).status,0);});
function seed(){
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE;CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,engine_type text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.exercise_types VALUES('${typeId}','RSVP','text_stream',true,false);
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,difficulty_level int,
      configuration_json jsonb,title text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,version int DEFAULT 1,updated_at timestamptz,updated_by text);
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json,title)
      SELECT (s->>'id')::uuid,'${typeId}',(s->>'ageGroupId')::uuid,(s->>'level')::int,s->'previousConfiguration',s->>'previousTitle'
      FROM jsonb_array_elements($settings$${JSON.stringify(settings)}$settings$::jsonb)s;
    CREATE TABLE speed_reading.reading_texts(id uuid PRIMARY KEY,title text,content text,word_count int,category text,difficulty_level int,
      target_age_group_id uuid,language text,is_active boolean,tags text,recommended_min_level int,recommended_max_level int,
      average_comprehension_score numeric,times_read int,average_reading_time_seconds int,exercise_id uuid,created_at timestamptz,created_by text,version int,is_deleted boolean);
    CREATE TABLE speed_reading.reading_questions(id uuid PRIMARY KEY,reading_text_id uuid,question_text text,type int,bloom_level int,difficulty_level int,
      explanation text,option_a text,option_b text,option_c text,option_d text,correct_answer text,order_index int,created_at timestamptz,created_by text,is_deleted boolean);
    CREATE TABLE speed_reading.session_snapshots(id int,configuration_json jsonb);
    INSERT INTO speed_reading.session_snapshots VALUES(1,$snapshot$${JSON.stringify(settings[0].previousConfiguration)}$snapshot$::jsonb);`);
}
const snapshot=()=>sql(`SELECT jsonb_build_object('exercises',(SELECT jsonb_agg(to_jsonb(e)ORDER BY id)FROM speed_reading.exercises e),
  'texts',(SELECT jsonb_agg(to_jsonb(t)ORDER BY id)FROM speed_reading.reading_texts t),
  'questions',(SELECT jsonb_agg(to_jsonb(q)ORDER BY id)FROM speed_reading.reading_questions q),
  'sessions',(SELECT jsonb_agg(to_jsonb(s)ORDER BY id)FROM speed_reading.session_snapshots s));`).stdout;
const render=(s=settings,c=content)=>template.replace('{{SETTINGS}}',JSON.stringify(s,null,2)).replace('{{CONTENT}}',JSON.stringify(c,null,2));
test('rendered SQL matches settings/content',()=>assert.equal(apply,render()));
test('real schema accepts eleven updates, twenty-two texts and110 questions; snapshots survive and second run is inert',()=>{
  seed();const old=sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout;
  sql(apply);const first=snapshot();
  assert.equal(sql('SELECT count(*) FROM speed_reading.reading_texts').stdout.trim(),'22');
  assert.equal(sql('SELECT count(*) FROM speed_reading.reading_questions').stdout.trim(),'110');
  assert.equal(sql('SELECT count(*) FROM speed_reading.exercises WHERE version=2').stdout.trim(),'11');
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout,old);
  sql(apply);assert.equal(snapshot(),first);
});
for(const [name,mutation]of[
  ['missing exercise',`DELETE FROM speed_reading.exercises WHERE id='${settings[0].id}'`],
  ['baseline drift',`UPDATE speed_reading.exercises SET configuration_json='{}' WHERE id='${settings[0].id}'`],
  ['inactive type','UPDATE speed_reading.exercise_types SET is_active=false'],
  ['text collision',`INSERT INTO speed_reading.reading_texts(id,content)VALUES('${content[0].id}','edited')`]
])test(`${name} aborts without partial changes`,()=>{
  seed();sql(mutation);const before=snapshot();const result=sql(apply,false);assert.equal(result.status,3);assert.equal(snapshot(),before);
});
for(const kind of ['evidence','answer','missingQuestion','timing','missingBand'])test(`${kind} payload corruption aborts atomically`,()=>{
  seed();const before=snapshot(),s=structuredClone(settings),c=structuredClone(content);
  if(kind==='evidence')c[0].questions[0].evidence='not present';
  if(kind==='answer')c[0].questions[0].correctAnswer='E';
  if(kind==='missingQuestion')c[0].questions.pop();
  if(kind==='timing')s[0].configuration.engineConfig.timing.durationMs=1;
  if(kind==='missingBand')delete s[0].configuration.engineConfig.content.minWordCount;
  const result=sql(render(s,c),false);assert.equal(result.status,3,result.stderr);assert.match(result.stderr,/RSVP/);assert.equal(snapshot(),before);
});
