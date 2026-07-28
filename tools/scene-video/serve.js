/* 미리보기용 정적 서버.
   ES 모듈(kind 동적 import)은 file:// 에서 막히므로 반드시 http 로 띄운다.
   사용: node tools/scene-video/serve.js  →  http://localhost:4173/engine/?ep=ep01 */
const http = require('http');
const fs = require('fs');
const path = require('path');

const ROOT = __dirname;
const PORT = Number(process.env.PORT) || 4173;   // 두 번째 미리보기를 나란히 띄울 때만 바꾼다
const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.woff2': 'font/woff2', '.png': 'image/png', '.mp3': 'audio/mpeg', '.wav': 'audio/wav'
};

http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p === '/') p = '/engine/index.html';
  if (p.endsWith('/')) p += 'index.html';
  const file = path.join(ROOT, p);
  if (!file.startsWith(ROOT)) { res.writeHead(403).end(); return; }
  fs.readFile(file, (err, buf) => {
    if (err) { res.writeHead(404, { 'content-type': 'text/plain' }).end('404 ' + p); return; }
    res.writeHead(200, {
      'content-type': TYPES[path.extname(file)] || 'application/octet-stream',
      'cache-control': 'no-store'
    });
    res.end(buf);
  });
}).listen(PORT, () => console.log(`scene-video  →  http://localhost:${PORT}/engine/?ep=ep01`));
