// Blogger API v3 최소 클라이언트. 의존성 없음(Node 내장 모듈만 사용).
// blog-publisher 에이전트가 Bash로 이 스크립트를 호출해 실제 게시를 수행한다.
//
// 사용법:
//   node blogger-client.js token                 → 유효한 access_token 출력 (필요 시 자동 갱신)
//   node blogger-client.js get-blog               → 블로그 정보 조회 (연결 확인용)
//   node blogger-client.js post --title-file t.txt --content-file c.html [--labels a,b,c] [--draft]
//                                                  → 신규 게시. 성공 시 게시된 글의 JSON(URL 포함) 출력
//   node blogger-client.js update --post-id <id> --title-file t.txt --content-file c.html [--labels a,b,c]
//                                                  → 기존 글(예: 이전 드라이런의 DRAFT) 내용 교체. 발행 상태는 유지됨
//
// 제목/본문은 파일로 넘긴다 — 셸 인자 이스케이프 문제(따옴표, 개행)를 피하기 위함.

const fs = require('fs');
const https = require('https');
const path = require('path');

const CRED_DIR = path.join(__dirname, '..', 'credentials');
const CLIENT_SECRET_PATH = path.join(CRED_DIR, 'client_secret.json');
const TOKEN_PATH = path.join(CRED_DIR, 'blogger_token.json');
const BLOG_CONFIG_PATH = path.join(CRED_DIR, 'blog_config.json');

// 원격 routine 실행 지원: credentials/ 파일이 없으면 env var에서 JSON을 읽는다.
// - BLOGGER_CLIENT_SECRET: client_secret.json 전체 내용 (JSON 문자열)
// - BLOGGER_TOKEN:         blogger_token.json 전체 내용 (JSON 문자열)
// - BLOG_CONFIG:           blog_config.json 전체 내용 (JSON 문자열)
// env var 경로에서는 refresh 후 새 access_token을 디스크에 되쓰지 않는다 —
// 다음 실행에서 다시 refresh_token으로 갱신하면 되므로 무해하다.
function loadJsonWithFallback(filePath, envName) {
  if (fs.existsSync(filePath)) {
    return { data: JSON.parse(fs.readFileSync(filePath, 'utf8')), fromEnv: false };
  }
  const envVal = process.env[envName];
  if (envVal) {
    return { data: JSON.parse(envVal), fromEnv: true };
  }
  throw new Error(`${filePath}도 없고 env ${envName}도 없음 — 원격 실행이면 routine env vars 설정 필요`);
}

function httpsJson(urlStr, { method = 'GET', headers = {}, body } = {}) {
  return new Promise((resolve, reject) => {
    const req = https.request(
      urlStr,
      { method, headers },
      (res) => {
        let data = '';
        res.on('data', (c) => (data += c));
        res.on('end', () => {
          let parsed;
          try {
            parsed = data ? JSON.parse(data) : {};
          } catch (e) {
            return reject(new Error(`응답 파싱 실패 (status ${res.statusCode}): ${data.slice(0, 500)}`));
          }
          if (res.statusCode >= 400) {
            return reject(new Error(`HTTP ${res.statusCode}: ${JSON.stringify(parsed)}`));
          }
          resolve(parsed);
        });
      }
    );
    req.on('error', reject);
    if (body) req.write(body);
    req.end();
  });
}

async function ensureAccessToken() {
  const { data: clientSecretJson, fromEnv: csFromEnv } = loadJsonWithFallback(CLIENT_SECRET_PATH, 'BLOGGER_CLIENT_SECRET');
  const { data: token, fromEnv: tokFromEnv } = loadJsonWithFallback(TOKEN_PATH, 'BLOGGER_TOKEN');
  const { installed } = clientSecretJson;

  const expiresAt = (token.obtained_at || 0) + (token.expires_in || 0) * 1000;
  const stillValid = Date.now() < expiresAt - 5 * 60 * 1000; // 5분 여유
  if (stillValid && token.access_token) return token.access_token;

  if (!token.refresh_token) {
    throw new Error('refresh_token이 없음 — blogger-auth.js를 재실행해 재인증 필요');
  }

  const body = new URLSearchParams({
    client_id: installed.client_id,
    client_secret: installed.client_secret,
    refresh_token: token.refresh_token,
    grant_type: 'refresh_token',
  }).toString();

  const refreshed = await httpsJson(installed.token_uri, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/x-www-form-urlencoded',
      'Content-Length': Buffer.byteLength(body),
    },
    body,
  });

  const merged = {
    ...token,
    access_token: refreshed.access_token,
    expires_in: refreshed.expires_in,
    obtained_at: Date.now(),
  };
  // env var 경로에서는 디스크에 쓰지 않는다 — 다음 실행에서 refresh_token으로 다시 갱신하면 됨.
  if (!tokFromEnv) {
    fs.writeFileSync(TOKEN_PATH, JSON.stringify(merged, null, 2));
  }
  return merged.access_token;
}

