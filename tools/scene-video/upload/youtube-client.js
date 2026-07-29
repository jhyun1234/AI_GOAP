/* YouTube Data API v3 최소 클라이언트. 의존성 없음(Node 내장 모듈만).
   블로그 자동화의 blogger-client.js 와 같은 규약을 따른다.

   사용:
     node youtube-client.js token                      유효한 access_token 출력(필요 시 갱신)
     node youtube-client.js whoami                     연결 확인 — 채널 이름 출력
     node youtube-client.js upload --meta m.json --file v.mp4
                                                        업로드. 성공 시 결과 JSON 출력

   원격 실행 지원: credentials/ 파일이 없으면 env 에서 읽는다
     YOUTUBE_CLIENT_SECRET / YOUTUBE_TOKEN
*/
const fs = require('fs');
const https = require('https');
const path = require('path');

const CRED_DIR = path.join(__dirname, 'credentials');
const CLIENT_SECRET_PATH = path.join(CRED_DIR, 'client_secret.json');
const TOKEN_PATH = path.join(CRED_DIR, 'youtube_token.json');

function loadJson(file, envName) {
  if (fs.existsSync(file)) return { data: JSON.parse(fs.readFileSync(file, 'utf8')), fromEnv: false };
  if (process.env[envName]) return { data: JSON.parse(process.env[envName]), fromEnv: true };
  throw new Error(`${file} 도 없고 env ${envName} 도 없다 — youtube-auth.js 를 먼저 돌려라`);
}

/** 청크를 Buffer 로 모았다가 끝에서 한 번만 디코드한다.
 *  이어붙이면 한글(UTF-8 3바이트)이 청크 경계에 걸릴 때 깨진다 — 블로그 쪽에서 실제로 겪었다. */
function request(url, { method = 'GET', headers = {}, body, bodyStream } = {}) {
  return new Promise((resolve, reject) => {
    const req = https.request(url, { method, headers }, res => {
      const chunks = [];
      res.on('data', c => chunks.push(Buffer.isBuffer(c) ? c : Buffer.from(c)));
      res.on('end', () => resolve({
        status: res.statusCode, headers: res.headers,
        text: Buffer.concat(chunks).toString('utf8')
      }));
    });
    req.on('error', reject);
    if (bodyStream) { bodyStream.pipe(req); bodyStream.on('error', reject); }
    else { if (body) req.write(body); req.end(); }
  });
}

async function accessToken() {
  const { data: conf } = loadJson(CLIENT_SECRET_PATH, 'YOUTUBE_CLIENT_SECRET');
  const { data: tok, fromEnv } = loadJson(TOKEN_PATH, 'YOUTUBE_TOKEN');
  const inst = conf.installed || conf.web;

  if (tok.access_token && tok.expiry && Date.now() < tok.expiry - 60000) return tok.access_token;

  const body = new URLSearchParams({
    client_id: inst.client_id, client_secret: inst.client_secret,
    refresh_token: tok.refresh_token, grant_type: 'refresh_token'
  }).toString();
  const r = await request(inst.token_uri || 'https://oauth2.googleapis.com/token', {
    method: 'POST', body,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'Content-Length': Buffer.byteLength(body) }
  });
  const j = JSON.parse(r.text);
  if (r.status >= 400) {
    // invalid_grant = refresh_token 이 죽었다. 대개 OAuth 앱이 "테스트" 상태라 7일 만료된 것
    const hint = /invalid_grant/.test(r.text)
      ? '\n  → refresh_token 이 만료됐다. OAuth 동의 화면 게시 상태를 "프로덕션"으로 바꾸고 youtube-auth.js 를 다시 돌려라.'
      : '';
    throw new Error('토큰 갱신 실패: ' + r.text + hint);
  }
  // env 경로에서는 디스크에 되쓰지 않는다 — 다음 실행에 다시 갱신하면 되므로 무해하다
  if (!fromEnv) fs.writeFileSync(TOKEN_PATH, JSON.stringify({
    ...tok, access_token: j.access_token, expiry: Date.now() + (j.expires_in || 3600) * 1000
  }, null, 2));
  return j.access_token;
}

async function whoami() {
  const t = await accessToken();
  const r = await request('https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true',
    { headers: { Authorization: `Bearer ${t}` } });
  if (r.status >= 400) throw new Error(r.text);
  const c = JSON.parse(r.text).items?.[0];
  if (!c) throw new Error('이 계정에 채널이 없다');
  return { id: c.id, title: c.snippet.title };
}

/** 재개 가능 업로드 — 큰 파일이 중간에 끊겨도 세션 URL 로 이어붙일 수 있는 정식 경로 */
async function upload(metaFile, videoFile) {
  const meta = JSON.parse(fs.readFileSync(metaFile, 'utf8'));
  const size = fs.statSync(videoFile).size;
  const t = await accessToken();
  const body = JSON.stringify(meta);

  const init = await request(
    'https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status',
    {
      method: 'POST', body,
      headers: {
        Authorization: `Bearer ${t}`,
        'Content-Type': 'application/json; charset=UTF-8',
        'Content-Length': Buffer.byteLength(body),
        'X-Upload-Content-Length': size,
        'X-Upload-Content-Type': 'video/mp4'
      }
    });
  if (init.status >= 400) throw new Error('세션 생성 실패: ' + init.text);
  const session = init.headers.location;
  if (!session) throw new Error('업로드 세션 URL 을 못 받았다');

  const put = await request(session, {
    method: 'PUT',
    headers: { 'Content-Length': size, 'Content-Type': 'video/mp4' },
    bodyStream: fs.createReadStream(videoFile)
  });
  if (put.status >= 400) throw new Error('업로드 실패: ' + put.text);
  return JSON.parse(put.text);
}

async function main() {
  const [cmd, ...rest] = process.argv.slice(2);
  const arg = n => { const i = rest.indexOf('--' + n); return i >= 0 ? rest[i + 1] : null; };
  if (cmd === 'token') console.log(await accessToken());
  else if (cmd === 'whoami') console.log(JSON.stringify(await whoami(), null, 2));
  else if (cmd === 'upload') {
    const r = await upload(arg('meta'), arg('file'));
    console.log(JSON.stringify({
      id: r.id, title: r.snippet?.title, privacy: r.status?.privacyStatus,
      url: `https://youtu.be/${r.id}`, studio: `https://studio.youtube.com/video/${r.id}/edit`
    }, null, 2));
  } else {
    console.error('명령: token | whoami | upload --meta <json> --file <mp4>');
    process.exit(1);
  }
}
if (require.main === module) main().catch(e => { console.error('🔴 ' + e.message); process.exit(1); });
module.exports = { accessToken, whoami, upload };
