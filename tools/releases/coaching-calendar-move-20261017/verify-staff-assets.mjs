import {readFileSync,existsSync} from 'node:fs';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const root=resolve(process.argv[2]),html=readFileSync(resolve(root,'index.html'),'utf8');
const base=html.match(/<base href="([^"]+)"/)?.[1];
assert.equal(base,'/staff/','Staff asset URLs must remain under the configured public /staff/ route.');
const origin='https://onuraltintas.net',urls=[...html.matchAll(/(?:src|href)="([^"]+\.(?:js|css))"/g)].map(x=>new URL(x[1],origin+base));
assert.ok(urls.length>1);
for(const url of urls){assert.equal(url.origin,origin);assert.ok(url.pathname.startsWith('/staff/'));assert.ok(existsSync(resolve(root,url.pathname.slice('/staff/'.length))),url.href);}
console.log(`Staff public prefix and ${urls.length} entry assets verified.`);
