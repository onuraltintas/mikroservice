import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { resolve, extname, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(fileURLToPath(new URL('../../../clients/admin-panel/dist/admin-panel/browser', import.meta.url)));
const types = { '.js': 'text/javascript', '.css': 'text/css', '.html': 'text/html', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2' };
createServer(async (request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1:4317');
  const path = resolve(root, '.' + decodeURIComponent(url.pathname));
  if (path !== root && !path.startsWith(root + sep)) { response.writeHead(403); response.end(); return; }
  const file = extname(path) ? path : resolve(root, 'index.csr.html');
  try { const content = await readFile(file); response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' }); response.end(content); }
  catch { response.writeHead(404); response.end(); }
}).listen(4317, '127.0.0.1');
