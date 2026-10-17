import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../../artifacts/coaching-weekly-outcomes-live-20261017/admin-source/clients/admin-panel/dist/admin-panel/browser/',import.meta.url));
const types={'.js':'text/javascript','.css':'text/css','.html':'text/html','.svg':'image/svg+xml','.png':'image/png','.woff2':'font/woff2'};
createServer(async(req,res)=>{const path=resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://127.0.0.1:4317').pathname));if(path!==resolve(root)&&!path.startsWith(resolve(root)+sep)){res.writeHead(403);res.end();return;}const file=extname(path)?path:resolve(root,'index.csr.html');try{const content=await readFile(file);res.writeHead(200,{'content-type':types[extname(file)]??'application/octet-stream'});res.end(content);}catch{res.writeHead(404);res.end();}}).listen(4317,'127.0.0.1');
