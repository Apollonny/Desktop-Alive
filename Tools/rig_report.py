"""Build a portable inspection gallery from genuine Unity parameter probe renders."""
from pathlib import Path
import json,html,hashlib,zipfile,sys
root=Path(__file__).resolve().parents[1]
folder=root/'Artifacts'/(sys.argv[1] if len(sys.argv)>1 else 'rig-audit')
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8-sig'))
profile=json.loads((folder/'animation-profile.json').read_text(encoding='utf-8-sig'))
first,last=manifest['probes'][0],manifest['probes'][-1]
assert first['parameter']==last['parameter']==''
assert (folder/first['file']).read_bytes()==(folder/last['file']).read_bytes(),'Neutral reference changed during capture; do not publish this gallery.'
manifest['neutralReferenceRepeat']='byte-identical'
manifest['visualAcceptance']='pending: parameter probes do not approve a collar rig'
verification=json.loads((folder/'render-verification.json').read_text())
assert verification['status']=='passed'
metrics={r['file']:r for r in verification['rows']}
cards=[]
labels={p['Id']:p['Name'] for p in json.loads((root/'165 218.cdi3.json').read_text(encoding='utf-8-sig'))['Parameters']}
for probe in manifest['probes']:
    file=folder/probe['file'];assert file.is_file()
    probe['displayName']=labels.get(probe['parameter'],'Neutral')
    label=(probe['parameter'] or 'Neutral')+' / '+probe['displayName']+' = '+str(probe['value'])
    if probe['facing']<0:label+=' (mirrored)'
    probe['sha256']=hashlib.sha256(file.read_bytes()).hexdigest()
    metric=metrics[probe['file']]
    assert metric['sha256']==probe['sha256'],'Capture changed after verification'
    warning=f"<p>Referansa göre değişen piksel: {metric['changedPixels']:,}. Geometri farkı: {metric['maxVertexError']:.6f}. Görünürlük uyuşmazlığı: {metric['visibilityMismatches']}.</p>"
    cards.append(f'<article data-label="{html.escape(label)}"><h2>{html.escape(label)}</h2><div class="portrait"><img loading="lazy" src="{file.name}" alt="{html.escape(label)}"><svg viewBox="0 0 512 768"><path class="foot"/><path class="collar"/></svg></div><p>{file.name}</p>{warning}</article>')
page='''<!doctype html><html lang="tr"><meta charset="utf-8"><title>Mika — rig incelemesi</title>
<style>body{margin:0;background:#10151f;color:#eef2fa;font:16px system-ui}header{padding:30px;max-width:1100px}h1{margin:0 0 12px}p{line-height:1.6;color:#bbc7d8}input{padding:10px;background:#222d40;color:white;border:1px solid #7184a0;border-radius:6px}main{padding:20px;display:grid;grid-template-columns:repeat(auto-fit,minmax(250px,1fr));gap:16px}article{background:#1b2433;border:1px solid #344257;border-radius:10px;padding:12px}h2{font-size:16px}.portrait{position:relative}.portrait img{display:block;width:100%;background:#090e16}.portrait svg{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}.foot{stroke:cyan;stroke-width:2}.collar{stroke:#ffd45c;stroke-width:2}label{display:inline-block;margin:8px 14px 8px 0}button{padding:10px;cursor:pointer}</style>
<header><h1>MIKA · Rig kapasitesi incelemesi</h1><p>Unity/Live2D tarafından üretilen tek parametre denemeleri. Her kare donmuş runtime Idle pozundan başlar; çekim boyunca fizik ve animasyon kapalıdır. Uç değerler tanılama içindir, oynanabilir animasyon değildir. Bir parametrenin adı, anatomik işlevinin kanıtı sayılmaz.</p>
<p>Yaka deformasyonu ve sarkık tutma pozu henüz onaylı değil. Sarı artı mevcut profilin yaka noktası, mavi artı ayak noktasıdır. Aşağıdaki yaka Y ayarı yalnızca bu raporun önizlemesini değiştirir; model veya uygulama ayarlarını kaydetmez.</p>
<label>Filtre <input id="filter" placeholder="walk, pose, motion1..."></label><label>Yaka Y <input id="anchor" type="number" min="0" max="1.2" step="0.005"></label><label><input type="checkbox" id="show" checked> Bağlantıları göster</label>
<p id="count"></p><p><a href="manifest.json">Kare ve SHA-256 listesi</a> · <a href="render-verification.json">Piksel ve geometri doğrulaması</a> · <a href="model-parameters.tsv">Parametre aralıkları</a> · <a href="animation-profile.json">Kullanılan profil</a></p></header><main>'''+''.join(cards)+'''</main><script>
const profile=PROFILE; const cards=[...document.querySelectorAll('article')];const anchor=document.querySelector('#anchor');anchor.value=profile.collar.y;
function draw(){const body=768/profile.textureHeight,bottom=(768+body)/2;cards.forEach((card,i)=>{const facing=PROBES[i].facing;[['foot',profile.foot.x,profile.foot.y],['collar',profile.collar.x,Number(anchor.value)]].forEach(([name,x,y])=>{x=256+x*body*facing;y=bottom-y*body;card.querySelector('.'+name).setAttribute('d',`M ${x-12} ${y} H ${x+12} M ${x} ${y-12} V ${y+12}`)});card.querySelector('svg').style.display=document.querySelector('#show').checked?'':'none'})}
document.querySelector('#filter').oninput=e=>{let n=0;cards.forEach(c=>{c.hidden=!c.dataset.label.toLowerCase().includes(e.target.value.toLowerCase());if(!c.hidden)n++});document.querySelector('#count').textContent=n+' / '+cards.length+' kare'};anchor.oninput=draw;document.querySelector('#show').onchange=draw;document.querySelector('#count').textContent=cards.length+' gerçek model karesi';draw();
</script></html>'''
page=page.replace('PROFILE',json.dumps(profile)).replace('PROBES',json.dumps(manifest['probes']))
(folder/'RIG_ASSESSMENT.md').write_text((root/'Docs/RIG_ASSESSMENT.md').read_text(encoding='utf-8'),encoding='utf-8')
(folder/'index.html').write_text(page,encoding='utf-8')
(folder/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
target=root/'Artifacts/Mika-Rig-Review.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED) as z:
    for file in sorted(folder.iterdir()):
        if file.is_file():z.write(file,'Mika-Rig-Review/'+file.name)
with zipfile.ZipFile(target) as z:assert z.testzip() is None
print(f'RIG_REPORT passed frames={len(cards)} archive={target}')
