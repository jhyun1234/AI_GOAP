import fs from 'fs';
import { openEngine, shot } from './lib-node.mjs';
const EP = process.argv[2];
const OUT = process.argv[3];
fs.mkdirSync(OUT, { recursive: true });
const { cdp, close } = await openEngine(EP, { quiet: true });
const tl = await cdp.evaluate('JSON.stringify(window.__timeline())');
fs.writeFileSync(`${OUT}/timeline.json`, tl);
console.log(tl);
for (const ms of process.argv.slice(4).map(Number))
  fs.writeFileSync(`${OUT}/${ms}.png`, await shot(cdp, ms));
await close();
