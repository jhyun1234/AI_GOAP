// 최초 1회만 실행하는 Blogger OAuth 인증 스크립트.
// 실행: node tools/blog-automation/scripts/blogger-auth.js
// 브라우저가 열리면 로그인 후 "허용"을 누른다. 완료되면 credentials/blogger_token.json에
// refresh_token이 저장되고, 이후 자동화는 이 파일을 이용해 사람 개입 없이 토큰을 갱신한다.

const fs = require('fs');
const http = require('http');
const https = require('https');
const path = require('path');
const { exec } = require('child_process');

const CRED_DIR = path.join(__dirname, '..', 'credentials');
const CLIENT_SECRET_PATH = path.join(CRED_DIR, 'client_secret.json');
const TOKEN_PATH = path.join(CRED_DIR, 'blogger_token.json');
const SCOPE = 'https://www.googleapis.com/auth/blogger';

function tokenRequest(tokenUri, params) {
  const body = new URLSearchParams(params).toString();
  return new Promise((resolve, reject) => {
    const req = https.request(
      tokenUri,
      {
        method: 'POST',
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
          'Content-Length': Buffer.byteLength(body),
        },
      },
      (res) => {
        let data = '';
        res.on('data', (chunk) => (data += chunk));
        res.on('end', () => {
          try {
            const parsed = JSON.parse(data);
            if (res.statusCode >= 400) return reject(new Error(JSON.stringify(parsed)));
            resolve(parsed);
          } catch (e) {
            reject(e);
          }
        });
      }
    );
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

async function main() {
  const { installed } = JSON.parse(fs.readFileSync(CLIENT_SECRET_PATH, 'utf8'));
  const { client_id, client_secret, auth_uri, token_uri } = installed;

  const server = http.createServer();
  server.listen(0, '127.0.0.1');
  await new Promise((resolve) => server.on('listening', resolve));
  const port = server.address().port;
  const redirectUri = `http://localhost:${port}`;

  const authUrl =
    `${auth_uri}?client_id=${encodeURIComponent(client_id)}` +
    `&redirect_uri=${encodeURIComponent(redirectUri)}` +
    `&response_type=code&access_type=offline&prompt=consent` +
    `&scope=${encodeURIComponent(SCOPE)}`;

  console.log('브라우저에서 인증 페이지를 엽니다. 열리지 않으면 아래 URL을 직접 열어주세요:\n');
  console.log(authUrl + '\n');
  exec(`start "" "${authUrl}"`);

  const code = await new Promise((resolve, reject) => {
    server.on('request', (req, res) => {
      const url = new URL(req.url, redirectUri);
      const code = url.searchParams.get('code');
      const error = url.searchParams.get('error');
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
      if (code) {
        res.end('<h2>인증 완료! 이 창은 닫으셔도 됩니다.</h2>');
        resolve(code);
      } else {
        res.end(`<h2>인증 실패: ${error || '알 수 없는 오류'}</h2>`);
        reject(new Error(error || 'no code returned'));
      }
    });
  });

  const tokens = await tokenRequest(token_uri, {
    code,
    client_id,
    client_secret,
    redirect_uri: redirectUri,
    grant_type: 'authorization_code',
  });

  tokens.obtained_at = Date.now();
  fs.writeFileSync(TOKEN_PATH, JSON.stringify(tokens, null, 2));
  console.log(`\n토큰 저장 완료: ${TOKEN_PATH}`);
  console.log('refresh_token이 포함되어 있는지 확인:', tokens.refresh_token ? 'OK' : '⚠️ 없음 (재시도 필요)');

  server.close();
  process.exit(0);
}

main().catch((err) => {
  console.error('인증 실패:', err.message);
  process.exit(1);
});