function loadBlogId() {
  const { data: cfg } = loadJsonWithFallback(BLOG_CONFIG_PATH, 'BLOG_CONFIG');
  if (!cfg.blogId) throw new Error('blog_config에 blogId 없음 (파일 또는 env BLOG_CONFIG)');
  return cfg.blogId;
}

function parseArgs(argv) {
  const args = { _: [] };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const next = argv[i + 1];
      if (next && !next.startsWith('--')) {
        args[key] = next;
        i++;
      } else {
        args[key] = true;
      }
    } else {
      args._.push(a);
    }
  }
  return args;
}

async function cmdToken() {
  const token = await ensureAccessToken();
  console.log(token);
}

async function cmdGetBlog() {
  const token = await ensureAccessToken();
  const blogId = loadBlogId();
  const result = await httpsJson(`https://www.googleapis.com/blogger/v3/blogs/${blogId}`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  console.log(JSON.stringify(result, null, 2));
}

async function cmdPost(args) {
  if (!args['title-file'] || !args['content-file']) {
    throw new Error('사용법: post --title-file <path> --content-file <path> [--labels a,b,c] [--draft]');
  }
  const title = fs.readFileSync(args['title-file'], 'utf8').trim();
  const content = fs.readFileSync(args['content-file'], 'utf8');
  const labels = args.labels ? String(args.labels).split(',').map((s) => s.trim()).filter(Boolean) : undefined;

  const token = await ensureAccessToken();
  const blogId = loadBlogId();
  const isDraft = !!args.draft;

  const payload = JSON.stringify({
    kind: 'blogger#post',
    title,
    content,
    ...(labels ? { labels } : {}),
  });

  const result = await httpsJson(
    `https://www.googleapis.com/blogger/v3/blogs/${blogId}/posts${isDraft ? '?isDraft=true' : ''}`,
    {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(payload),
      },
      body: payload,
    }
  );
  console.log(JSON.stringify(result, null, 2));
}

async function cmdUpdate(args) {
  if (!args['post-id'] || !args['title-file'] || !args['content-file']) {
    throw new Error('사용법: update --post-id <id> --title-file <path> --content-file <path> [--labels a,b,c]');
  }
  const title = fs.readFileSync(args['title-file'], 'utf8').trim();
  const content = fs.readFileSync(args['content-file'], 'utf8');
  const labels = args.labels ? String(args.labels).split(',').map((s) => s.trim()).filter(Boolean) : undefined;

  const token = await ensureAccessToken();
  const blogId = loadBlogId();

  const payload = JSON.stringify({
    kind: 'blogger#post',
    title,
    content,
    ...(labels ? { labels } : {}),
  });

  const result = await httpsJson(
    `https://www.googleapis.com/blogger/v3/blogs/${blogId}/posts/${args['post-id']}`,
    {
      method: 'PUT',
      headers: {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(payload),
      },
      body: payload,
    }
  );
  console.log(JSON.stringify(result, null, 2));
}

async function main() {
  const [cmd, ...rest] = process.argv.slice(2);
  const args = parseArgs(rest);
  if (cmd === 'token') return cmdToken();
  if (cmd === 'update') return cmdUpdate(args);
  if (cmd === 'get-blog') return cmdGetBlog();
  if (cmd === 'post') return cmdPost(args);
  console.error('알 수 없는 명령. token | get-blog | post | update 중 하나를 사용하세요.');
  process.exit(1);
}

main().catch((err) => {
  console.error('오류:', err.message);
  process.exit(1);
});
