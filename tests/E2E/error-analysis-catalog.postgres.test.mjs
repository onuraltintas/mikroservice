import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

const settings=JSON.parse(readFileSync(new URL('../../content-packs/error-analysis/v1/settings.json',import.meta.url)));
const content=JSON.parse(readFileSync(new URL('../../content-packs/error-analysis/v1/content.json',import.meta.url)));
const template=readFileSync(new URL('../../content-packs/error-analysis/v1/apply.template.sql',import.meta.url),'utf8');
const apply=readFileSync(new URL('../../content-packs/error-analysis/v1/apply.sql',import.meta.url),'utf8');
const typeId='30000000-0000-0000-0000-000000000001';
let container;
const docker=args=>spawnSync('docker',args,{encoding:'utf8',timeout:60000});
const sql=(input,success=true)=> {
  const result=spawnSync('docker',['exec','-i',container,'psql','-X','-qAt','-v','ON_ERROR_STOP=1','-U','postgres'],{input,encoding:'utf8',timeout:60000});
  if(success) assert.equal(result.status,0,result.stderr||result.error?.message);
  return result;
};
before(async()=>{
  const started=docker(['run','-d','--name',`error-analysis-test-${process.pid}-${Date.now()}`,'-e','POSTGRES_HOST_AUTH_METHOD=trust','postgres:16-alpine']);
  assert.equal(started.status,0,started.stderr||started.error?.message);
  container=started.stdout.trim(); assert.match(container,/^[0-9a-f]{64}$/);
  for(let i=0;i<150;i++) {
    if(docker(['exec',container,'pg_isready','-U','postgres']).status===0) return;
    await new Promise(resolve=>setTimeout(resolve,200));
  }
  throw new Error('Disposable PostgreSQL did not become ready');
});
after(()=>{if(container) assert.equal(docker(['rm','-fv',container]).status,0);});
function seed() {
  sql(`DROP SCHEMA IF EXISTS speed_reading CASCADE; CREATE SCHEMA speed_reading;
    CREATE TABLE speed_reading.exercise_types(id uuid PRIMARY KEY,name text,engine_type text,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.exercise_types VALUES('${typeId}','ErrorAnalysis','error_analysis',true,false);
    CREATE TABLE speed_reading.exercises(id uuid PRIMARY KEY,exercise_type_id uuid,target_age_group_id uuid,difficulty_level int,
      configuration_json jsonb,is_active boolean DEFAULT true,is_deleted boolean DEFAULT false,version int DEFAULT 1,
      updated_at timestamptz,updated_by text);
    INSERT INTO speed_reading.exercises(id,exercise_type_id,target_age_group_id,difficulty_level,configuration_json)
      SELECT (s->>'id')::uuid,'${typeId}',(s->>'ageGroupId')::uuid,(s->>'level')::int,s->'previousConfiguration'
      FROM jsonb_array_elements($settings$${JSON.stringify(settings)}$settings$::jsonb) s;
    CREATE TABLE speed_reading.reading_texts(id uuid PRIMARY KEY,title text,content text,word_count int,category text,difficulty_level int,
      target_age_group_id uuid,language text,is_active boolean,tags text,recommended_min_level int,recommended_max_level int,
      average_comprehension_score numeric,times_read int,average_reading_time_seconds int,exercise_id uuid,created_at timestamptz,
      created_by text,version int,is_deleted boolean);
    CREATE TABLE speed_reading.reading_questions(id int,reading_text_id uuid,is_deleted boolean DEFAULT false);
    INSERT INTO speed_reading.reading_questions VALUES(1,'00000000-0000-0000-0000-000000000001',false);
    CREATE TABLE speed_reading.session_snapshots(id int,configuration_json jsonb);
    INSERT INTO speed_reading.session_snapshots VALUES(1,$snapshot$${JSON.stringify(settings[0].previousConfiguration)}$snapshot$::jsonb);`);
}
const snapshot=()=>sql(`SELECT jsonb_build_object('exercises',(SELECT jsonb_agg(to_jsonb(e) ORDER BY id) FROM speed_reading.exercises e),
  'texts',(SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM speed_reading.reading_texts t),
  'questions',(SELECT jsonb_agg(to_jsonb(q) ORDER BY id) FROM speed_reading.reading_questions q),
  'sessions',(SELECT jsonb_agg(to_jsonb(s) ORDER BY id) FROM speed_reading.session_snapshots s));`).stdout;
const render=(s=settings,c=content)=>template.replace('{{SETTINGS}}',JSON.stringify(s,null,2)).replace('{{CONTENT}}',JSON.stringify(c,null,2));
test('rendered SQL matches verified content and settings',()=>assert.equal(apply,render()));
test('eight updates and forty scoped texts preserve questions/history and are idempotent',()=> {
  seed(); const history=sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout;
  sql(apply); const first=snapshot();
  assert.equal(sql('SELECT count(*) FROM speed_reading.exercises').stdout.trim(),'8');
  assert.equal(sql('SELECT count(*) FROM speed_reading.reading_texts').stdout.trim(),'40');
  assert.equal(sql('SELECT count(*) FROM speed_reading.reading_questions').stdout.trim(),'1');
  assert.equal(sql('SELECT configuration_json FROM speed_reading.session_snapshots').stdout,history);
  sql(apply); assert.equal(snapshot(),first);
});
for(const [name,mutation] of [
  ['missing original exercise',`DELETE FROM speed_reading.exercises WHERE id='${settings[0].id}'`],
  ['config drift',`UPDATE speed_reading.exercises SET configuration_json='{}' WHERE id='${settings[0].id}'`],
  ['wrong age',`UPDATE speed_reading.exercises SET target_age_group_id=NULL WHERE id='${settings[0].id}'`],
  ['inactive type',`UPDATE speed_reading.exercise_types SET is_active=false`],
  ['text ID collision',`INSERT INTO speed_reading.reading_texts(id,content) VALUES('${content[0].id}','different')`]
]) test(`${name} aborts entire migration`,()=>{
  seed(); sql(mutation); const before=snapshot(); const failed=sql(apply,false);
  assert.equal(failed.status,3,failed.stderr); assert.match(failed.stderr,/Proofreading/); assert.equal(snapshot(),before);
});
for(const kind of ['index','original','duplicate','count','whitespace']) test(`invalid annotation ${kind} aborts migration`,()=> {
  seed(); const before=snapshot(), s=structuredClone(settings), c=structuredClone(content);
  const errors=c[0].errors;
  if(kind==='index') errors[0].wordIndex=c[0].wordCount;
  if(kind==='original') errors[0].originalWord='changed';
  if(kind==='duplicate') errors[1]=structuredClone(errors[0]);
  if(kind==='count') errors.pop();
  if(kind==='whitespace') errors[0].errorWord='two words';
  s.find(row=>row.id===c[0].exerciseId).configuration.engineConfig.errorsByTextId[c[0].id]=errors;
  const failed=sql(render(s,c),false);
  assert.equal(failed.status,3,failed.stderr); assert.match(failed.stderr,/Proofreading/); assert.equal(snapshot(),before);
});
