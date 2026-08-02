/* 재료가 남았는가 — 다음 회차를 정하고, 순서표가 바닥나면 새 글로 늘린다.

   사용:
     node tools/scene-video/backlog.mjs            보고만 한다 (읽기 전용)
     node tools/scene-video/backlog.mjs --extend   재료가 있으면 schedule.json 에 회차 한 칸을 늘린다
     node tools/scene-video/backlog.mjs --json     판정을 stdout 에 한 줄 JSON 으로도 낸다
                                                   (사람이 읽는 줄은 언제나 stderr 로 간다)

   🔴 왜 이 파일이 생겼나 (2026-08-02, 사용자 지시로 격일 → 매일 전환).
   `schedule.json` 의 `order` 는 **손으로 적어 둔 14칸짜리 고정 목록**이었다. 격일일 때는
   한 달치라 안 보였는데, 매일로 바꾸면 열흘 남짓 뒤에 바닥난다. 그러면 기획팀이
   "구성안 표에 다음 영상 행이 없다"로 SKIP 을 내고 — **그 뒤로 영원히 SKIP 한다.**
   블로그가 새 글을 계속 발행해도 아무도 순서표에 줄을 안 넣기 때문이다.

   그래서 규칙을 이렇게 바꾼다:
     ① `order` 안에 아직 대본 없는 회차가 있으면 그것. (구성안이 정한 순서 = 그대로 정본)
     ② 다 만들었으면 **발행된 블로그 글 중 아직 영상이 안 된 것**을 발행 순서로 하나 집는다.
     ③ 그것도 없으면 **아무것도 하지 않는다.** 에러가 아니다 — 재료가 없는 것뿐이고,
        블로그가 다음 글을 발행하면 다음 실행이 알아서 ②로 돌아온다.

   🔑 한 번에 **한 칸만** 늘린다. 매일 하나가 규칙이므로 재료가 셋 밀려 있어도 사흘에 나눠 간다.

   ⚠️ 이 스크립트는 회차를 만들지 않는다. `schedule.json` 에 줄을 넣을 뿐이다.
      대본은 scene-* 에이전트가, 렌더는 publish.mjs 가 한다. */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { epScene } from './lib-node.mjs';

const ROOT = path.dirname(fileURLToPath(import.meta.url));
const REPO = path.resolve(ROOT, '..', '..');
/* 순서표 경로를 환경변수로 갈아 끼울 수 있게 열어 둔다.
   🔑 이유는 편의가 아니라 **시험 가능성**이다. 이 스크립트에서 정작 중요한 갈래는
   "순서표가 바닥났을 때"인데, 실제 순서표에는 늘 남은 회차가 있어서 그 갈래가 안 돌아 본다.
   그러면 바닥나는 날 처음으로 돌게 되고, 그날은 아무도 안 보고 있다. */
const SCHED = process.env.SCENE_SCHEDULE || path.join(ROOT, 'state', 'schedule.json');
const PUBLISHED = path.join(REPO, 'tools', 'blog-automation', 'published');
const COVERAGE = path.join(REPO, 'tools', 'blog-automation', 'BLOG_COVERAGE.md');
const LASTPUB = path.join(REPO, 'tools', 'blog-automation', 'state', 'blog_last_published_commit.md');

const argv = process.argv.slice(2);
const EXTEND = argv.includes('--extend');
const JSON_OUT = argv.includes('--json');

const sched = JSON.parse(fs.readFileSync(SCHED, 'utf8'));
const sources = sched.sources || {};

/* ── ① 순서표 안에서 아직 대본이 없는 첫 회차 ────────────────
   기준은 `scene.json` 의 존재다. `uploads.json`(렌더 기록)이 아니다 —
   클라우드가 대본을 만들고 로컬이 렌더하므로 둘의 박자가 다르고,
   대본 쪽 기준을 써야 "다음에 무엇을 쓸까"가 맞는다. */
const pending = sched.order.find(id => !fs.existsSync(epScene(id)));

