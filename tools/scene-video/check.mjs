/* 회차가 youtube-editor 가이드와 프로젝트 규약을 지키는지 기계로 점검한다.
   사용: node tools/scene-video/check.mjs ep01s [--fps 5] [--json]

   왜 필요한가: 가이드는 4,545줄이고 회차마다 사람이 다시 훑을 수는 없다. 기계가 볼 수 있는
   것만이라도 매번 보게 만든다. 못 보는 것(구성이 재미있는가, 그림이 뜻을 전하는가)은
   여전히 사람 몫이다 — 이 스크립트는 그걸 대신한다고 주장하지 않는다.

   예외는 씬 JSON 에 사유와 함께 적는다:
     shot.checks = { "palette": "제품 마크는 브랜드 색", "edge": "파편이 화면 밖으로 날아감" }
   값이 곧 사유다. 사유 없이 끄는 길은 두지 않았다.
*/
import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { ROOT, openEngine, langPaths, langOf, bakeScene } from './lib-node.mjs';

const argv = process.argv.slice(2);
const EP = argv.find(a => !a.startsWith('--')) || 'ep01s';
const FPS = Number((argv[argv.indexOf('--fps') + 1] > 0 && argv.includes('--fps')) ? argv[argv.indexOf('--fps') + 1] : 5);
const AS_JSON = argv.includes('--json');
const LANG = langOf(argv);
/* 🔴 언어별 게이트 규칙(LANG_RULES)은 W2 의 것이다. 여기서는 경로만 가른다 —
   모르는 언어는 bakeScene 이 "대본이 없다"로 명시적으로 멈춘다. */
const P = langPaths(EP, LANG);

const scene = JSON.parse(fs.readFileSync(bakeScene(EP, LANG), 'utf8'));
const timedPath = P.build('timed.json');
const timed = fs.existsSync(timedPath) ? JSON.parse(fs.readFileSync(timedPath, 'utf8')) : null;

const results = [];
const add = (ok, name, detail, level = 'fail') =>
  results.push({ ok, name, detail, level: ok ? 'pass' : level });

/* ── A. 씬·대본 (파일만 보면 되는 것) ───────────────── */

// 클립 샷(롱폼 W5)은 kind 대신 clip 이 화면을 진다 — 어느 쪽이든 하나는 있어야 한다
const missingMeta = scene.shots.filter(s => !s.reads || !s.source || !(s.kind || s.clip)).map(s => s.id);
add(missingMeta.length === 0, 'shot 메타 완비',
  missingMeta.length ? `reads/source/kind|clip 누락: ${missingMeta.join(', ')}` : `${scene.shots.length}샷 전부 있음`);

/* ── 형제 카드 라벨 = 그 편의 youtube.title ──────────
   🔴 분할 회차 여섯이 서로를 부르는 이름 열여덟 개 중 **하나도 안 맞았다**(2026-08-04 검수).
   그중 둘은 사실관계까지 어긋났다 — 5-1·5-3 이 5-2 를 「갇혔다」(완료형)로 불렀는데
   원문은 '갇히지 않으려면' 이라는 조건절이고 안 난 사고다. 한 편이 원문 대조로 바로잡은
   수위가 형제 카드에서 무효가 되고 있었다.

   정본을 새로 만들지 않고 이미 승인된 youtube.title 에서 기계적으로 파생한다:
   「<제목> · 유니티 GOAP 개발일지 5-2편 #shorts」 → 「5-2편 · <제목>」.
   그러면 카드를 보고 검색·추천에서 그 편을 찾는 시청자가 같은 문장을 만난다.
   지어낸 문구가 0개이므로 라벨이 네 번째 표기가 되는 일도 없다. */
const sibTail = /^(.+) · 유니티 GOAP 개발일지 ([\d-]+편) #shorts$/;
const sibLabel = sc => { const m = sibTail.exec(sc.youtube?.title ?? ''); return m ? `${m[2]} · ${m[1]}` : null; };
const sibs = scene.outro?.siblings ?? [];
if (sibs.length) {
  const bad = [];
  for (const s of sibs) {
    const got = typeof s === 'string' ? s : s.label;
    const part = /^([\d-]+편)/.exec(got ?? '')?.[1];
    /* 4-2편 → ep04s-2. 이 규약이 §1 의 분할 id 규약과 같은 자리에서 나온다. */
    const m = /^(\d+)-(\d+)편$/.exec(part ?? '');
    const sibEp = m ? `ep${String(m[1]).padStart(2, '0')}s-${m[2]}` : null;
    const sibPath = sibEp && path.join(ROOT, 'episodes', sibEp, 'scene.json');
    if (!sibPath || !fs.existsSync(sibPath)) { bad.push(`${got} → 회차를 못 찾았다`); continue; }
    /* 🔴 파싱은 크래시가 아니라 게이트 실패다 (2026-08-11 · ep16s 실증 2회 — 형제 한 편의
       비이스케이프 따옴표가 다섯 편 판정 실행을 통째로 죽였다). 깨진 형제는 실재하는 문제이므로
       숨기지 않고 이름을 붙여 떨어뜨린다. 조용한 재시도는 진짜 파손을 가리므로 안 넣는다. */
    let sibScene;
    try { sibScene = JSON.parse(fs.readFileSync(sibPath, 'utf8')); }
    catch (e) { bad.push(`${sibEp}: scene.json 파싱 불가(${e.message.slice(0, 60)}) — 저장 경합이면 재실행, 아니면 그 파일을 고쳐라`); continue; }
    const want = sibLabel(sibScene);
    if (want === null) { bad.push(`${sibEp} 의 youtube.title 이 규약 형식이 아니다`); continue; }
    if (want !== got) bad.push(`${sibEp}: "${got}" ≠ "${want}"`);
  }
  add(bad.length === 0, '형제 라벨 = 그 편의 youtube.title',
    bad.length ? bad.join(' / ') : `${sibs.length}개 전부 일치`);
}

const kindsDir = path.join(ROOT, 'episodes', EP, 'kinds');
const badKind = [...new Set(scene.shots.filter(s => s.kind).map(s => s.kind))].filter(k => !fs.existsSync(path.join(kindsDir, `${k}.js`)));
add(badKind.length === 0, 'kind 파일 존재', badKind.length ? `없음: ${badKind.join(', ')}` : '전부 존재');

const allLines = scene.shots.flatMap(s => s.lines.map(l => ({ ...l, shot: s.id })));

const tooManyLines = allLines.filter(l => l.text.split('\n').length > 2);
add(tooManyLines.length === 0, '자막 2줄 상한',
  tooManyLines.length ? `${tooManyLines.length}줄이 3줄 이상 (${tooManyLines[0].shot})` : `${allLines.length}줄 전부 2줄 이하`);

const bigPauses = allLines.filter(l => (l.pauseAfter ?? 0) >= 600);
add(bigPauses.length <= 2, '큰 쉼 회차당 2회 이하',
  `${bigPauses.length}회` + (bigPauses.length ? ` (${bigPauses.map(l => l.shot).join(', ')})` : ''),
  'warn');

/* ── 4단 고정 구조 (2026-08-08 신설 · ADR-V25-8) ─────
   인트로(고정 문안 + 브랜딩 그림) → 훅(「오늘 개발 일지 내용은」) → 본문 → 아웃트로.
   사용자 지시가 문안까지 고정했으므로 기계가 볼 수 있다 — 셋 다 fail/warn 로 잰다.
   🔴 이 게이트는 신규 회차용이다. 옛 회차(ep11s 이전)에 돌리면 전부 red 가 나는 것이
   옳다 — 그 회차들은 이 구조 이전의 규격으로 승인됐다. */
/* 🔑 구형 예외 — 4단 구조(2026-08-08) **이전에 승인·완주한** 회차를 제목 개정 등으로
   다시 구울 때만 쓴다. `scene.legacyFormat` 에 사유를 적으면 구조 게이트 셋을 사유와
   함께 통과시킨다(예외 = 값이 곧 사유, checks 와 같은 원칙).
   🔴 신규 회차가 이 필드를 들고 오면 그 자체가 반려 사유다 — 검수팀이 본다.
   첫 사용처: ep10s-3·ep11s (제목 도치형 개정 재렌더, 2026-08-08). */
const legacy = String(scene.legacyFormat ?? '').trim();

/* 🔑 가로(wide) 프로필 (롱폼 트랙, ADR-LF-1) — 4단 구조·쇼츠 길이 대역·예고 사슬은
   **세로 쇼츠의 규격**이다(ADR-V25-8 의 적용 범위). 롱폼 구조의 정본은
   Docs/롱폼_대본_문법.md(W7)이고, 기계 게이트 승격은 그 문서 확정 후에 한다 —
   그때까지 wide 는 해당 검사를 「wide — 쇼츠 규격 비대상」으로 통과시킨다.
   🔴 무르게 한 것이 아니라 측정을 규격에 맞춘 것이다(ADR-V25-3 과 같은 원칙).
   보편 검사(메타·2줄 상한·2초·팔레트·결정성·정적·잘림·안전영역)는 wide 도 그대로 본다. */
const WIDE = scene.format === 'wide';
const wideSkip = 'wide — 쇼츠 규격 비대상 (롱폼 규격은 Docs/롱폼_대본_문법.md)';

/* 🔄 2026-08-12 개정 (ADR-V25-17 · 사용자 승인) — 구조가 둘이고 **`shots[0].kind` 가
   스스로 밝힌다**. 새 필드를 안 만드는 이유: 기존 44편이 한 글자도 안 바뀌고 돌아야 한다.
     구 구조(INTRO_FIRST) : [인트로] [훅] 본문… [아웃트로]   ← ep00s~ep16s·d01s
     신 구조              : [사건]   [정체성] 본문… [아웃트로] ← ep14s-4 부터
   근거 = 실측. 구 구조는 **사건이 6.8~7.0초에 시작**했다(인트로 4.6 + 훅 보일러플레이트
   「오늘 개발 일지 내용은,」 ~2.3). 8초 창의 85%를 사건 이전이 쓴 것이다. */
