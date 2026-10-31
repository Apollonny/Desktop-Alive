"""Validate candidate curves independently of the unavailable artist rig."""
import json, math
from pathlib import Path

root=Path(__file__).resolve().parents[1]
folder=root/'Art/CollarRig'
profile=json.loads((folder/'animation-profile.candidate.json').read_text(encoding='utf-8'))
contract=json.loads((folder/'rig-contract.json').read_text(encoding='utf-8'))
assert profile['rigReview']=='pending' and profile['collarRig']['enabled']
assert not profile['grabSwayDraft']['enabled']
ranges={k:(v[0],v[2]) for k,v in contract['parameters'].items()}
ranges.update({'walk':(-3,0),'walk2':(-3,0),'walk9':(-2,2),'walk8':(-2,2),'walk10':(-.5,.5),
               'ParamAngleY':(-30,30),'ParamBodyAngleY':(-10,10)})
# Release retains the current model's walk values, which reach -4 / -3.
ranges['walk']=(-10,10);ranges['walk2']=(-10,10)
endpoints={}
for binding in profile['bindings']:
    if binding['state'] not in ('Grabbed','Release'):continue
    for stage in binding['stages']:
        assert stage['contentPending']
        motion=json.loads((folder/stage['motion']).read_text(encoding='utf-8'))
        curves=motion['Curves'];meta=motion['Meta'];points=segments=0;ends={}
        assert len(curves)==meta['CurveCount'] and meta['Duration']==stage['duration']
        assert meta['Loop']==stage['loop']
        assert set(stage['ownedParameters'])=={c['Id'] for c in curves}
        assert set(contract['parameters'])<={c['Id'] for c in curves}
        for curve in curves:
            data=curve['Segments'];lo,hi=ranges[curve['Id']];i=2;points+=1;t=data[0]
            assert t==0 and lo<=data[1]<=hi
            while i<len(data):
                kind=data[i];assert kind in (0,1,2,3)
                n=6 if kind==1 else 2;chunk=data[i+1:i+n+1];assert len(chunk)==n
                times=chunk[::2];values=chunk[1::2]
                assert all(math.isfinite(v) for v in chunk)
                assert times==sorted(times) and times[0]>=t and all(lo<=v<=hi for v in values)
                t=times[-1];i+=n+1;segments+=1;points+=n//2
            assert abs(t-meta['Duration'])<1e-6
            ends[curve['Id']]=(data[1],data[-1])
            if meta['Loop']:assert abs(data[1]-data[-1])<1e-6
        assert segments==meta['TotalSegmentCount'] and points==meta['TotalPointCount']
        endpoints[stage['motion']]=ends
for pid in contract['parameters']:
    grab=endpoints['authored/collar grab.motion3.json'][pid]
    idle=endpoints['authored/grabbed idle.motion3.json'][pid]
    release=endpoints['authored/release.motion3.json'][pid]
    assert grab[0]==0 and grab[1]==idle[0]==idle[1]==release[0] and release[1]==0
print('COLLAR_DESIGN_VALID: 3 candidate clips; curve metadata/ranges, loop and phase endpoints; rig still pending')
