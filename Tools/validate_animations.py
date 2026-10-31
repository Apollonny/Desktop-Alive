"""Validate profile paths, authored curve metadata and parameter ranges from the loaded model."""
from pathlib import Path
import json,os,math
root=Path(__file__).resolve().parents[1];art=root/'MikaUnity/Assets/StreamingAssets/Mika'
p=json.loads((art/'animation-profile.json').read_text())
ranges={}
table=Path(os.environ['USERPROFILE'])/'AppData/LocalLow/Mika/MIKA LIVES HERE/model-parameters.tsv'
for line in table.read_text().splitlines():
    key,lo,default,hi=line.split('\t');ranges[key]=(float(lo.replace(',','.')),float(hi.replace(',','.')))
checked=[];pending=[]
for binding in p['bindings']:
    for stage in binding['stages']:
        file=(art/stage['motion']).resolve();assert file.is_relative_to(art.resolve())
        if not file.exists():assert stage['contentPending'];pending.append(stage['motion']);continue
        motion=json.loads(file.read_text());duration=motion['Meta']['Duration']
        assert stage['sourceEnd']==-1 or stage['sourceEnd']<=duration+.001
        actual_segments=actual_points=0
        for curve in motion['Curves']:
            data=curve['Segments'];assert len(data)>=2
            assert curve['Id'] in ranges,curve['Id']
            lo,hi=ranges[curve['Id']];t=data[0];values=[data[1]];i=2;actual_points+=1
            while i<len(data):
                kind=data[i];length=6 if kind==1 else 2;assert kind in (0,1,2,3)
                assert i+length<len(data)
                times=data[i+1:i+length+1:2];values.extend(data[i+2:i+length+1:2]);assert all(math.isfinite(x) for x in times)
                assert times[0]>=t-.001 and times==sorted(times)
                t=times[-1];actual_segments+=1;actual_points+=length//2;i+=length+1
            assert t<=duration+.001
            # New motions must stay inside exported parameter ranges. Original source curves remain unmodified.
            if stage['motion'].startswith('authored/'):
                assert all(math.isfinite(v) and lo-.001<=v<=hi+.001 for v in values),(curve['Id'],lo,hi,values)
        assert actual_segments==motion['Meta']['TotalSegmentCount'],stage['motion']
        assert actual_points==motion['Meta']['TotalPointCount'],stage['motion']
        assert len(motion['Curves'])==motion['Meta']['CurveCount']
        checked.append(stage['motion'])
report={'validMotions':sorted(set(checked)),'pending':sorted(set(pending)),'rangesCheckedFrom':str(table),'rigReview':p['rigReview']}
(root/'Artifacts/animation-asset-audit.json').write_text(json.dumps(report,indent=2))
print(f'ANIMATION_ASSETS passed={len(set(checked))}; pending={len(set(pending))}; authored ranges and metadata valid.')
