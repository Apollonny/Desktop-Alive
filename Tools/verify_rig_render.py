"""Compare complete RGBA pixels and native/Unity geometry; never infer missing parts from a preview."""
from pathlib import Path
import json,sys,hashlib
from PIL import Image
root=Path(__file__).resolve().parents[1]
folder=root/'Artifacts'/(sys.argv[1] if len(sys.argv)>1 else 'rig-audit')
manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8-sig'))
baseline=Image.open(folder/manifest['probes'][0]['file']).convert('RGBA')
reference=baseline.tobytes();rows=[]
for probe in manifest['probes']:
    path=folder/probe['file'];img=Image.open(path).convert('RGBA')
    assert img.size==baseline.size,(path,'size changed')
    geometry=json.loads(path.with_suffix('.geometry.json').read_text())['drawables']
    assert geometry
    max_error=max(r['vertexError'] for r in geometry)
    visibility_mismatches=sum(r['visible']!=r['enabled'] for r in geometry)
    assert max_error<=.0001,(path,'native/Unity vertex mismatch',max_error)
    assert visibility_mismatches==0,(path,'visibility mismatch')
    pixels=list(img.get_flattened_data());ref_pixels=baseline.get_flattened_data()
    solid=sum(p[3]>25 for p in pixels)
    assert solid>100,(path,'empty render')
    rows.append({'file':probe['file'],'parameter':probe['parameter'],'value':probe['value'],
                 'changedPixels':sum(a!=b for a,b in zip(pixels,ref_pixels)),
                 'opaquePixels':solid,'alphaBounds':img.getchannel('A').getbbox(),
                 'maxVertexError':max_error,'visibilityMismatches':visibility_mismatches,
                 'sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
assert Image.open(folder/manifest['probes'][-1]['file']).convert('RGBA').tobytes()==reference,'Neutral restoration differs'
result={'status':'passed','probes':len(rows),'neutralRestoration':'exact RGBA match',
        'identicalToNeutral':[r['file'] for r in rows if r['changedPixels']==0],
        'scope':'Native/Unity geometry agreement and exact pixel comparisons; not artistic acceptance or collar approval',
        'rows':rows}
(folder/'render-verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(f"RIG_RENDER_VERIFIED probes={len(rows)} neutralMatches={len(result['identicalToNeutral'])}; geometry, visibility, RGBA checks passed")
