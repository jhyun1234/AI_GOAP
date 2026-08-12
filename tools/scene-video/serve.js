/* 미리보기용 정적 서버.
   ES 모듈(kind 동적 import)은 file:// 에서 막히므로 반드시 http 로 띄운다.
   사용: node tools/scene-video/serve.js  →  http://localhost:4173/engine/?ep=ep01 */
const http = require('http');
const fs = require('fs');
const path = require('path');

const ROOT = __dirname;
const PORT = Number(process.env.PORT) || 4173;   // 두 번째 미리보기를 나란히 띄울 때만 바꾼다
// 게임 클립 라이브러리 (롱폼 W5) — 리포 밖 D: 에 산다 (용량·공개 리포 문제로 복사 금지)
const CLIPS = path.join(process.env.SCENE_CLIPS_ROOT || 'D:\\AI_GOAP-videos\\clips', 'library');
const TYPES = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.json': 'application/json; charset=utf-8',
  '.woff2': 'font/woff2', '.png': 'image/png', '.mp3': 'audio/mpeg', '.wav': 'audio/wav',
  '.mp4': 'video/mp4'
};

http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  if (p === '/') p = '/engine/index.html';
  if (p.endsWith('/')) p += 'index.html';
  const base = p.startsWith('/clips/') ? CLIPS : ROOT;
  const file = path.join(base, p.startsWith('/clips/') ? p.slice(7) : p);
  if (!file.startsWith(base)) { res.writeHead(403).end(); return; }
  fs.readFile(file, (err, buf) => {
    if (err) { res.writeHead(404, { 'content-type': 'text/plain' }).end('404 ' + p); return; }
    const type = TYPES[path.extname(file)] || 'application/octet-stream';
    /* 🔴 Range 지원 (롱폼 W5 수리, 2026-08-09) — HTML5 비디오 시킹은 Range 가 필수다.
       없으면 크로미움이 seekable=[0,0] 인 스트림으로 취급해 currentTime 할당이 전부
       0 으로 클램프되고, **클립 8샷이 전부 정지 사진으로 렌더된다** (검수가 잡았다 —
       엔진 스틸이 시각과 무관하게 비트 동일 · PSNR inf). */
    const range = /^bytes=(\d*)-(\d*)$/.exec(req.headers.range || '');
    if (range && (range[1] || range[2])) {
      const start = range[1] ? Number(range[1]) : Math.max(0, buf.length - Number(range[2]));
      const end = range[1] && range[2] ? Math.min(Number(range[2]), buf.length - 1) : buf.length - 1;
      if (start > end || start >= buf.length) {
        res.writeHead(416, { 'content-range': `bytes */${buf.length}` }).end(); return;
      }
      res.writeHead(206, {
        'content-type': type, 'cache-control': 'no-store',
        'accept-ranges': 'bytes',
        'content-range': `bytes ${start}-${end}/${buf.length}`,
        'content-length': end - start + 1,
      });
      res.end(buf.subarray(start, end + 1));
      return;
    }
    res.writeHead(200, {
      'content-type': type,
      'cache-control': 'no-store',
      'accept-ranges': 'bytes'
    });
    res.end(buf);
  });
}).listen(PORT, () => console.log(`scene-video  →  http://localhost:${PORT}/engine/?ep=ep01`));
