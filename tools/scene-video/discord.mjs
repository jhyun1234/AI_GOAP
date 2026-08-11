/* 디스코드 웹훅 포스터 — 의존성 0 (fetch 내장).
   웹훅 URL 은 `.discord-webhook`(gitignore). 파일이 없으면 조용히 넘어간다 —
   훅에서 불려도 워크플로우를 막지 않는다.

   사용:
     node discord.mjs "아무 문장"     한 줄 포스팅
     node discord.mjs --new           uploads.json 에서 url 이 생겼는데 아직 안 알린 회차를 알린다

   🔴 커밋마다 쏘지 않는다. 하루 10커밋이 나오는 리포라 그건 소식이 아니라 소음이다. */
import fs from 'fs';
import path from 'path';
import { ROOT } from './lib-node.mjs';

const HOOK = path.join(ROOT, '.discord-webhook');
const UPLOADS = path.join(ROOT, 'state', 'uploads.json');

async function post(content) {
  if (!fs.existsSync(HOOK)) { console.log('웹훅 파일 없음 — 건너뜀'); return false; }
  const url = fs.readFileSync(HOOK, 'utf8').trim();
  const r = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ content: content.slice(0, 2000) }),   // 디스코드 한 줄 상한
  });
  if (!r.ok) { console.error(`🔴 ${r.status} ${await r.text()}`); return false; }
  console.log('보냄:', content.split('\n')[0].slice(0, 60));
  return true;
}

const arg = process.argv.slice(2).join(' ').trim();

if (arg === '--new') {
  /* 사람이 업로드한 뒤 uploads.json 에 url 을 채우면 그때 알린다.
     같은 url 을 두 번 안 알리려고 announced 를 되쓴다 — 새 상태 파일을 만들지 않는다. */
  const st = JSON.parse(fs.readFileSync(UPLOADS, 'utf8'));
  const todo = Object.entries(st).filter(([, v]) => v.url && !v.announced);
  if (!todo.length) { console.log('알릴 새 회차 없음'); process.exit(0); }
  for (const [id, v] of todo) {
    if (await post(`**${v.title}**\n${v.url}`)) v.announced = new Date().toISOString();
  }
  fs.writeFileSync(UPLOADS, JSON.stringify(st, null, 2));
  console.log(`${todo.length}건 처리`);
} else if (arg) {
  await post(arg);
} else {
  console.log('사용: node discord.mjs "문장"  |  node discord.mjs --new');
}
