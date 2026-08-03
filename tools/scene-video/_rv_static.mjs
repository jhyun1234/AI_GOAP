import { openEngine } from './lib-node.mjs';
const EP = process.argv[2];
const FPS = Number(process.argv[3] || 5);
const { cdp, close } = await openEngine(EP, { quiet: true });
const r = await cdp.evaluate(`(()=>{
  const FPS=${FPS}, N=Math.max(2,Math.floor(window.TOTAL/1000*FPS));
  let prev=null, prevId=null, run=0, runStart=0;
  const out=[];
  const flush=(ms)=>{ if(run>0) out.push({id:prevId,from:+runStart.toFixed(0),to:+ms.toFixed(0),sec:+(run/FPS).toFixed(2)}); run=0; };
  for(let f=0;f<N;f++){
    const ms=f/FPS*1000; window.seek(ms);
    const el=document.querySelector('.shot.on');
    const id=el?.dataset.id; if(!id) continue;
    const cv=el.querySelector('canvas');
    if(!cv||!cv.width){prev=null;prevId=id;continue;}
    const d=cv.getContext('2d').getImageData(0,0,cv.width,cv.height).data;
    const L=new Uint8Array(d.length>>2);
    for(let p=0;p<d.length;p+=4) L[p>>2]=(((d[p]*77+d[p+1]*151+d[p+2]*28)>>8)*d[p+3]/255)|0;
    if(prev&&prevId===id&&prev.length===L.length){
      let diff=0; for(let j=0;j<L.length;j+=3) diff+=Math.abs(L[j]-prev[j]);
      const m=diff/(L.length/3)/255;
      if(m<0.0008){ if(run===0) runStart=ms-1000/FPS; run++; }
      else flush(ms);
    } else flush(ms);
    prev=L; prevId=id;
  }
  flush(N/FPS*1000);
  return JSON.stringify(out);
})()`);
console.log(r);
await close();
