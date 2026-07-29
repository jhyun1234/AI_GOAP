/* 최초 1회만 실행하는 YouTube OAuth 인증 스크립트.
   실행: node tools/scene-video/upload/youtube-auth.js
   브라우저가 열리면 로그인 후 "허용"을 누른다. 끝나면 credentials/youtube_token.json 에
   refresh_token 이 저장되고, 이후 업로드는 사람 개입 없이 이 파일로 토큰을 갱신한다.

   ── 이 스크립트를 돌리기 전에 사람이 해야 하는 일 (내가 대신 못 한다) ──────────────
   1. Google Cloud Console 에서 프로젝트를 열고 **YouTube Data API v3** 를 사용 설정한다.
      (블로그 자동화가 쓰는 프로젝트를 그대로 써도 된다 — API 만 추가로 켜면 된다)
   2. OAuth 동의 화면 > 데이터 액세스에 범위 `.../auth/youtube.upload` 를 추가한다.
   3. 🔴 게시 상태를 **"프로덕션"** 으로 바꾼다.
      "테스트" 상태로 두면 refresh_token 이 **7일마다 만료**되어 자동화가 매주 끊긴다.
      (미인증 앱이라 동의 화면에 경고가 뜨는데, 본인 계정이면 "고급 > 계속"으로 통과된다)
   4. 사용자 인증 정보 > OAuth 클라이언트 ID(**데스크톱 앱**)를 만들어 JSON 을 받아
      `tools/scene-video/upload/credentials/client_secret.json` 으로 저장한다.
      (블로그 쪽 `tools/blog-automation/credentials/client_secret.json` 을 복사해도 된다)

   ⚠️ 업로드 공개 범위에 대한 사실 하나:
      API 감사(audit)를 통과하지 않은 프로젝트로 올린 영상은 **YouTube 가 강제로 비공개**로
      만든다. privacy 를 public 으로 적어도 마찬가지다. 그러니 이 파이프라인의 정상 동작은
      "비공개로 올려두면 사람이 스튜디오에서 공개로 전환" 이다. 이건 제약이자 안전장치다.
*/

const fs = require('fs');
const http = require('http');
const https = require('https');
const path = require('path');
const { exec } = require('child_process');

const CRED_DIR = path.join(__dirname, 'credentials');
const CLIENT_SECRET_PATH = path.join(CRED_DIR, 'client_secret.json');
const TOKEN_PATH = path.join(CRED_DIR, 'youtube_token.json');
const SCOPE = 'https://www.googleapis.com/auth/youtube.upload';

function tokenRequest(tokenUri, params) {
  const body = new URLSearchParams(params).toString();
  return new Promise((resolve, reject) => {
    const req = https.request(tokenUri, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
        'Content-Length': Buffer.byteLength(body)
      }
    }, res => {
      // 청크는 Buffer 로 모았다가 끝에서 한 번만 디코드한다 — 에러 메시지에 한글이 섞이면
      // 이어붙이기 방식은 멀티바이트가 경계에 걸려 깨진다(블로그 클라이언트와 같은 규약).
      const chunks = [];
      res.on('data', c => chunks.push(Buffer.isBuffer(c) ? c : Buffer.from(c)));
      res.on('end', () => {
        const data = Buffer.concat(chunks).toString('utf8');
        try {
          const parsed = JSON.parse(data);
          if (res.statusCode >= 400) return reject(new Error(JSON.stringify(parsed)));
          resolve(parsed);
        } catch (e) { reject(new Error(data.slice(0, 400))); }
      });
    });
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

async function main() {
  if (!fs.existsSync(CLIENT_SECRET_PATH)) {
    console.error(`client_secret.json 이 없다:\n  ${CLIENT_SECRET_PATH}\n`);
    console.error('이 파일 맨 위 주석의 1~4단계를 먼저 끝내야 한다.');
    process.exit(1);
  }
  const conf = JSON.parse(fs.readFileSync(CLIENT_SECRET_PATH, 'utf8'));
  const inst = conf.installed || conf.web;
  if (!inst) throw new Error('client_secret.json 에 installed/web 항목이 없다');
  const { client_id, client_secret, auth_uri, token_uri } = inst;

  const server = http.createServer();
  server.listen(0, '127.0.0.1');
  await new Promise(r => server.on('listening', r));
  const port = server.address().port;
  const redirectUri = `http://localhost:${port}`;

  const authUrl = `${auth_uri || 'https://accounts.google.com/o/oauth2/auth'}?` +
    new URLSearchParams({
      client_id, redirect_uri: redirectUri, response_type: 'code',
      scope: SCOPE,
      access_type: 'offline',
      prompt: 'consent'              // refresh_token 을 반드시 받기 위해
    }).toString();

  console.log('\n브라우저에서 아래 주소를 열고 "허용"을 누른다:\n');
  console.log(authUrl + '\n');
  exec(`start "" "${authUrl}"`, () => { });

  const code = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('5분 안에 동의가 끝나지 않았다')), 300000);
    server.on('request', (req, res) => {
      const u = new URL(req.url, redirectUri);
      const c = u.searchParams.get('code');
      const err = u.searchParams.get('error');
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      res.end(`<meta charset="utf-8"><body style="font-family:sans-serif;padding:40px">
        <h2>${c ? '인증 완료 — 창을 닫아도 됩니다.' : '인증 실패: ' + err}</h2></body>`);
      clearTimeout(timer);
      c ? resolve(c) : reject(new Error(err || 'code 를 못 받았다'));
    });
  });

  const tok = await tokenRequest(token_uri || 'https://oauth2.googleapis.com/token', {
    code, client_id, client_secret, redirect_uri: redirectUri, grant_type: 'authorization_code'
  });
  if (!tok.refresh_token)
    throw new Error('refresh_token 이 안 왔다. 계정의 앱 권한을 지우고 다시 시도할 것.');

  fs.mkdirSync(CRED_DIR, { recursive: true });
  fs.writeFileSync(TOKEN_PATH, JSON.stringify({
    refresh_token: tok.refresh_token,
    access_token: tok.access_token,
    expiry: Date.now() + (tok.expires_in || 3600) * 1000,
    scope: SCOPE
  }, null, 2));
  console.log(`\n저장 완료 → ${TOKEN_PATH}`);
  console.log('이제 node tools/scene-video/publish.mjs ep01s 로 올릴 수 있다.');
  server.close();
}

main().catch(e => { console.error('\n🔴 ' + e.message); process.exit(1); });
