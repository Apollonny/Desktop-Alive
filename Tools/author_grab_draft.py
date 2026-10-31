"""Add a reversible development draft without changing the original model or approving the collar rig."""
from pathlib import Path
import json,math
root=Path(__file__).resolve().parents[1];art=root/'MikaUnity/Assets/StreamingAssets/Mika'
profile_path=art/'animation-profile.json';profile=json.loads(profile_path.read_text())
assert profile['rigReview']=='pending','Do not replace an approved artist rig'
path='authored/grabbed-leg-swing-draft.motion3.json'
assert not (art/path).exists(),'Draft exists; edit its source and profile explicitly rather than overwriting it'
curves=[]
spec={'walk':(-2,.35,0),'walk2':(-1.7,.3,1.1),'walk9':(0,1.2,0),'walk8':(0,1,1.1),'walk10':(0,.15,.5)}
for pid,(base,amplitude,phase) in spec.items():
    frames=[(i/8,base+amplitude*math.sin(math.pi*i/8+phase)) for i in range(17)]
    frames[-1]=(2,frames[0][1]);segments=[0,frames[0][1]]
    for (t0,v0),(t1,v1) in zip(frames,frames[1:]):
        segments.extend([1,t0+(t1-t0)/3,v0,t1-(t1-t0)/3,v1,t1,v1])
    curves.append({'Target':'Parameter','Id':pid,'Segments':segments})
motion={'Version':3,'Meta':{'Duration':2,'Fps':30,'Loop':True,'AreBeziersRestricted':True,'CurveCount':5,
    'TotalSegmentCount':80,'TotalPointCount':245,'UserDataCount':0,'TotalUserDataSize':0},'Curves':curves}
(art/path).write_text(json.dumps(motion,indent=2),encoding='utf-8')
profile['grabSwayDraft']={'enabled':True,'frequency':4.5,'damping':1.6,'initialVelocity':1.8,
    'speedLimit':3,'inputGain':.18,'angle':2,'lift':.6,'hip':.35}
grab=next(b for b in profile['bindings'] if b['state']=='Grabbed')
grab['stages'][-1].update(motion=path,ownedParameters=list(spec),contentPending=True)
profile_path.write_text(json.dumps(profile,indent=2),encoding='utf-8')
print('GRAB_DRAFT_AUTHORED looping reference + damped runtime profile; rigReview=pending')
