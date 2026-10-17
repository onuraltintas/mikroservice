import {createServer} from 'node:http';
import {readFile} from 'node:fs/promises';
import {resolve,extname,sep} from 'node:path';
import {fileURLToPath} from 'node:url';
const types={'.js':'text/javascript','.css':'text/css','.html':'text/html','.svg':'image/svg+xml','.png':'image/png','.woff2':'font/woff2'};
for(const [app,port,index] of [['admin-panel',4317,'index.csr.html'],['staff-portal',4318,'index.html']]){
  const root=fileURLToPath(new URL(`../../../clients/admin-panel/dist/${app}/browser/`,import.meta.url));
  createServer(async(req,res)=>{const urlPath=decodeURIComponent(new URL(req.url,`http://127.0.0.1:${port}`).pathname),publicPath=app==='staff-portal'?urlPath.replace(/^\/staff\//,'/'):urlPath;const path=resolve(root,'.'+publicPath);if(path!==resolve(root)&&!path.startsWith(resolve(root)+sep)){res.writeHead(403);res.end();return;}const file=extname(path)?path:resolve(root,index);try{const content=await readFile(file);res.writeHead(200,{'content-type':types[extname(file)]??'application/octet-stream'});res.end(content);}catch{res.writeHead(404);res.end();}}).listen(port,'127.0.0.1');
}
