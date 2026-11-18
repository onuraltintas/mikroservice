"""Remove leftover Markdown markers from reading texts; word count (tokens with a letter or digit) must not change."""
import json,re,sys
def wc(s): return sum(1 for tok in s.split() if any(ch.isalnum() for ch in tok))
def clean(c):
    lines=[]
    for line in c.split('\n'):
        s=line.rstrip()
        if re.fullmatch(r'\s*(-{3,}|\*{3,}|_{3,})\s*',s): lines.append(''); continue
        s=re.sub(r'^(\s*)#{1,6}\s+',r'\1',s)
        s=s.replace('**','')
        s=re.sub(r'(?<![\w*])\*(?=\S)([^*\n]+?)(?<=\S)\*(?![\w*])',r'\1',s)
        lines.append(s)
    out='\n'.join(lines)
    out=re.sub(r'\n{3,}','\n\n',out).strip()
    return out
if __name__=='__main__':
    src,dst=sys.argv[1],sys.argv[2]
    n=0;bad=[]
    with open(dst,'w',encoding='utf-8') as f:
        for l in open(src,encoding='utf-8'):
            t=json.loads(l); new=clean(t['content'])
            if new!=t['content'].strip() or new!=t['content']:
                if wc(new)!=wc(t['content']): bad.append((t['id'],wc(t['content']),wc(new)))
                f.write(json.dumps({'id':t['id'],'content':new,'old_content':t['content'],'word_count':t['word_count']},ensure_ascii=False)+'\n'); n+=1
    print('changed',n,'word-count mismatches',len(bad),bad[:5])
