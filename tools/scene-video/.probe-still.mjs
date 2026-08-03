import fs from 'fs';
import path from 'path';
import { openEngine, epBuild } from './lib-node.mjs';

const EP = process.argv[2];
const secs = String(process.argv[3]).split(',').map(Number);
const { cdp, close } = await openEngine(EP, { quiet: true });
const dir = '/tmp/claude-0/-home-user-AI-GOAP/21ce8092-076c-517f-a526-bcc57158c98e/scratchpad/stills';
fs.mkdirSync(dir, { recursive: true });
for (const sec of secs) {
  await cdp.evaluate(`window.seek(${sec * 1000})`);
  const s = await cdp.send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false });
  fs.writeFileSync(path.join(dir, `${sec}s.png`), Buffer.from(s.data, 'base64'));
  console.log('still', sec);
}
await close();
