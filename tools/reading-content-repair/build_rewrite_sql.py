"""Build apply/rollback SQL for the reading content repair v2.

Questions: guarded by the exported (live) values, so a row edited since the export is skipped.
Texts: guarded by the exported content; word_count recomputed with the domain rule; version + 1.
"""
import json,sys,hashlib
RC=sys.argv[1] if len(sys.argv) > 1 else '.'  # folder holding reading-*-20261118.jsonl exports and out/
def lit(s):
    if s is None: return 'NULL'
    return "'"+str(s).replace("'","''")+"'"
Q={q['id']:q for q in map(json.loads,open(f'{RC}/reading-questions-20261118.jsonl',encoding='utf-8'))}
T={t['id']:t for t in map(json.loads,open(f'{RC}/reading-texts-20261118.jsonl',encoding='utf-8'))}
pack=[json.loads(l) for l in open(f'{RC}/out/pack-final.jsonl',encoding='utf-8')]
texts=[json.loads(l) for l in open(f'{RC}/out/texts-final.jsonl',encoding='utf-8')]
def wc(s): return sum(1 for tok in s.split() if any(ch.isalnum() for ch in tok))
cols=['question_text','option_a','option_b','option_c','option_d','correct_answer','explanation']
def rows_q(new):
    out=[]
    for r in pack:
        q=Q[r['id']]; old={c:q[c] for c in cols}; nv={c:r.get(c,q[c]) for c in cols}
        a,b=(old,nv) if new else (nv,old)
        out.append("('"+r['id']+"'::uuid,"+",".join(lit(a[c]) for c in cols)+","+",".join(lit(b[c]) for c in cols)+")")
    return out
def rows_t(new):
    out=[]
    for r in texts:
        old=T[r['id']]['content']; nv=r['content']
        a,b=(old,nv) if new else (nv,old)
        out.append("('"+r['id']+"'::uuid,"+lit(a)+","+lit(b)+","+str(wc(b))+")")
    return out
def script(new):
    name='apply' if new else 'rollback'
    L=[f"-- Reading content repair v2 ({name}), 18.11.2026. Database: speedreading_owned_db.",
       "-- Each row is changed only if it still holds the expected current values; otherwise it is skipped and counted.",
       "\\set ON_ERROR_STOP on","BEGIN;",
       "CREATE TEMP TABLE rq (id uuid PRIMARY KEY, "+", ".join(f"cur_{c} text" for c in cols)+", "+", ".join(f"new_{c} text" for c in cols)+") ON COMMIT DROP;",
       "INSERT INTO rq VALUES\n"+",\n".join(rows_q(new))+";",
       "CREATE TEMP TABLE rt (id uuid PRIMARY KEY, cur_content text, new_content text, new_word_count int) ON COMMIT DROP;"]
    if texts: L.append("INSERT INTO rt VALUES\n"+",\n".join(rows_t(new))+";")
    L+=["WITH u AS (UPDATE speed_reading.reading_questions q SET "+", ".join(f"{c} = rq.new_{c}" for c in cols)+", updated_at = now()",
        "  FROM rq WHERE q.id = rq.id AND NOT q.is_deleted AND "+" AND ".join(f"q.{c} IS NOT DISTINCT FROM rq.cur_{c}" for c in cols)+" RETURNING 1)",
        "SELECT 'questions updated' AS what, count(*) FROM u;",
        "SELECT 'questions skipped (changed since export or deleted)' AS what, count(*) FROM rq JOIN speed_reading.reading_questions q ON q.id = rq.id WHERE NOT ("+" AND ".join(f"q.{c} IS NOT DISTINCT FROM rq.new_{c}" for c in cols)+");",
        "WITH u AS (UPDATE speed_reading.reading_texts t SET content = rt.new_content, word_count = rt.new_word_count, version = t.version + 1, updated_at = now()",
        "  FROM rt WHERE t.id = rt.id AND NOT t.is_deleted AND t.content = rt.cur_content RETURNING 1)",
        "SELECT 'texts updated' AS what, count(*) FROM u;",
        "SELECT 'texts skipped' AS what, count(*) FROM rt JOIN speed_reading.reading_texts t ON t.id = rt.id WHERE t.content <> rt.new_content;",
        "COMMIT;"]
    p=f'{RC}/out/reading-content-repair-v2.{name}.sql'
    open(p,'w',encoding='utf-8').write("\n".join(L)+"\n")
    print(p, len(pack),'questions', len(texts),'texts', hashlib.sha256(open(p,'rb').read()).hexdigest())
script(True); script(False)
