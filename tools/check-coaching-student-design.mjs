import { readFileSync,readdirSync } from 'node:fs';
import assert from 'node:assert/strict';

const root='clients/admin-panel/src/app/features/coaching-portal/';
const tokens=readFileSync(root+'layout/coaching-student-tokens.scss','utf8').split('.dark body');
const depth=readFileSync(root+'layout/coaching-workspace-depth.scss','utf8').split('.dark .coaching-shell');
const luminance=colour=>{
  let hex=colour.slice(1);if(hex.length===3)hex=[...hex].map(x=>x+x).join('');
  const rgb=[0,2,4].map(i=>parseInt(hex.slice(i,i+2),16)/255).map(c=>c<=.04045?c/12.92:((c+.055)/1.055)**2.4);
  return .2126*rgb[0]+.7152*rgb[1]+.0722*rgb[2];
};
for(const [index,text]of tokens.entries()) {
  const vars=Object.fromEntries([...(text+depth[index]).matchAll(/(--[\w-]+):\s*(#[\da-f]{3,6})\s*;/gi)].map(m=>[m[1],m[2]]));
  const pairs=[['--ui-text','--ui-surface'],['--ui-text-muted','--ui-surface'],['--ui-text-muted','--ui-page'],['--ui-success','--ui-brand-soft'],['--ui-danger','--coaching-danger-soft'],['--coaching-warning','--coaching-warm'],['#fff','--ui-brand'],...Array.from({length:5},(_,i)=>['--coaching-lesson-text-'+i,'--coaching-lesson-'+i]),...['mint','honey','blue'].flatMap(tint=>['--ui-text','--ui-text-muted'].map(text=>[text,'--coaching-'+tint+'-tint']))];
  for(const [fg,bg]of pairs){const a=luminance(vars[fg]??fg),b=luminance(vars[bg]??bg),ratio=(Math.max(a,b)+.05)/(Math.min(a,b)+.05);assert.ok(ratio>=4.5,`${index?'Dark':'Light'} ${fg}/${bg} contrast ${ratio.toFixed(2)}`);}
}
const files=readdirSync(root+'pages').filter(n=>n.endsWith('.component.ts')).map(n=>root+'pages/'+n).concat('clients/admin-panel/shared/coaching-profile.component.ts');
for(const file of files){const s=readFileSync(file,'utf8');assert.ok(!/window\.(alert|confirm|prompt)\s*\(/.test(s),file+' uses a browser dialog');assert.ok(!s.includes('window:beforeunload'),file+' registers a browser unload dialog');}
console.log('Light/dark text, state and lesson colours pass 4.5:1 contrast checks; student components use no browser dialogs.');
