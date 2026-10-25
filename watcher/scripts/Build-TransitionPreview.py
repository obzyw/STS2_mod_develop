"""Embed native PNG samples in one portable HTML preview; uses only Python's standard library."""
import argparse
import base64
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("frames", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    routes = []
    for source, target, label, duration in [
        ("Normal", "Calm", "普通 → 平静", .58),
        ("Calm", "Wrath", "平静 → 愤怒", .44),
        ("Wrath", "Divinity", "愤怒 → 神格", .68),
        ("Divinity", "Normal", "神格 → 普通", .52),
    ]:
        frames = [args.frames / f"{source}-{target}-{index:02}.png" for index in range(17)]
        routes.append({"label": label, "duration": duration, "frames": [
            "data:image/png;base64," + base64.b64encode(path.read_bytes()).decode("ascii")
            for path in frames
        ]})
    page = """<!doctype html>
<html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>观者姿态过渡预览</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#11121e;color:#e9e4fa;font:16px/1.6 system-ui,sans-serif}
main{max-width:1040px;margin:30px auto;padding:0 24px}h1{margin:0;font-size:28px}p{color:#aaa6bd}
nav{display:flex;gap:8px;flex-wrap:wrap;margin:20px 0}button,select{font:inherit;color:inherit;background:#282538;border:1px solid #514366;border-radius:8px;padding:7px 14px;cursor:pointer}
button[aria-pressed=true]{background:#704da5;border-color:#c69af2}.stage{border:1px solid #383047;border-radius:16px;text-align:center;background:radial-gradient(ellipse at 50% 80%,#302745,#181822 70%);overflow:hidden}
img{display:block;width:100%;max-width:660px;aspect-ratio:1;margin:auto;object-fit:contain}
.controls{display:flex;gap:16px;align-items:center;margin-top:16px}input{flex:1;accent-color:#c69af2;min-width:60px}output{min-width:80px}
</style>
<main><h1>观者 · 姿态过渡</h1><p>轻蓄力 → 光色交融 → 新姿态落定。原生骨架分帧预览，战斗中的背景眼睛与场景光效继续保留。</p>
<nav id="routes"></nav><div class="stage"><img id="frame" alt="观者姿态切换动画"></div>
<div class="controls"><button id="play">暂停</button><input id="seek" aria-label="动画进度" type="range" min="0" max="16" value="0"><select id="speed" aria-label="播放速度"><option value="1">正常速度</option><option value="0.5">半速观察</option></select><output id="duration"></output></div>
<p>循环播放在首尾稍作停留。可暂停、拖动进度或减速查看角色的衔接；神格接入已有的摊掌姿态。</p></main>
<script>
const routes = __ROUTES__;
const image=document.querySelector('#frame'), seek=document.querySelector('#seek'), play=document.querySelector('#play'), speed=document.querySelector('#speed');
let selected=0, playing=true, elapsed=0, last=performance.now();
const buttons=routes.map((route,i)=>{const button=document.createElement('button');button.textContent=route.label;button.onclick=()=>{selected=i;elapsed=0;render(0);};document.querySelector('#routes').append(button);return button;});
// Pre-decode the native samples so the short transition is not slowed by PNG decoding.
const pictures=routes.map(route=>route.frames.map(src=>{const pic=new Image();pic.src=src;return pic;}));
function render(index){image.src=pictures[selected][index].src;seek.value=index;document.querySelector('#duration').textContent=routes[selected].duration.toFixed(2)+' 秒';buttons.forEach((b,i)=>b.setAttribute('aria-pressed',i===selected));}
play.onclick=()=>{playing=!playing;play.textContent=playing?'暂停':'播放';};
seek.oninput=()=>{playing=false;play.textContent='播放';elapsed=.6+Number(seek.value)/16*routes[selected].duration;render(Number(seek.value));};
function tick(now){const delta=(now-last)/1000;last=now;if(playing){const route=routes[selected];elapsed=(elapsed+delta*Number(speed.value))%(route.duration+1.4);render(Math.max(0,Math.min(16,Math.floor((elapsed-.6)/route.duration*16))));}requestAnimationFrame(tick);}
Promise.all(pictures.flat().map(pic=>pic.decode())).then(()=>{render(0);last=performance.now();requestAnimationFrame(tick);});
</script></html>"""
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(page.replace("__ROUTES__", json.dumps(routes, ensure_ascii=False)), encoding="utf-8")
    print(f"Saved {len(routes)} routes / 68 native frames: {args.output}")


if __name__ == "__main__":
    main()
