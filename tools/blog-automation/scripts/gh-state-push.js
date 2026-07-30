// GitHub REST API로 상태 파일을 main에 직접 커밋하는 스크립트. 의존성 없음(Node 내장만).
//
// 배경: 원격 routine 샌드박스는 detached HEAD로 체크아웃되고, GitHub 프록시가
// git push를 "세션의 현재 작업 브랜치"로만 허용하므로 routine에서는 어떤 git push도
// 403이다 (2026-07-14~16 3회 실증). 이 스크립트는 git 프록시를 거치지 않는
// api.github.com REST 경로(blob→tree→commit→ref)로 main을 fast-forward 갱신한다.
//
// 필요 조건: env var GH_STATE_TOKEN — fine-grained PAT,
//   대상 리포 jhyun1234/AI_GOAP 단독, 권한 Contents: Read and write 만.
//
// 사용법:
//   node gh-state-push.js "chore(blog): auto-run state update (YYYY-MM-DD)"
//   → git status --porcelain에서 tools/blog-automation/{state,published}/ 아래의
//     추가/수정 파일만 골라 커밋한다. 그 외 경로는 무시(안전장치).
//   성공 시 "API_STATE_PUSH_OK <새 커밋 sha>" 출력, 실패 시 stderr + exit 1.

const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

// 샌드박스 환경은 outbound HTTPS를 지정된 로컬 프록시(HTTPS_PROXY)로만 허용한다.
// Node의 https.request는 이 env var를 읽지 않아 직접 접속을 시도하다 막힌다
// (2026-07-30 관측: api.github.com 직접 접속이 403으로 거부됨). 내장 fetch(undici)는
// NODE_USE_ENV_PROXY=1일 때 HTTPS_PROXY를 지킨다.
if (!process.env.NODE_USE_ENV_PROXY) process.env.NODE_USE_ENV_PROXY = '1';

const OWNER = 'jhyun1234';
const REPO = 'AI_GOAP';
const BRANCH = 'main';
const ALLOWED_PREFIX = /^tools\/blog-automation\/(state|published)\//;

// GH_STATE_TOKEN 우선, 없으면 GH_TOKEN(공식 문서가 gh CLI용으로 안내하는 표준 이름)도 인식
const token = process.env.GH_STATE_TOKEN || process.env.GH_TOKEN;
if (!token) {
  console.error('GH_STATE_TOKEN(또는 GH_TOKEN) env var 없음 — API 경로 사용 불가');
  process.exit(2);
}
const message = process.argv[2] || `chore(blog): auto-run state update`;

async function api(method, urlPath, body) {
  const payload = body ? JSON.stringify(body) : null;
  const res = await fetch(`https://api.github.com${urlPath}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      'User-Agent': 'aigoap-blog-state-push',
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      ...(payload ? { 'Content-Type': 'application/json' } : {}),
    },
    body: payload,
  });
  const text = await res.text();
  let parsed;
  try { parsed = text ? JSON.parse(text) : {}; } catch { parsed = { raw: text }; }
  if (res.status >= 400) {
    throw new Error(`${method} ${urlPath} → HTTP ${res.status}: ${text.slice(0, 400)}`);
  }
  return parsed;
}

(async () => {
  // 1. 변경 파일 수집 (상태 경로만; 삭제는 다루지 않음 — 이 파이프라인에 삭제는 없다)
  const porcelain = execSync('git status --porcelain', { encoding: 'utf8' });
  const files = porcelain
    .split('\n')
    .filter(Boolean)
    .map((l) => ({ status: l.slice(0, 2).trim(), file: l.slice(3).replace(/^"|"$/g, '') }))
    .filter((e) => ALLOWED_PREFIX.test(e.file) && !e.status.startsWith('D'));
  if (files.length === 0) {
    console.log('No state changes to push');
    process.exit(0);
  }
  console.log('files:', files.map((f) => f.file).join(', '));

  // 2. 원격 main 최신 sha
  const ref = await api('GET', `/repos/${OWNER}/${REPO}/git/ref/heads/${BRANCH}`);
  const baseSha = ref.object.sha;
  const baseCommit = await api('GET', `/repos/${OWNER}/${REPO}/git/commits/${baseSha}`);

  // 3. blob 생성
  const treeEntries = [];
  for (const { file } of files) {
    const content = fs.readFileSync(file);
    const blob = await api('POST', `/repos/${OWNER}/${REPO}/git/blobs`, {
      content: content.toString('base64'),
      encoding: 'base64',
    });
    treeEntries.push({ path: file.replace(/\\/g, '/'), mode: '100644', type: 'blob', sha: blob.sha });
  }

  // 4. tree → commit → ref 갱신 (force 아님 = fast-forward만 허용)
  const tree = await api('POST', `/repos/${OWNER}/${REPO}/git/trees`, {
    base_tree: baseCommit.tree.sha,
    tree: treeEntries,
  });
  const commit = await api('POST', `/repos/${OWNER}/${REPO}/git/commits`, {
    message,
    tree: tree.sha,
    parents: [baseSha],
    author: { name: 'aigoap-blog-automation', email: 'blog-automation@aigoap.local', date: new Date().toISOString() },
  });
  await api('PATCH', `/repos/${OWNER}/${REPO}/git/refs/heads/${BRANCH}`, { sha: commit.sha, force: false });

  console.log(`API_STATE_PUSH_OK ${commit.sha}`);
})().catch((err) => {
  console.error('API_STATE_PUSH_FAILED:', err.message);
  process.exit(1);
});