/* ── ② 아직 영상이 안 된 발행 글 ────────────────────────────
   정본은 `published/` 디렉터리다. 대조표(BLOG_COVERAGE.md)가 아니다 —
   대조표는 사람이 사후에 갱신하는 문서라 **실제로 늦는다**(2026-07-30 M11 발행분이
   표에 안 들어가 있던 전례가 그 파일에 적혀 있고, 08-02 M12 도 표에는 아직 "미발행 구간"이다).
   파일이 published/ 에 있다는 것은 게시팀이 발행 후 아카이브했다는 뜻이므로 그쪽이 빠르고 정확하다. */
function isDraft(file) {
  /* 발행 안 된 초안이 섞여 있다. 앞머리 주석의 publish_status 로 가른다
     (예: 2026-07-09-goap-pathfinding-honest-failure.html = DRAFT).
     주석 자체가 없는 파일이 대부분인데 그건 전부 발행분이다 — 없으면 발행으로 본다. */
  const head = fs.readFileSync(path.join(PUBLISHED, file), 'utf8').slice(0, 2000);
  const m = head.match(/publish_status:\s*(\S+)/);
  return !!m && /DRAFT/i.test(m[1]);
}

/* 이미 어느 회차가 물고 있는 글 + 구성안이 본편 밖으로 빼 둔 번외.
   🔴 번외(개발 방법론 편)를 자동으로 편입하지 않는다. 구성안이 "시즌 마지막이나 중간
   쉬어가는 자리"라고 자리를 지정해 둔 편이라, 발행일 순서로 끼워 넣으면 그 결정을 덮는다.
   넣을 때가 되면 사람이 `order` 에 한 줄 적는다. */
const claimed = new Set([
  ...Object.values(sources).map(s => s && s.local).filter(Boolean),
  ...(sched.번외?.local ? [sched.번외.local] : [])
]);

const fresh = fs.existsSync(PUBLISHED)
  ? fs.readdirSync(PUBLISHED).filter(f => !claimed.has(f)).filter(f => !isDraft(f)).sort()
  : [];

/* ── 원본 글의 공개 URL 찾기 ────────────────────────────────
   영상 설명에 블로그 링크를 넣는 자리라 비면 홍보가 죽는다. 두 군데를 순서대로 본다.
   못 찾으면 null 로 두고 ⚠️ 를 찍는다 — 기획팀이 그때 사람 눈으로 채운다.
   (publish.mjs 는 URL 이 없으면 설명에서 그 줄을 빼고 계속 간다. 막지는 않는다.) */