const INTRO_FIRST = scene.shots[0]?.kind === 'intro';
const introIdx = INTRO_FIRST ? 0 : 1;
const hookIdx = INTRO_FIRST ? 1 : 0;

const introShot = scene.shots[introIdx];
const introSayAll = (introShot?.lines ?? []).map(l => l.say ?? l.text).join(' ');
add((legacy || WIDE) ? true : (introShot?.kind === 'intro' && /개발\s*일지|마을/.test(introSayAll)), '인트로 있음',
  WIDE ? wideSkip
  : legacy ? `구형 예외 — ${legacy}`
    : introShot?.kind === 'intro'
      ? `${introShot.id} (kind=intro · shots[${introIdx}]) — 「${introSayAll.slice(0, 28)}…」`
      : `🔴 shots[${introIdx}].kind 가 intro 가 아니다(${introShot?.kind}) — 고정 인트로가 없다`);

const hookLine = scene.shots[hookIdx]?.lines?.[0];
const hookSay = hookLine ? (hookLine.say ?? hookLine.text) : '';
/* 🔴 신 구조에서는 **금지 검사로 뒤집힌다** — 첫 줄이 보일러플레이트로 시작하면 fail.
   구 구조에서 요구하던 「오늘 개발 일지 내용은」이 바로 그 2.3초짜리 보일러플레이트다. */
const hookOk = INTRO_FIRST ? /^오늘\s*개발\s*일지/.test(hookSay) : !/^오늘\s*개발\s*일지/.test(hookSay);
add((legacy || WIDE) ? true : hookOk, INTRO_FIRST ? '훅 시작 문구' : '첫 줄이 사건 (보일러플레이트 금지)',
  WIDE ? wideSkip
  : legacy ? `구형 예외 — ${legacy}`
    : hookOk
      ? `「${hookSay.slice(0, 30)}…」`
      : INTRO_FIRST
        ? `🔴 shots[1] 첫 줄이 「오늘 개발 일지 내용은」으로 시작하지 않는다 — 「${hookSay.slice(0, 30)}」`
        : `🔴 첫 줄이 「오늘 개발 일지…」로 시작한다 — 신 구조는 0초에 사건이 와야 한다`);

/* 아웃트로 소개 — 마지막 샷에 시리즈 소개로 맺는 줄이 있어야 한다(ADR-V25-10).
   문안은 회차 재량이라 낱말만 본다. 뜻은 검수팀 몫. */
const lastLines = (scene.shots.at(-1)?.lines ?? []).map(l => l.say ?? l.text);
add((legacy || WIDE) ? true : lastLines.some(s => /개발\s*일지|다음\s*편에서/.test(s)), '아웃트로 소개 있음',
  WIDE ? wideSkip
  : legacy ? `구형 예외 — ${legacy}`
    : lastLines.some(s => /개발\s*일지|다음\s*편에서/.test(s))
      ? `「${lastLines.at(-1)?.slice(0, 30)}…」`
      : `🟡 마지막 샷에 소개 마무리 줄(「…개발일지」·「다음 편에서…」)이 없다`, 'warn');

/* ── 영어 자막 (CC 로 나가는 트랙) ──────────────────
   영상은 하나다 — 더빙도 번인 자막도 한국어고, 영어권 시청자는 CC 를 켜서 본다.
   그 자막의 원문이 `lines[].en` 이고 srt.mjs 가 그것을 엔진 타임라인에 얹는다.

   🔴 **잴 수 있는 것만 잰다**(명세 ADR-V-18). "직역인가"는 기계로 못 재므로 여기서
   판정하지 않는다 — 억지로 만들면 근거 없는 임계값이 되고 그건 ADR-V-7 이 금지한 것이다.

   🔑 옛 회차(ep00s~ep05s-2)에는 `en` 이 아예 없다. **하나도 없으면 영어 자막 대상이 아닌
   회차로 보고 건너뛴다** — 안 그러면 옛 회차를 다시 볼 때마다 헛경보가 난다.
   반대로 **일부만 있으면 fail 이다.** 그게 진짜 누락이고, 빠진 줄만큼 자막이 비어 나간다. */
const enCount = allLines.filter(l => l.en?.trim()).length;
if (enCount > 0) {
  const noEn = allLines.filter(l => !l.en?.trim());
  add(noEn.length === 0, '영어 자막 전 줄 존재',
    noEn.length
      ? `${noEn.length}줄 빠짐 (${[...new Set(noEn.map(l => l.shot))].join(', ')}) — srt.mjs 가 멈춘다`
      : `${allLines.length}줄 전부 있음`);

  /* 2줄 상한의 근거는 명세 §0.4 실측이다: 유튜브가 쇼츠 자막을 프레임 318px 에서
     시작해 **아래로** 키우는데, 2줄이 정확히 그림 시작선(420px)에서 끝난다.
     3줄이면 그림을 덮는다.
     🔴 이 값은 기기 1대·관측 2건에서 나왔다. 더 조이지도 풀지도 마라 — 관측이 쌓이면 그때. */
  const enTooMany = allLines.filter(l => (l.en ?? '').split('\n').length > 2);
  add(enTooMany.length === 0, '영어 자막 2줄 상한',
    enTooMany.length
      ? `${enTooMany.length}줄이 3줄 이상 (${enTooMany[0].shot}) — 그림을 덮는다`
      : `${enCount}줄 전부 2줄 이하`);
} else {
  add(true, '영어 자막 (없음 · 판정 안 함)',
    `이 회차는 en 이 0줄이다 — 영어 자막 대상이 아닌 회차로 보고 건너뛴다`);
}

/* ── 제목 이행 — 제목이 약속한 것이 첫 두 줄 안에 나오는가 ──────────────
   🔴 이 채널 최대의 이탈 원인이다(Docs/영상_이탈률_개선_실행명세서.md §관측②).
   실측 절대 시청 시간이 16~27초인데 ep02s 는 제목이 약속한 장면이 34.3초에야 나왔다 —
   시청자 대다수가 그 장면을 한 번도 못 보고 나간다.

   기준을 `hook` 이 아니라 `youtube.title` 로 잡는 이유는 ADR-V-3 이다:
   시청자가 실제로 보고 누른 것이 제목이지 화면 안 문구가 아니다.

   `text`(자막)와 `say`(음성)를 **둘 다** 본다. 같은 수를 자막은 "4,096" 으로,
   음성은 "사천 구십육" 으로 적기 때문이다 — 한쪽만 보면 통과할 것을 반려한다.

   ⚠️ 검사가 거칠다. 형태소 분석 없이 어절만 대조하므로 동의어·활용형을 못 잡는다.
   그런데도 과거 5편에 돌리면 이행이 늦은 셋(ep01s·ep02s·ep03s)만 정확히 걸린다.
   의존성 0 이 이 파이프라인의 원칙이라(render.mjs 5행) 거친 채로 둔다.
   🔴 오탐이 나면 게이트를 무르게 하지 말고 제목이나 첫 줄을 고쳐라 —
   이 검사는 5편 중 3편을 잡아야 옳다. 전부 통과하면 검사가 망가진 것이다. */