function findUrl(file) {
  // ⓐ 블로그 상태 파일 — 발행 직후에 갱신되므로 가장 빠르다.
  //    한 회차 블록 안에 blog_url 과 local_archive 가 함께 있다. 파일명이 먼저 나오는
  //    블록도 있어서 짝을 블록 단위로 묶지 않고, 파일명 주변 앞뒤를 훑는다.
  if (fs.existsSync(LASTPUB)) {
    const t = fs.readFileSync(LASTPUB, 'utf8');
    const at = t.indexOf(file);
    if (at >= 0) {
      const around = t.slice(Math.max(0, at - 1500), at + 500);
      const u = [...around.matchAll(/blog_url`?:\s*(https:\/\/\S+?\.html)/g)].pop();
      if (u) return u[1];
    }
  }
  // ⓑ 커버리지 대조표 — 발행일이 같은 행의 링크. 표가 늦으면 여기서 못 찾는다.
  if (fs.existsSync(COVERAGE)) {
    const date = file.slice(0, 10);
    for (const line of fs.readFileSync(COVERAGE, 'utf8').split('\n')) {
      if (!line.startsWith('|') || !line.includes(date) || !line.includes('PUBLISHED')) continue;
      const u = line.match(/\((https:\/\/[^)\s]+\.html)\)/);
      if (u) return u[1];
    }
  }
  return null;
}

/* 다음 회차 id. 이미 쓴 `epNNs` 중 가장 큰 번호 + 1.
   🔑 `order` 만 보면 안 된다 — `order` 와 `sources` 는 어긋날 수 있고(회차를 순서표에서
   빼도 매핑은 남는다), 그러면 이미 매핑이 있는 번호를 다시 집어 남의 줄을 덮는다.
   **둘의 합집합**에서 센다.
   ⚠️ 특별편(`ep00i` 처럼 s 로 안 끝나는 것)은 세지 않는다 — 본편 번호와 별개 계열이다. */
function nextId() {
  const n = [...sched.order, ...Object.keys(sources)]
    .map(id => id.match(/^ep(\d+)s$/)).filter(Boolean)
    .map(m => Number(m[1]));
  return 'ep' + String(Math.max(-1, ...n) + 1).padStart(2, '0') + 's';
}

/* ── 판정 ───────────────────────────────────────────────── */
let verdict;
if (pending) {
  verdict = { state: 'PENDING', ep: pending, reason: '순서표에 아직 대본 없는 회차가 있다' };
} else if (fresh.length === 0) {
  verdict = {
    state: 'IDLE', ep: null,
    reason: '재료 없음 — 순서표를 다 만들었고 아직 영상이 안 된 발행 글도 없다'
  };
} else {
  const file = fresh[0];
  verdict = {
    state: EXTEND ? 'EXTENDED' : 'AVAILABLE',
    ep: nextId(), source: { local: file, url: findUrl(file) },
    waiting: fresh.length,
    reason: '순서표를 다 만들었고 아직 영상이 안 된 발행 글이 있다'
  };

  if (EXTEND) {
    /* 이미 그 id 로 적어 둔 것이 있으면 덮지 않고 멈춘다. 번호 계산이 어긋났다는 뜻이고,
       덮으면 사람이 손으로 적어 둔 원본 매핑이 조용히 사라진다. */
    if (sources[verdict.ep]) {
      console.error(`🔴 ${verdict.ep} 가 이미 sources 에 있다 — 덮지 않고 멈춘다. 순서표를 사람이 봐라.`);
      process.exit(1);
    }
    /* 🔴 한 칸만 늘린다. 밀린 글이 셋이어도 셋을 한꺼번에 넣지 않는다 —
       "매일 하나"가 규칙이고, 순서표에 미리 넣어 두면 간격 게이트가 무의미해진다. */
    sched.order.push(verdict.ep);
    sched.sources[verdict.ep] = {
      local: file,
      url: verdict.source.url,
      note: `순서표 소진 후 backlog.mjs 가 발행 순서로 편입(${new Date().toISOString().slice(0, 10)}). `
        + `구성안 표에 행이 없는 회차다 — 막·주제는 원문에서 읽어라.`
        + (verdict.source.url ? '' : ' ⚠️ 공개 URL 미확인 — 기획팀이 채울 것.')
    };
    fs.writeFileSync(SCHED, JSON.stringify(sched, null, 2) + '\n');
  }
}

/* ── 보고 ───────────────────────────────────────────────────
   사람이 읽는 줄은 **stderr**, 기계가 읽는 한 줄은 stdout 으로 가른다.
   publish.mjs 가 이 스크립트를 부르면서 사람 줄은 그대로 흘려보내고 판정만 파싱하기 때문이다.
   섞어 두면 부르는 쪽이 출력을 정규식으로 긁게 되고, 그건 문구를 고치는 날 조용히 깨진다. */
const say = s => process.stderr.write(s + '\n');

say(`상태     ${verdict.state}`);
say(`사유     ${verdict.reason}`);
if (verdict.ep) say(`회차     ${verdict.ep}`);
if (verdict.source) {
  say(`원본     tools/blog-automation/published/${verdict.source.local}`);
  say(`URL      ${verdict.source.url ?? '⚠️ 못 찾았다 — 기획팀이 채울 것'}`);
  if (verdict.waiting > 1) say(`대기     발행 글 ${verdict.waiting}편이 밀려 있다 (하루 한 편씩 간다)`);
}
if (verdict.state === 'EXTENDED') say(`기록     state/schedule.json 에 ${verdict.ep} 추가`);
if (verdict.state === 'AVAILABLE') say(`         --extend 를 안 줘서 순서표는 그대로다.`);
if (verdict.state === 'IDLE') say(`         → 아무것도 하지 않는다. 블로그가 새 글을 발행하면 다음 실행이 이어받는다.`);

if (JSON_OUT) console.log(JSON.stringify(verdict));