const TITLE_STOP = new Set(['유니티', 'Unity', 'GOAP', '개발일지', 'shorts', 'Shorts', '특별편']);
const titleWords = s => [...new Set(
  (s || '')
    .replace(/(\d),(\d)/g, '$1$2')                 // 4,096 을 한 덩어리로 (쉼표가 어절을 쪼개지 않게)
    .replace(/[·#,.…?!"'“”‘’·]/g, ' ')
    .split(/\s+/).filter(Boolean)
    .map(w => w.replace(/[을를이가은는의에서도만로]$/, ''))   // 흔한 조사만 턴다
    .filter(w => w.length >= 2)
    .filter(w => !TITLE_STOP.has(w))
    .filter(w => !/^\d+(-\d+)?편$/.test(w))        // "2편"·"5-2편" 은 회차 번호지 약속이 아니다
)];

const titleKeys = titleWords(scene.youtube?.title);
/* 🔴 인트로 샷은 매회 같은 고정 문안이라 제목을 이행할 수 없다(ADR-V25-8, 2026-08-08).
   「첫 두 줄」은 인트로를 건너뛰고 훅 줄부터 센다 — 인트로가 없는 옛 회차는 그대로다. */
const postIntroLines = scene.shots.filter(s => s.kind !== 'intro')
  .flatMap(s => s.lines.map(l => ({ ...l, shot: s.id })));
const headText = postIntroLines.slice(0, 2).map(l => `${l.text ?? ''} ${l.say ?? ''}`).join(' ');
const headKeys = titleWords(headText);
const titleHit = titleKeys.filter(k =>
  headKeys.some(h => h === k || h.includes(k) || k.includes(h)));

add(WIDE ? true : (titleKeys.length > 0 && titleHit.length > 0), '제목 이행 (첫 두 줄)',
  WIDE ? wideSkip
  : titleKeys.length === 0
    ? `🔴 youtube.title 에서 핵심어를 못 뽑았다 — 제목이 비었거나 상투어뿐이다`
    : titleHit.length
      ? `겹침: ${titleHit.join(', ')}`
      : `🔴 제목 핵심어 [${titleKeys.join(', ')}] 가 첫 두 줄에 없다 — `
      + `제목이 약속한 것을 30초 뒤에 주면 대부분 못 보고 나간다`);

/* ── 제목 후킹 — 제목에 아라비아 숫자가 있는가 (2026-08-06 신설 · 같은 날 warn 강등)
   위 게이트는 **약속을 지키는지**만 본다. 약속이 매력적인지는 여태 아무도 안 봤다.

   관측: 채널 제목 14편을 전수 분류하니 아라비아 숫자가 든 제목은 **3편뿐**이다
   (ep01s 4,096 · ep03s 24,800→5,977 · ep05s-3 0줄). 재료가 없어서가 아니다 —
   발행 글 10편에 수치 문장이 글당 2~37개 있고 대부분 표 밖 산문이다. ep06s 는 원문의
   「5번째 아키타입은 코드 0줄로 추가됐습니다」를 손에 쥐고도 "직전 편과 같은 훅"이라며
   버렸다(episodes/ep06s/notes/writer.md:32). 먹히는 훅을 파이프라인이 스스로 봉인했다.
   그 봉인을 푸는 것이 이 검사의 목적이고, 그 부분은 유효하다.

   🔴 **그런데 fail 이 아니라 warn 이다. 실측 15편이 그렇게 시켰다**(2026-08-06 채널
   지표 전수). 숫자 제목은 **클릭은 만들지만 체류를 못 만든다**:
     ep03s 「24,800줄을 5,977줄로」 → CTR **16.67% (채널 1위)** · 체류 29.86% (11/15위)
     ep05s-3 「코드는 0줄이에요」   → CTR 4.76%              · 체류 25.07% (13/15위)
   그리고 **클릭 표면 자체가 거의 없다** — 편당 노출수가 2~75회인데 유효 조회는 43~596회다.
   조회의 대부분이 쇼츠 피드에서 오고, 피드에서는 제목이 스와이프를 결정하지 않는다.
   즉 이 검사는 **트래픽의 5% 남짓한 표면**을 겨냥한다. 값은 있으나 회차를 막을 값은 아니다.

   🔑 체류를 만드는 것은 제목이 아니라 **첫 줄**이다. 같은 소재를 자른 통제된 비교가 있다:
   분할 전 `_archive/ep04s`(145초, 첫 줄 「재설계가 끝난 그 자리에서, 마을은 다시 돌아갔어요」)
   = 체류 **18.30%** → 분할 후 `ep04s-1`(40.5초, 첫 줄 「게임 속 유닛은 명령하면 그냥
   복종하죠」) = 체류 **55.16% (채널 1위)**. 소재도 사람도 같고 **첫 줄과 길이만 바뀌었는데
   3.01배**다. 강제력은 그쪽(scene-writer.md 0-B 의 첫 줄 규칙)에 둔다.

   ⚠️ 이 검사는 숫자의 **존재**만 본다. 숫자가 좋은지는 못 본다.

   🔴 **warn → 정보 표시 (2026-08-08 사용자 A/B 실측).** 사용자가 업로드된 ep10s-1 의
   제목을 「어제 잔소리한 사이인지 3초 만에 보이게 했어요」(숫자형)에서 「잔소리 들은
   주민이 원한을 가지게 해봤어요. 이게 나중에 어떤 효과가 있을까요?」(감정+질문형,
   숫자 0개)로 바꿨더니 **절대 시청 시간이 늘었다** — 같은 영상의 제목만 바꾼 통제
   비교라, 이 채널 최초의 제목 A/B 다. 제목의 기본 축은 이제 **감정·사건 + 궁금증
   질문**(scene-writer.md 0-A)이고 숫자는 보조다. 숫자 부재에 경고를 내면 그 축과
   싸우게 되므로 표시만 남긴다 — 뜻(자극·궁금증·사실성)은 검수팀이 본다.
   ⚠️ 옛 자기 검산(「과거 14편에 돌리면 11편이 걸려야 옳다」)은 이 강등으로 무효다. */
const rawTitle = scene.youtube?.title ?? '';
/* 꼬리(「· 유니티 GOAP 개발일지 3편 #shorts」)의 회차 번호는 약속이 아니라 색인이다 */
const titleBody = rawTitle.replace(/ · 유니티 GOAP 개발일지 [\d-]+편 #shorts\s*$/, '')
  .replace(/#\S+/g, '');
const titleDigits = titleBody.match(/\d[\d,\.]*/g) ?? [];
/* 한글 수사는 썸네일에서 데이터로 안 읽힌다 — 걸렸을 때 무엇을 고칠지 알려주려고 본다 */
const KO_NUM = /(하나|둘|셋|넷|다섯|여섯|일곱|여덟|아홉|열|두 번|세 번|네 번|한 줄|두 줄)/g;
const koNums = [...new Set(titleBody.match(KO_NUM) ?? [])];

add(true, '제목 후킹 (정보)',
  (titleDigits.length ? `숫자 ${titleDigits.join(', ')}` : '숫자 없음')
  + (koNums.length ? ` · 한글 수사 [${koNums.join(', ')}]` : '')
  + ` — 기본 축은 감정·사건+질문형(0-A), 뜻 판정은 검수팀`);

/* ── 다음 편 예고 (2026-07-30 사용자 요청 · 2026-08-03 길이 확정) ──
   예고 자체는 사용자 요청이라 있어야 하고, 길이만 잡는다 — 1차본들이 10~14초를 써서
   페이오프 뒤가 늘어졌다. 명세서와 충돌하지 않아 그대로 둔다(명세서는 예고를 다루지 않는다).
   🔴 같은 커밋에 있던 "편당 길이 75/90"과 "첫 자막 3.5초"는 버렸다 —
   앞엣것은 명세서 §관측①(길이는 원인이 아니다)이 기각했고 W3(50초/30~45초)이 대신한다.
   뒤엣것은 §0 틀린 전제 ②("ep02s 는 도입부 빌드업이 길어 초반 이탈")가 실사로 기각된
   추측에 기대고 있었다. 근거 없는 규칙을 만들지 않는다(ADR-V-7). */
/* 🔴 2026-08-06 개정 4.0 → 1.0 (Docs/영상_25초_전환_실행명세서.md W2·ADR-V25-2).
   편이 25초가 되면 3초 예고는 12% 다. 사용자 지시는 혼합안이다 —
   ①1초 이하 「쇼트 훅」(찰나에 다음 편의 한 컷만 탁 보여주고 끝)으로 시작해
   ②음성 예고를 아예 빼고 아웃트로 카드가 글자로 지는 형태로 이행한다.
   **두 형태를 다 받는다.** 어느 쪽인지는 회차마다 작성팀이 고른다.
   🔴 예고를 없애는 것이 아니다 — 예고 자체는 2026-07-30 사용자 요청이고 유효하다.

   🔴 **1.0 → 1.8 → 3.0 (2026-08-07 사용자 판정으로 「쇼트 훅」 자체를 폐기).**

   이력과 실패를 남긴다. 1.0 은 **물리적으로 불가능**했다 — 본문 줄은 179~213ms/자로 균일한데
   짧은 줄에는 **고정 오버헤드 약 550ms** 가 붙어(ep08s-2 예고 6자 = 1,657ms = 276ms/자)
   1.0초엔 **2.4자**밖에 안 들어간다. 그래서 1.8 로 올렸는데, **그것도 틀렸다.**
   1.8초 = 6~7자로 나온 실물이 「다음 편, 다 같이.」였고 사용자 판정은
   *"이게 무슨 말이야, 시청자는 읭? 이게 뭔 말이야 한다고"* 였다.

   🔴 **근본 원인은 내가 지시를 잘못 옮긴 것이다.** 원안은 *"찰나의 순간에 가장 자극적인
   **장면 하나만** '탁' 보여주고"* — **시각 컷**이었다. 그걸 **음성 한 줄**로 구현하고
   「1.8초는 말로 6~7자」라고 안내했으니 뜻이 성립할 수가 없었다.
   그리고 이 엔진에서 시각 컷 예고는 **놓을 자리가 없다** — 마지막 2.6초는 아웃트로 카드가
   덮고, 그 앞은 본문이다.

   → **결론: 예고는 「뜻이 통하는 한 문장」이다. 상한 3.0초**(2026-08-03 사용자 확정값으로 복귀).
   하한은 두지 않는다 — **"이 문장만 보고 다음 편이 뭔지 알 수 있는가"는 사람이 판정한다**
   (글자 수 임계값을 발명하지 않는다, ADR-V-7).

   🔴 **3.0 → 5.0 (2026-08-08 사용자 판정).** ep10s-2 재제작본의 예고(「다음 편, 편한 기능을
   일부러 지워요」 2.9초)를 보고: *"아웃트로에서 다음 편이 무엇인지에 대한 설명이 조금
   부족한 거 같아. 영상 길이가 좀 더 길어진다고 해도 괜찮을 거 같아."* + 문형도 「다음 편,」
   (끊김)이 아니라 **「다음 편은~」으로 잇는 문장**으로. 4단 구조에서 아웃트로가 미리보기
   (nextpeek)와 함께 설명을 지는 자리가 됐으므로 한 문장이 길어진다 — 상한만 5.0 으로 열고
   뜻 판정은 여전히 사람이 한다. */
const TEASER_MAX = 5.0;
/* 🔴 2026-08-04. 이 선언이 한때 사라져 check.mjs 가 통째로 죽었다. 명세서를 정본으로
   병합하며 "편당 길이 75/90" 블록을 버렸는데 CPS_REF 를 거기서 선언하고 있었고,
   아래 예고 길이 계산이 그걸 쓴다. teaserLines 가 비면 콜백이 안 불려 조용히 지나가지만,
   바로 아래 항목이 예고를 fail 수준으로 **요구**하므로 규칙을 지킨 회차만 골라서 터진다 —
   가장 나쁜 형태의 고장이다. 작성팀이 ep04s-2 를 쓰다 발견해 보고했다.
   🔑 이 값은 예고 한 줄의 길이를 재는 데만 쓴다. 회차 총 길이는 timed.json 실측으로 재고
   (W3), 산정치로 길이를 판정하지 않는다 — 그건 §관측①이 기각한 접근이다. */
const CPS_REF = 6.7;   // ep02s 실측: 발화 84.18초에 564자
const lastShot = scene.shots.at(-1);
const teaserRe = /다음\s*(편|회차|은|는)|이어서|(\d+)\s*부/;
const teaserLines = lastShot.lines.filter(l => teaserRe.test(l.say ?? l.text));
/* 🔴 줄을 **위치(마지막 샷의 몇 번째 줄)** 로 기억한다. 내용으로 맞추면 안 된다 —
   `timed.json` 의 줄에는 `text`·`say` 가 없어서(dur/pause 만 있다) 내용 대조가
   전부 빗나가고, 아래 「자막 2초」 예외가 조용히 무효가 된다. 2026-08-06 fixture 로 잡았다. */
const teaserIdx = new Set(
  lastShot.lines.map((l, i) => (teaserRe.test(l.say ?? l.text) ? i : -1)).filter(i => i >= 0));
const lastShotIdx = scene.shots.length - 1;
/* 예고 길이 — `timed.json` 이 있으면 실측을, 없으면(클라우드) 글자수 산정을 쓴다.
   🔴 상한이 1.0초로 내려가 산정 오차가 그만큼 크게 보인다. 실측이 있으면 실측이 정본이다. */
const teaserTimed = timed?.shots?.[lastShotIdx]?.lines;
const teaserDur = teaserTimed
  ? [...teaserIdx].reduce((a, i) => a + ((teaserTimed[i]?.dur ?? 0) + (teaserTimed[i]?.pause ?? 0)) / 1000, 0)
  : teaserLines.reduce((a, l) =>
    a + (l.say ?? l.text.replace(/\n/g, ' ')).length / CPS_REF + (l.pauseAfter ?? 0) / 1000, 0);
/* 2안 — 음성 예고가 0 이면 아웃트로 카드가 다음 편을 이름으로 져야 한다.
   🔴 `outro.next` 는 `outro.siblings` 와 다르다. siblings 는 형제 편 목록이고
   next 는 **다음 회차 하나**다. 둘을 같은 것으로 읽으면 분할 회차에서 조용히 통과한다. */
const outroNext = String(scene.outro?.next ?? '').trim();
add(WIDE ? true : (teaserLines.length > 0 || outroNext.length > 0), '다음 편 예고 있음',
  WIDE ? wideSkip
  : teaserLines.length
    ? `${lastShot.id} 자막 ${teaserLines.length}줄 (음성 예고)`
    : outroNext
      ? `아웃트로 카드: 「${outroNext}」 (음성 0)`
      : `🔴 마지막 샷(${lastShot.id})에 음성 예고가 없고 outro.next 도 비었다 — 다음 편을 아무도 모른다`);
add(WIDE || teaserLines.length === 0 || teaserDur <= TEASER_MAX, `예고 ${TEASER_MAX}초 이하`,
  WIDE ? wideSkip
    : teaserLines.length ? `${teaserDur.toFixed(1)}초` : '음성 예고 없음 — 아웃트로가 진다', 'warn');

if (timed) {
  const flat = timed.shots.flatMap((s, si) =>
    s.lines.map((l, li) => ({ ...l, shot: scene.shots[si].id, si, li })));
  /* 🔴 예고 줄은 뺀다 (2026-08-06, ADR-V25-3). 2초 규칙의 목적은 "읽을 시간을 준다"인데
     쇼트 훅은 **읽히지 않는 게 설계**다 — 목적이 다른 줄에 같은 잣대를 대면 안 된다.
     🔴 검사를 무르게 하는 것이 아니라 측정을 목적에 맞추는 것이다.
     본문 줄은 2초 규칙 그대로다 — 여기서 통째로 끄면 아무도 못 읽는 자막이 샌다. */
  const tooShort = flat.filter(l =>
    (l.dur + (l.pause || 0)) < 2000 && !(l.si === lastShotIdx && teaserIdx.has(l.li)));
  add(tooShort.length === 0, '자막 2초 미만 없음',
    tooShort.length ? `${tooShort.length}줄 (${tooShort.map(l => `${l.shot}:${((l.dur + l.pause) / 1000).toFixed(1)}s`).join(', ')})` : '전부 2초 이상',
    'warn');

  const cps = timed.summary?.charsPerSec;
  add(cps >= 6.0 && cps <= 7.2, '말 속도 6.0~7.2자/초', `${cps}자/초 (참고 영상 6.93)`, 'warn');

  /* ── 총 길이 ────────────────────────────────────────
     🔴 2026-08-06 개정: 50 / 30~45 → **25 / 20~25**
     (Docs/영상_25초_전환_실행명세서.md W1·ADR-V25-1, 사용자 지시로 실험 없이 전면 전환).

     🔴 **이유를 오해하지 마라 — "짧으면 안 넘긴다"가 아니다.** 길이와 Stayed to watch 는
     무상관이다(ρ=0.071, 15편 실측. ep01s 는 105.5초인데 체류 2위이고 ep05s-1 은 43.2초인데
     꼴찌다). 노리는 것은 **완주와 루프**, 그리고 편수를 늘려 피드 노출 기회를 늘리는 것이다.
     🔴 **조회율이 오르는 것은 성과가 아니라 산수다**(조회율 = 시청시간 ÷ 길이).
     실제로 117→37초로 줄였을 때 조회율은 17%→40% 로 올랐지만 절대 시청 시간은
     20초 → 15초로 **떨어졌다**. 판정은 명세서 §1 대로 **절대 시청 시간 15초 유지**로 한다.

     상한(25)과 권장대역(20~25)을 나눈 이유는 기존과 같다 — 상한은 페널티의 천장이고
     권장은 목표다. 합치면 25.4초짜리가 반려되어 되돌림 루프가 헛돈다.
     🔴 25 는 사용자 지정값이고 **20 은 제안치**다(하한이 없으면 본문이 얇은 조각이 통과한다). */
  /* 🔴 **꼬리를 반드시 더한다** (2026-08-06 개정, 사용자 판정 — 마스터가 잡았다).
     엔진은 샷이 끝날 때마다 `SHOT_TAIL` 0.35초 여운을 넣는다(engine.js:16,167).
     이 줄이 그걸 안 세는 바람에 **게이트가 파일보다 짧게 보고하고 있었다** —
     실증: 승인·렌더된 ep08s-1 의 mp4 `mvhd` 가 **25.800초**인데 게이트는 24.41초라고 했다
     (ep04s-1 41.83 vs 40.46 · ep07s-2 43.27 vs 41.52. 전부 0.35×샷수 차이, 오차 0.03초 안).
     즉 「25초 내로」가 실제로는 지켜지지 않고 있었다.
     🔑 아이러니: **같은 파일의 산정 경로(아래 else 블록)는 꼬리를 이미 더하고 있었다.**
     그래서 「산정이 실측보다 짧다」던 관측은 애초에 **서로 다른 것을 비교한 것**이었다 —
     산정은 꼬리 포함, 실측은 미포함. ADR-V25-1 의 그 문단도 이 사실로 정정했다. */
  const SHOT_TAIL = 0.35;   // engine.js 의 같은 이름 상수와 맞춰라
  const subSec = timed.shots.flatMap(s => s.lines)
    .reduce((a, l) => a + l.dur + (l.pause || 0), 0) / 1000;
  const totalSec = subSec + SHOT_TAIL * timed.shots.length;
  /* 🔴 상한 28 → **38초 · 권장 33~37** (권장대역만 2026-08-10 재조정 · ADR-V25-9 개정).
     이력: 25(2026-08-06) → 28(2026-08-07 「25초 ±3초」) → 35+3(2026-08-08) →
     권장 30~35 → **33~37**(2026-08-10 사용자 판정). **fail 38 은 안 바뀌었다.**
     *"영상의 상한은 35초로 늘려도 될 듯하다"* + *"아웃트로를 넣었을 때 전체 영상길이가
     35초 -+ 3초 정도는 충분히 괜찮다"*. 길이가 늘어난 것이 아니라 **구조가 늘었다** —
     인트로(~4.5초)와 아웃트로 소개+미리보기(~5.5초)가 새로 붙었고 본문은 20±2 그대로다.

     🔴 **왜 하한을 30 → 33 으로 올렸나** — 옛 대역의 아래쪽이 **도달 불가능**했다.
     7편 실측(ep13s 3 + ep14s 4)에서 4단 고정비가 **15.7~17.0초**(인트로 4.6 + 훅 4.3~4.9 +
     아웃트로 6.8~7.6)라, `본문 18~22초` 의 하한 18 을 지키면 **총합이 최소 33.7초**다.
     즉 30~33 대는 **다른 게이트를 어겨야만** 닿는 구간이었다.
     🔴 **왜 상한을 35 → 37 로 올렸나** — `ADR-V25-13`(비개발자 이해 가능성, 2026-08-10)이
     본문을 평균 **+1.7초** 늘렸다. 쉬운 말은 전문 용어보다 음절이 길다. 그 결과
     **7편 중 6편이 warn** 을 띄웠고 — **상시 켜지는 경고는 경고가 아니다.**
     🔑 이 대역은 지어낸 값이 아니라 **관측에서 나왔다**: 하한은 고정비 최솟값 + 본문 하한,
     상한은 사용자가 연 35±3 안에서 fail(38)에 여유 1초를 남긴 값이다.
     🔄 **2026-08-11 재개정 — 상단 37 → 38**(사용자 판정 · 채널 재측정이 근거).
     스튜디오 실측(창 7/13~8/9, 34편): ①절대 시청 시간은 길이와 무관하게 12~30초 대역
     (84~150초 시절부터 37초 4단까지 동일) ②조회율 하락(37.8%)이 배포에 불이익 없음 —
     12편 3형제가 1~2일 만에 2.2~2.7천 회로 채널 최고 페이스. **본문을 깎아 짧출 이득이
     실측에 없으므로** 37~38 사이 완충 warn 을 없애고 대역을 fail 직전까지 연다.
     (ep14s-4 37.53초 같은 편이 warn 을 상시 띄우던 것도 해소 — 상시 경고는 경고가 아니다.) */
  if (WIDE) {
    /* 롱폼 길이 판정(본편 300~600초 = 명세 S1)은 W7 문법 문서 확정 후 게이트로 승격한다
       (ADR-LF-9: 실측 전 규칙 금지). 지금은 실측값만 찍는다 — 판정 아님. */
    add(true, '총 길이 (wide 참고 · 판정 아님)',
      `${totalSec.toFixed(1)}초 = 자막 ${subSec.toFixed(1)} + 꼬리 ${(SHOT_TAIL * timed.shots.length).toFixed(2)}` +
      ` (본편 기준 300~600초 · 승격은 W7 뒤)`);
  } else {
    add(totalSec <= 38, '총 길이 38초 이하',
      `${totalSec.toFixed(1)}초 = 자막 ${subSec.toFixed(1)} + 꼬리 ${(SHOT_TAIL * timed.shots.length).toFixed(2)}` +
      ` (목표 33~38초 · 이 값이 실제 mp4 길이다)`);
    /* 🔄 ADR-V25-17 — **대역은 구조에 종속이다.** ADR-V25-9 의 산식(고정비 최솟값 + 본문
       하한 18)을 그대로 쓰되 고정비가 구조마다 다르다:
         구 구조 = 15.7 + 18 ≈ 33   /   신 구조 = 12.7 + 18 ≈ 31
       (신 고정비 실측 3편: 인트로 3.5 + 훅 2.4~3.0 + 아웃트로 6.8~7.3 = 12.7~13.8)
       상한 38 은 구조와 무관하다 — fail 선이고 절대 시청 시간 실측이 떠받친다. */
    const bandLo = INTRO_FIRST ? 33 : 31;
    add(totalSec >= bandLo && totalSec <= 38, `총 길이 ${bandLo}~38초 권장대역`,
      `${totalSec.toFixed(1)}초`, 'warn');
  }

  /* ── 단별 길이 (2026-08-08 개정 · ADR-V25-8/9) ─────
     본문 = 총 − 인트로 샷 − 훅 줄 − 아웃트로 샷. 사용자 「본문 20초 ±2초」.
     🔴 걸리면 편을 쪼갤 신호가 아니라 곁가지를 덜어낼(모자라면 원문 어휘를 되살릴)
     신호다(ADR-V25-4). 인트로가 없는 옛 회차는 옛 정의(총 − 예고 − 아웃트로)로 산다. */
  const durShot = i => timed.shots[i]
    ? timed.shots[i].lines.reduce((a, l) => a + l.dur + (l.pause || 0), 0) / 1000 + SHOT_TAIL
    : 0;
  /* 🔄 ADR-V25-17 — 인트로·훅의 **자리**가 구조마다 다르다. 둘 다 본문에서 빼는 것은 같다. */
  const hasIntro = scene.shots[introIdx]?.kind === 'intro';
  const introSec = hasIntro ? durShot(introIdx) : 0;
  const hookSec = hasIntro
    ? ((timed.shots[hookIdx]?.lines?.[0]?.dur ?? 0) + (timed.shots[hookIdx]?.lines?.[0]?.pause ?? 0)) / 1000
    : 0;
  const outroSec = hasIntro ? durShot(timed.shots.length - 1) : teaserDur + 3.0;
  const bodySec = totalSec - introSec - hookSec - outroSec;
  if (!WIDE) add(bodySec >= 18 && bodySec <= 22, '본문 18~22초',
    `본문 ${bodySec.toFixed(1)}초 (인트로 ${introSec.toFixed(1)} · 훅 ${hookSec.toFixed(1)}` +
    ` · 아웃트로 ${outroSec.toFixed(1)} 제외)`, 'warn');
  if (hasIntro && !WIDE) {
    /* 인트로 고정 문안(33자)의 실측이 4초대다. 5.5 를 넘으면 문안이 불었거나 pause 가
       샌 것 — 사용자 제안치 3초와의 차이는 음성 물리량이다(명세 §2-C). */
    /* 🔄 ADR-V25-17 — 신 구조의 정체성 줄은 사건 뒤에 오므로 짧아야 한다(구 구조 5.5).
       🔄 상한 3.0 → **3.6** (같은 날 재개정): 3.0 은 실측 없이 적은 제안치였고, 압축 문안
       (「클로드 코드로만 만든 에이아이 마을이에요.」) 실측이 **3.5초**였다. 문안을 더 깎으면
       「로만」이나 「AI」를 잃는데 둘 다 정체성 핵심어다. 3편 전부에 상시 경고가 켜지는 쪽이
       더 나쁘다 — **제안치 전에 그 자리 값부터 본다**를 또 어긴 자리다. */
    const introCap = INTRO_FIRST ? 5.5 : 3.6;
    add(introSec <= introCap, `인트로 ${introCap}초 이하`, `${introSec.toFixed(1)}초`, 'warn');
  }

  /* ── 어미 연속 (2026-08-11 신설 · 사용자 승인 「fail ≥4 · warn =3」) ─────
     voice.json rules.1(「같은 어미 2연속까지, 3연속 금지」)은 있었는데 **측정법이 없어
     한 번도 집행되지 않았다** — ep16s-3 이 「-죠」 5연속으로 첫 집행(마스터 반려)됐다.
     🔴 잣대 = **끝 두 글자 축자 동일 연속** (마스터 전수 재측정 2026-08-11: 「어요/예요/해요
     합산」으로 세면 발행분에 8연속이 있어 집행 불가, 끝 두 글자로 세면 발행분 전부 ≤3).
     fail ≥4 = 발행분 소급 red 0 · warn =3 = 문언(3 금지)의 정신 보존(발행분 3편만 해당 —
     상시 경고 아님). 🔴 구간 = 인트로·아웃트로 샷 제외(훅 포함 — 16-3 집행 실측과 같은 구간).
     WIDE 제외: 실측 전수가 쇼츠 발행분뿐이다(M20 — 전수 검사는 고정한 축을 함께 적는다). */
  if (hasIntro && !WIDE) {
    /* 🔄 ADR-V25-17 — 재는 구간은 「고정 문안 샷과 아웃트로를 뺀 나머지」다. 구 구조에선
       그게 slice(1,-1) 이었고, 신 구조에선 인트로가 가운데(1)라 그 샷만 빠진다.
       고정 문안은 매 회차 같으므로 어미 다양성 판정에 넣으면 판정이 오염된다. */
    const midTails = scene.shots
      .filter((s, i) => i !== introIdx && i !== scene.shots.length - 1)
      .flatMap(s => s.lines || [])
      .map(l => String(l.say ?? l.text ?? '').replace(/[\s.…!?,·」』)"'『「]+$/g, '').slice(-2))
      .filter(t => t.length === 2);
    let run = 1, maxRun = midTails.length ? 1 : 0, at = midTails[0] || '';
    for (let i = 1; i < midTails.length; i++) {
      run = midTails[i] === midTails[i - 1] ? run + 1 : 1;
      if (run > maxRun) { maxRun = run; at = midTails[i]; }
    }
    const detail = `최대 ${maxRun}연속${maxRun > 1 ? ` (「${at}」)` : ''} — 본문·훅 한정, 끝 두 글자 축자`;
    add(maxRun <= 3, '어미 연속 4 이상 없음', detail);
    add(maxRun <= 2, '어미 연속 2 이하 권장', detail, 'warn');
  }
} else {
  add(false, '실측 타임라인 존재', `episodes/${EP}/build/timed.json 이 없다 — tts.mjs 를 먼저 돌려라`);

  /* ── 산정 길이(참고) ─────────────────────────────────
     🔴 판정이 아니다. ok=true 로 넣어 절대 fail 이 안 나게 해 뒀다.

     왜 넣나: 대본이 길이를 자기보고로 적는 자리(notes.길이)가 여섯 회차 전부
     제각각 틀렸다. 어떤 회차는 ÷9.15+0.5×줄수, 어떤 회차는 ÷6.77, pauseAfter 합을
     잘못 더한 것도 있었다. 식을 문서에 베껴 적게 하면 베낀 곳마다 갈린다.
     그래서 **식은 여기 한 곳에만 두고 대본은 이 줄의 출력을 인용한다.**

     회귀는 회차 다섯의 timed.json 으로 맞췄다 — ÷6.77 보다 ÷6.7(= CPS_REF)이
     최대오차 2.8s → 2.0s 로 낫다. voice.json 의 6.77 은 발화만 잰 값이라
     총 길이 산정에 그대로 쓰면 안 된다.

     구두점은 뺀다(TTS 가 소리로 내지 않고 쉼으로 흡수한다). 샷 꼬리는
     engine.js 의 SHOT_TAIL 과 같은 0.35s 다. */
  const SHOT_TAIL = 0.35;
  const spoken = l => (l.say ?? l.text.replace(/\n/g, ' ')).replace(/[.,?!…·「」"'\s]/g, '').length;
  const allLines = scene.shots.flatMap(s => s.lines);
  const estVoice = allLines.reduce((a, l) => a + spoken(l) / CPS_REF + (l.pauseAfter ?? 0) / 1000, 0);
  const estTotal = estVoice + SHOT_TAIL * scene.shots.length;
  add(true, '산정 길이 (참고 · 판정 아님)',
    `${estTotal.toFixed(1)}초 = 발화 ${estVoice.toFixed(1)} + 꼬리 ${(SHOT_TAIL * scene.shots.length).toFixed(2)}` +
    ` · 실측 오차 ±2.0초 — notes.길이 는 이 값을 인용할 것`);
}

/* ── B. 실제 프레임 (헤드리스에서 그려 보고 판정) ───── */

const paletteSkip = {}, edgeSkip = {};
for (const s of scene.shots) {
  if (s.checks?.palette) paletteSkip[s.id] = s.checks.palette;
  if (s.checks?.edge) edgeSkip[s.id] = s.checks.edge;
}

let frame = null;
const { cdp, close } = await openEngine(EP, { quiet: AS_JSON, lang: LANG });
try {
  /* 읽기 경로 점검.
     🔴 여기서 한 번 크게 틀렸다. 투명 캔버스에 rgba(255,255,255,0.28) 을 칠하고 색 채널을
     읽으면 255 가 나온다 — 알파가 이진화된 게 아니라, 투명 배경 위에서는 색이 그대로 남고
     농도가 알파 채널에 들어가기 때문이다(검정을 먼저 깔았을 때만 71 이 된다).
     이걸 "이진화"로 오독해서 한동안 픽셀 계측 전체를 못 믿을 것으로 판단했었다.
     그래서 점검은 검정을 깔고 한다 — 실제 화면과 같은 조건이다. */
  const sane = await cdp.evaluate(`(()=>{
    const c=document.createElement('canvas');c.width=c.height=4;
    const x=c.getContext('2d');
    x.fillStyle='#000';x.fillRect(0,0,4,4);
    x.fillStyle='rgba(255,255,255,0.28)';x.fillRect(0,0,4,4);
    return x.getImageData(0,0,1,1).data[1];
  })()`);
  add(Math.abs(sane - 71) <= 8, '픽셀 읽기 신뢰 가능', `검정 위 알파 0.28 → ${sane} (기대 71±8)`);

  /* 🔴 그림이 실제로 로드됐는가. 파일이 있는 것과 import 가 되는 것은 다르다 —
     kinds 를 회차 폴더로 옮기며 lib.js 상대경로가 깨졌을 때, 파일은 전부 제자리에 있었고
     엔진은 조용히 빈 화면으로 넘어갔고, 이 점검은 "정적 구간 0.0초"로 통과시켰다.
     아무것도 안 그려진 화면이 가장 조용하다. */
  const failed = await cdp.evaluate('window.__failedKinds ? window.__failedKinds() : []');
  add(!failed?.length, '그림 로드 성공',
    failed?.length ? `import 실패: ${failed.join(', ')}` : '전 kind 로드됨');

  /* 🔴 하단 안전영역 — 유튜브가 영상 위에 자기 UI 를 덮는 자리.
     이 점검이 없어서 회차 둘이 그대로 나갔다. 업로드된 ep00s 를 쇼츠 앱에서 찍어 보고서야
     자막 둘째 줄이 채널 아이콘·제목 줄에 통째로 묻혀 있다는 걸 알았다(2026-07-29).
     기존 점검이 전부 캔버스 픽셀만 보는데 자막은 DOM 이라 처음부터 시야 밖이었다.

     1477 의 출처 = 실측. 화면 좌표 역산으로 영상 상단 = 기기 y 108, 배율 1.3333(전체 폭),
     영상 하단 = 2668 → 채널 아이콘 상단 기기 2078 = (2078-108)/1.3333 = 1477.
     ⚠️ 그 화면은 크리에이터 본인 화면이라 `분석`·`공개`·`동영상 공유` 가 오버레이를 위로
     밀어 올린 최악 조건이다. 일반 시청자는 덜 가리지만 최악에 맞춘다.

     상단(0~130)은 안 본다 — 그 값은 실측한 적이 없다. 잰 것만 판정한다.
     오른쪽 세로 버튼 열(x≥930·y≥1090)도 아직 안 본다 — 비주얼이 x 1025 까지 그려서
     지금도 모서리가 겹치는데, 폭을 줄이면 안 가리는 위쪽까지 손해라 미뤘다. 관찰 항목. */
  if (WIDE) {
    /* 무대가 선언한 프로필로 실제로 떴는가 — dimsOfEp 가 경로 오류로 조용히 tall 이
       되거나 wide.css 로드가 실패하면 세로 무대에 가로 대본이 얹힌다. 조용한 종류라
       비율로 못박는다 (W1 자가 재검토 산출물). wide 전용 — tall 은 CSS 가 정적
       링크라 같은 고장 모드가 없고, tall 판정 불변(W2 DoD)을 지킨다. */
    const stageAR = await cdp.evaluate(
      `(()=>{const b=document.querySelector('.stage').getBoundingClientRect();return b.width/b.height})()`);
    add(Math.abs(stageAR - 16 / 9) < 0.01, '무대 프로필 일치',
      `가로세로비 ${stageAR.toFixed(4)} (기대 ${(16 / 9).toFixed(4)} · format=wide)`);
    /* 가로 16:9 안전영역 — youtube-editor/CAPTION_SAFE_AREA.md **원판 규격** (@1080):
       콘텐츠 안전영역 0~850 / 자막영역 850~1080. 쇼츠(아래 else)와 달리 플랫폼 UI
       실측이 아니라 가이드 문서가 정본이다 — 16:9 규격의 첫 원판 적용(W2).
       CAP_BOTTOM_MAX 1060 은 제안치(바닥 20px 여유) — 실측이 생기면 그쪽이 이긴다. */
    const CONTENT_BOTTOM = 850, CAP_BOTTOM_MAX = 1060;
    const w = await cdp.evaluate(`(()=>{
      const st = document.querySelector('.stage') || document.body;
      const S = st.getBoundingClientRect();
      const y = v => (v - S.top) / S.height * 1080;
      const worst = { visBot: 0, capBot: 0, capTop: 1e9 };
      const N = 24;
      for (let i = 0; i < N; i++) {
        window.seek(window.TOTAL * i / (N - 1));
        const vis = document.querySelector('.vis');
        const cap = document.querySelector('.cap p');
        if (vis) worst.visBot = Math.max(worst.visBot, y(vis.getBoundingClientRect().bottom));
        if (cap) {
          const b = cap.getBoundingClientRect();
          worst.capBot = Math.max(worst.capBot, y(b.bottom));
          worst.capTop = Math.min(worst.capTop, y(b.top));
        }
      }
      return worst;
    })()`);
    add(w.visBot <= CONTENT_BOTTOM + 1, `콘텐츠 안전영역 (비주얼 ≤ ${CONTENT_BOTTOM})`,
      `비주얼 바닥 ${w.visBot.toFixed(0)} (한계 ${CONTENT_BOTTOM})`);
    add(w.capTop >= CONTENT_BOTTOM - 1 && w.capBot <= CAP_BOTTOM_MAX,
      `자막영역 ${CONTENT_BOTTOM}~${CAP_BOTTOM_MAX} 안`,
      `자막 머리 ${w.capTop.toFixed(0)} · 바닥 ${w.capBot.toFixed(0)}` +
      (w.capTop < CONTENT_BOTTOM - 1 ? ' — 🔴 자막이 콘텐츠 영역을 침범 (줄 수 확인)' : ''));
  } else {

  const SAFE_BOTTOM = 1477;
  /* 🔴 2026-08-10 정정 — 옛 판은 `.cap` 과 `.vis` **컨테이너**를 쟀다. 둘 다
     `position:absolute` 에 `bottom` 이 고정이라 **어떤 대본으로도 실패할 수 없었다**
     (CSS 상수를 상수와 비교). 이 트랙의 **두 번째 「항상 참인 검사」**다(첫째 = 피크 천장,
     `synth()` 가 스스로 gain 을 깎아 출력이 정의상 못 넘는다).
     🔑 게이트를 새로 만들 때마다 물어라 — **이 검사가 실패할 수 있는 입력이 존재하는가.**
     지금은 **안에서 실제로 그려진 것**(자막 `p` · 샷 자식)을 잰다. */
  const low = await cdp.evaluate(`(()=>{
    const st = document.querySelector('.stage') || document.body;
    const S = st.getBoundingClientRect();
    const y = r => (r.bottom - S.top) / S.height * 1920;
    const worst = {};
    const put = (k, v) => { if (!(worst[k] >= v)) worst[k] = v; };
    // 자막은 페이드 중 translateY 로 움직인다. 한 프레임만 보면 안 되고 전 구간에서 최저점을 본다.
    const N = 24;
    for (let i = 0; i < N; i++) {
      window.seek(window.TOTAL * i / (N - 1));
      const p = document.querySelector('.cap p');
      if (p && p.textContent.trim()) put('자막 글자', y(p.getBoundingClientRect()));
      for (const el of document.querySelectorAll('.vis .shot')) {
        if (el.offsetParent === null) continue;
        const r = el.getBoundingClientRect();
        if (r.height > 0) put('샷 내용', y(r));
      }
    }
    return worst;
  })()`);
  const over = Object.entries(low).filter(([, v]) => v > SAFE_BOTTOM);
  add(over.length === 0, `하단 안전영역 (바닥 ${1920 - SAFE_BOTTOM}px 비움)`,
    over.length
      ? over.map(([s, v]) => `${s} 바닥 ${v.toFixed(0)} > 한계 ${SAFE_BOTTOM}`).join(', ')
      : Object.entries(low).map(([k, v]) => `${k} ${v.toFixed(0)}`).join(' · ') + ` (한계 ${SAFE_BOTTOM})`);

  /* 🔴 아웃트로 카드 넘침 — ep15s 트랙 판정(2026-08-10)으로 신설.
     A 가 근본(내용이 상자를 넘는가), B 가 증상(넘친 것이 자막·HUD 와 겹치는가).
     🔑 **A 만 두면** 나중에 상자 좌표가 바뀔 때 못 잡고, **B 만 두면** 자막이 짧은 회차에서
     우연히 통과한다. 둘 다 둔다.
     🔴 **허용 오차 0.** 「N px 까지는 봐준다」는 순간 그 N 이 지어낸 임계값이 되어
     `ADR-V-11`(근거 없는 임계값 금지)에 걸린다. 값은 DOM 에서 나오고 임계값이 0 이라
     지어낼 여지가 없다 — `ADR-V-11` 본문이 *"관측되면 그때 근거를 갖고 만든다"* 로
     직접 허가하고, 실측 사고가 **둘**이다(ep15s-3 HUD 관통 · ep15s-1 `.oc-ai` 겹침).
     🔴 **소급 적용 금지** — `ADR-V-15` 로 `ep13s`·`ep14s` 는 재렌더하지 않는다.
     이 게이트에 `ep14s-1`(넘침 26.5 · 겹침 1.4)·`ep14s-4`(넘침 1.5)가 걸리지만
     **둘 다 화면상 깨끗함을 스틸로 확인했다**(상자 겹침 ≠ 글자 겹침). 업로드하는 사람이
     걸린 편을 보고 멈추지 않도록 여기 적어 둔다. */
  const oc = await cdp.evaluate(`(()=>{
    const card = document.getElementById('outroCard');
    if (!card) return null;
    const OUT = 3000, N = 12;   // 아웃트로 3초 창을 따로 훑는다 — 위 N=24 스윕은 여기 표본이 둘뿐이다
    let over = 0, hit = [];
    for (let i = 0; i < N; i++) {
      window.seek(window.TOTAL - OUT + OUT * i / (N - 1));
      if (card.hidden) continue;
      const box = card.getBoundingClientRect();
      const kids = [...card.children].filter(el => !el.hidden && el.offsetParent !== null);
      if (!kids.length) continue;
      let top = Infinity, bot = -Infinity;
      for (const el of kids) { const r = el.getBoundingClientRect(); top = Math.min(top, r.top); bot = Math.max(bot, r.bottom); }
      over = Math.max(over, (box.top - top) + (bot - box.bottom));
      for (const el of kids) {
        const a = el.getBoundingClientRect();
        for (const sel of ['.cap p', '.hud']) {
          const o = document.querySelector(sel); if (!o) continue;
          if (sel === '.cap p' && !o.textContent.trim()) continue;
          const b = o.getBoundingClientRect();
          const ov = Math.min(a.bottom, b.bottom) - Math.max(a.top, b.top);
          const oh = Math.min(a.right, b.right) - Math.max(a.left, b.left);
          if (ov > 0 && oh > 0) hit.push(\`\${el.className || el.id} × \${sel} \${ov.toFixed(1)}px\`);
        }
      }
    }
    return { over, hit: [...new Set(hit)], fit: window.__ocFit || null };
  })()`);
  if (oc) {
    /* 🔑 사다리 칸을 함께 찍는다 — **0칸이면 옛 회차와 픽셀이 같다**는 뜻이고,
       1칸 이상이면 그 편은 원래 넘치고 있었다는 뜻이다(엔진 수정의 영향 범위가 보인다). */
    const rung = oc.fit ? ` · 사다리 ${oc.fit.rung}칸${oc.fit.rung === 0 ? '(현행 그대로)' : ''}` : '';
    add(oc.over <= 0, '아웃트로 카드 넘침 없음',
      (oc.over > 0 ? `내용이 상자를 ${oc.over.toFixed(1)}px 넘는다 — 형제 줄 수·outro.next 길이를 줄여라`
                   : '내용이 상자 안') + rung);
    add(oc.hit.length === 0, '아웃트로 카드 × 자막·HUD 겹침 없음',
      oc.hit.length ? oc.hit.join(' · ') : '겹침 0');
  }
  }

  frame = await cdp.evaluate(`(async () => {
    const FPS = ${FPS}, N = Math.max(2, Math.floor(window.TOTAL/1000*FPS));
    const per = {};
    let prev = null, prevId = null;
    for (let i = 0; i < N; i++) {
      await window.seek(i / FPS * 1000);   // 클립 샷은 seek 이 비디오 프레임 정합 약속을 돌려준다 (W5)
      const el = document.querySelector('.shot.on');
      const id = el?.dataset.id; if (!id) continue;
      const cv = el.querySelector('canvas');
      if (!per[id]) per[id] = { frames:0, bad:0, total:0, staticRun:0, maxStatic:0, edgeBot:0, edgeSide:0, edgeSideFrames:0 };
      const P = per[id]; P.frames++;
      if (!cv || !cv.width) {
        /* 클립 샷 (롱폼 W5) — 캔버스가 없다. 팔레트·가장자리는 게임 화면의 규격이
           아니라서 재지 않지만(3색 규약은 모션그래픽의 것), **정적은 여기서도 잡는다**
           — 멈춘 클립은 멈춘 캔버스와 같은 결함이다 (W2 DoD: 예외를 두지 않는다). */
        const v = window.__activeClipVideo ? window.__activeClipVideo() : null;
        if (v) {
          const oc = window.__clipProbe
            || (window.__clipProbe = Object.assign(document.createElement('canvas'), { width: 160, height: 90 }));
          const og = oc.getContext('2d', { willReadFrequently: true });
          og.drawImage(v, 0, 0, 160, 90);
          const dd = og.getImageData(0, 0, 160, 90).data;
          const Lc = new Uint8Array(160 * 90);
          for (let p2 = 0; p2 < dd.length; p2 += 4)
            Lc[p2 >> 2] = (dd[p2] * 77 + dd[p2 + 1] * 151 + dd[p2 + 2] * 28) >> 8;
          if (prev && prevId === id && prev.length === Lc.length) {
            let diff = 0;
            for (let j = 0; j < Lc.length; j += 3) diff += Math.abs(Lc[j] - prev[j]);
            const m = diff / (Lc.length / 3) / 255;
            if (m < 0.0008) { P.staticRun++; P.maxStatic = Math.max(P.maxStatic, P.staticRun); }
            else P.staticRun = 0;
          } else P.staticRun = 0;
          prev = Lc; prevId = id;
        } else { prev = null; prevId = id; }
        continue;
      }
      const g = cv.getContext('2d');
      const d = g.getImageData(0,0,cv.width,cv.height).data;

      /* 3색 판정은 '색상(hue)'으로 본다.
         🔴 처음엔 정확한 값 비교로 짰다가 멀쩡한 화면이 무더기로 걸렸다. 흰 막대 위에
         그린 글자를 얹으면 안티에일리어싱 경계에 두 색의 중간값이 생기는데, 그건
         팔레트 위반이 아니라 허용된 두 색이 섞인 자리다. 채도가 낮으면(회색 계열)
         통과, 채도가 있으면 그린(#00FF88, 색상 152도) 근처인지만 본다.
         주황(색상 17도)인 제품 마크는 이 검사에 정확히 걸린다 — 그래서 예외를 선언한다.

         밝기는 알파를 곱해서 낸다. 캔버스가 투명 배경이라 색 채널만 보면 흐릿한 칠도
         255 로 읽혀 움직임이 없는 것처럼 보인다. */
      let bad = 0, tot = 0;
      const L = new Uint8Array((d.length>>2));
      for (let p = 0; p < d.length; p += 4) {
        const r=d[p], gg=d[p+1], b=d[p+2], a=d[p+3];
        L[p>>2] = (((r*77+gg*151+b*28)>>8) * a / 255) | 0;
        /* 알파 64 미만은 세지 않는다. 8비트 프리멀티플라이드 합성은 아주 옅은 칠에서
           색이 흔들린다 — 실측으로 (0,255,136,a=12) 이 (0,255,212) 로 읽혔다(색상 152→170).
           검정 위 알파 64 미만은 밝기가 25% 미만이라 색이 뜻을 갖지 못한다. 반올림 잡음을
           위반으로 세면 점검이 늑대 소년이 된다. */
        if (a < 64) continue;
        tot++;
        const mx=Math.max(r,gg,b), mn=Math.min(r,gg,b);
        /* 검정. mx===0 만 거르면 안 된다 — rgb(0,1,0) 같은 값이 채도 1.0 · 색상 120도로
           계산돼 위반으로 잡힌다(실측: ep00s 한 회차에서 8샷 41만 픽셀). 눈에는 검정이다.
           최대 채널이 9% 미만이면 색이 아니라 검정으로 본다. */
        if (mx < 24) continue;
        const sat = (mx-mn)/mx;
        if (sat <= 0.14) continue;                    // 회색 계열 = 검정~흰색 사이
        let hue;                                      // 0~360
        const c = mx-mn;
        if (mx === r) hue = 60*(((gg-b)/c)%6);
        else if (mx === gg) hue = 60*(((b-r)/c)+2);
        else hue = 60*(((r-gg)/c)+4);
        if (hue < 0) hue += 360;
        /* 🔄 2026-08-12 개정 — 창이 둘이다. 옛 규칙은 강조색이 하나뿐이라
           「결함」을 그릴 색이 없었다(lib.js PALETTE 주석 참조).
           🔴 fail 창을 336~6 으로 좁게 잡은 이유: 제품 마크의 주황이 hue 17 이다.
           창을 주황까지 늘리면 결함의 색과 브랜드 마크가 같은 색이 되고,
           지금 예외로 선언해 통과시키는 마크가 조용히 규격 안으로 들어와 버린다. */
        const accent = (hue >= 138 && hue <= 168)     // #00FF88 = 152도 (해결)
                    || (hue >= 336 || hue <= 6);      // #FF3B5C = 350도 (결함)
        if (!accent) bad++;
      }
      P.bad += bad; P.total += tot;

      /* 가장자리에 칠이 닿는가 = 밖으로 나간 것이 잘린 흔적.
         🔴 오래 아래쪽만 봤다. 그래서 ep00s 는 도장이 오른쪽으로 잘린 채 나갔고(사람이 잡음),
         ep02s 첫 무인 실행은 "GOAP 목표 : HungerLevel" 이 "HungerLe" 로 잘린 채
         **점검 13종을 전부 통과했다.** 무인이면 눈이 없으니 기계가 봐야 한다.
         ⚠️ 이 블록은 템플릿 문자열 안이다 — 주석에 백틱을 쓰면 문자열이 끊긴다(실제로 겪었다).
         좌우도 같은 방법으로 잴 수 있었는데 안 재고 있었을 뿐이다. */
      const W=cv.width, H=cv.height;
      let e=0; const row=g.getImageData(0,H-1,W,1).data;
      for (let x=0;x<W;x++) if (row[x*4+3] > 10) e++;
      P.edgeBot = Math.max(P.edgeBot, e);

      /* 좌우는 아래와 다르게 재야 한다. 두 번 틀리고 나서 얻은 규칙이다.
         ① **맨 끝 한 열만 보면 놓친다.** ep02s S4 의 잘린 라벨은 969열이 0 이고 968열부터
            잉크가 있었다(안티에일리어싱이 마지막 열에서 죽는다). → 바깥 4열을 띠로 본다.
         ② **한 프레임이라도 닿으면 실패로 하면 오탐한다.** 화면을 쓸고 지나가는 그림
            (ep01s 의 erasure)은 가장자리를 지나는 게 설계다. → "몇 프레임이나 닿아 있었나"를
            센다. 스쳐 가는 연출은 잠깐이고, **잘린 글자는 몇 초씩 그 자리에 서 있다.** */
      /* 띠 폭 2 는 실측으로 정했다. 4 로 잡았더니 ep01s 의 shelf 가 100% 로 걸렸는데
         눈으로 보니 안 잘렸다 — 가장자리에서 **3px 안쪽**에 정상적으로 그려진 요소였다.
         진짜 잘린 글자(ep02s S4)는 **1px 앞**에 잉크가 선다(맨 끝 열은 안티에일리어싱으로 비어도).
         2 로 좁히니 승인된 두 회차는 통과하고 잘린 것만 남았다. */
      const BAND = 2;
      let sx = 0;
      for (let i = 0; i < BAND; i++) {
        const l = g.getImageData(i, 0, 1, H).data, r = g.getImageData(W - 1 - i, 0, 1, H).data;
        for (let y = 0; y < H; y++) { if (l[y*4+3] > 10) sx++; if (r[y*4+3] > 10) sx++; }
      }
      if (sx >= 3) { P.edgeSideFrames++; P.edgeSide = Math.max(P.edgeSide, sx); }

      /* 정적 구간.
         🔴 아웃트로 카드가 덮는 구간은 세지 않는다(2026-08-04). .outrocard 는 .vis 와 좌표가
         한 픽셀도 다르지 않고 배경이 불투명 z-index 3 이라, 마지막 OUTRO_MS 동안 캔버스는
         **설계상 아무도 못 본다.** 그런데 이 검사는 .shot.on canvas 픽셀만 읽으므로 그 구간의
         정지를 세서 회차에 경고를 냈다 — 검수 세 팀이 각각 짚었고, 훅이 카드로 옮겨간 뒤로는
         그 구간에 캔버스가 움직일 이유 자체가 없어졌다.
         🔑 이것은 기준을 무르게 하는 것이 아니라 **측정을 보이는 것으로 좁히는 것**이다.
         실제로 ep05s-2 는 이 패치 뒤에도 보이는 정적이 2.6초로 남아 3.0초 코앞이다. */
      const OUTRO_MS = 3000;   // engine.js 의 같은 이름 상수와 맞춰라 (2026-08-08 ADR-V25-10)
      const covered = (i / FPS * 1000) >= window.TOTAL - OUTRO_MS;
      if (covered) { P.staticRun = 0; prev = L; prevId = id; continue; }
      if (prev && prevId === id && prev.length === L.length) {
        let diff = 0;
        for (let j=0;j<L.length;j+=3) diff += Math.abs(L[j]-prev[j]);
        const m = diff/(L.length/3)/255;
        if (m < 0.0008) { P.staticRun++; P.maxStatic = Math.max(P.maxStatic, P.staticRun); }
        else P.staticRun = 0;
      } else P.staticRun = 0;
      prev = L; prevId = id;
    }
    for (const k in per) per[k].maxStaticSec = +(per[k].maxStatic / FPS).toFixed(1);
    return { fps: FPS, per };
  })()`);
  /* 결정성 — 이 프로젝트의 1번 규약이자 mp4 추출의 전제다.
     같은 시각을 어느 순서로 그려도 같은 그림이어야 한다. 깨지면 프레임이 흔들린다.
     회차마다 자동으로 보게 둔다 — 사람이 기억해서 돌릴 규칙은 언젠가 안 돌린다. */
  const det = await cdp.evaluate(`(()=>{
    const FPS=${FPS}, N=Math.max(2, Math.floor(window.TOTAL/1000*FPS));
    const sig=()=>{const el=document.querySelector('.shot.on');const cv=el?.querySelector('canvas');
      let h=2166136261;
      if(cv&&cv.width){const d=cv.getContext('2d').getImageData(0,0,cv.width,cv.height).data;
        for(let i=0;i<d.length;i+=11){h^=d[i];h=Math.imul(h,16777619);}}
      const s=(document.getElementById('cap').textContent||'')+'|'+(el?.style.transform||'')+'|'+(el?.style.clipPath||'')+'|'+(el?.innerHTML||'');
      for(let i=0;i<s.length;i++){h^=s.charCodeAt(i);h=Math.imul(h,16777619);}
      return (h>>>0).toString(36);};
    const pass=o=>{const r=[];for(const i of o){window.seek(i/FPS*1000);r.push(sig());}return r;};
    const fwd=[...Array(N).keys()];
    const a=pass(fwd), b=pass([...fwd].reverse()).reverse();
    const co=[...fwd].filter((_,i)=>i%3===0).concat([...fwd].filter((_,i)=>i%3!==0));
    const c=pass(co); const cm={}; co.forEach((f,i)=>cm[f]=c[i]);
    let m=0; for(const f of fwd) if(a[f]!==b[f]||a[f]!==cm[f]) m++;
    return {frames:N, mismatch:m};
  })()`);
  add(det.mismatch === 0, '결정성 (3패스)',
    det.mismatch ? `${det.frames}프레임 중 ${det.mismatch} 불일치` : `${det.frames}프레임 불일치 0`);
} finally { close(); }

if (frame) {
  const rows = Object.entries(frame.per);

  const violations = rows
    .filter(([id]) => !paletteSkip[id])
    .map(([id, v]) => ({ id, pct: v.total ? v.bad / v.total * 100 : 0 }))
    .filter(v => v.pct > 0.05);
  add(violations.length === 0, '팔레트 (해결·결함 2강조)',
    violations.length
      ? violations.map(v => `${v.id} ${v.pct.toFixed(2)}%`).join(', ')
      : `위반 없음` + (Object.keys(paletteSkip).length ? ` (선언 예외: ${Object.entries(paletteSkip).map(([k, r]) => `${k}=${r}`).join(', ')})` : ''));

  const stat = rows.filter(([, v]) => v.maxStaticSec > 3.0);
  add(stat.length === 0, '정적 구간 3초 이하',
    stat.length ? stat.map(([id, v]) => `${id} ${v.maxStaticSec}s`).join(', ')
      : `최대 ${Math.max(...rows.map(([, v]) => v.maxStaticSec)).toFixed(1)}s`, 'warn');

  /* 잘린 글자는 몇 초씩 서 있고, 쓸고 지나가는 그림은 스친다. 그 차이로 가른다.
     샷 프레임의 4분의 1 넘게 가장자리에 붙어 있으면 잘린 것으로 본다. */
  const side = rows.filter(([id, v]) => !edgeSkip[id] && v.frames && v.edgeSideFrames / v.frames > 0.25);
  add(side.length === 0, '좌우 가장자리 잘림 없음',
    side.length
      ? side.map(([id, v]) => `${id} ${Math.round(v.edgeSideFrames / v.frames * 100)}% 구간 · 최대 ${v.edgeSide}px`).join(', ')
      : '없음' + (Object.keys(edgeSkip).length ? ` (선언 예외: ${Object.keys(edgeSkip).join(', ')})` : ''));

  const bled = rows.filter(([id, v]) => v.edgeBot > 0 && !edgeSkip[id]);
  add(bled.length === 0, '아래 가장자리 잘림 없음',
    bled.length ? bled.map(([id, v]) => `${id} ${v.edgeBot}px`).join(', ')
      : '없음' + (Object.keys(edgeSkip).length ? ` (선언 예외: ${Object.entries(edgeSkip).map(([k, r]) => `${k}=${r}`).join(', ')})` : ''));
}

/* ── 보고 ────────────────────────────────────────── */
const fails = results.filter(r => r.level === 'fail');
const warns = results.filter(r => r.level === 'warn');

if (AS_JSON) {
  console.log(JSON.stringify({ ep: EP, pass: fails.length === 0, results }, null, 2));
} else {
  console.log(`\n가이드 점검 · ${EP}\n${'─'.repeat(52)}`);
  /* 🔴 대상 파일 지문 (2026-08-11 신설) — 게이트 출력이 「증거」로 인용되는데, 출력 뒤에
     파일이 바뀌면 낡은 증거가 된다(ep16s-5 에서 마스터가 수리본을 크기 오독으로 되돌린 실물).
     지문이 출력에 박혀 있으면 대조가 산수가 된다. 검수·마스터는 이 줄째로 인용할 것. */
  for (const [name, f] of [['scene.json', bakeScene(EP, LANG)],
                           ['timed.json', path.join(ROOT, 'episodes', EP, 'build', 'timed.json')]]) {
    if (!fs.existsSync(f)) continue;
    const st = fs.statSync(f);
    const md5 = crypto.createHash('md5').update(fs.readFileSync(f)).digest('hex');
    console.log(`  지문 ${name.padEnd(10)} ${st.size}B · mtime ${st.mtime.toISOString()} · md5 ${md5}`);
  }
  for (const r of results) {
    const mark = r.level === 'pass' ? '  OK ' : r.level === 'warn' ? '  ⚠  ' : '  🔴 ';
    console.log(`${mark}${r.name.padEnd(22)} ${r.detail}`);
  }
  console.log('─'.repeat(52));
  console.log(fails.length === 0
    ? (warns.length ? `통과 — 경고 ${warns.length}건은 확인 권장` : '전부 통과')
    : `🔴 ${fails.length}건 실패`);
}
process.exit(fails.length ? 1 : 0);
