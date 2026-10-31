"""Prepare rig-authoring inputs; never install them into the current runtime model."""
from pathlib import Path
import json, copy, zipfile

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / 'MikaUnity/Assets/StreamingAssets/Mika'
OUT = ROOT / 'Art/CollarRig'
IDS = ['ParamCollarPull', 'ParamHangBody', 'ParamHangAngle']

def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, ensure_ascii=False)+'\n', encoding='utf-8')

def motion(duration, loop, channels):
    curves=[]; segments=points=0
    for name, keys in channels.items():
        data=list(keys[0]); points+=1
        for (t0,v0),(t1,v1) in zip(keys,keys[1:]):
            # Restricted cubic Bezier: smooth zero-velocity endpoints, monotonic time.
            data.extend([1,t0+(t1-t0)/3,v0,t1-(t1-t0)/3,v1,t1,v1])
            segments+=1; points+=3
        curves.append({'Target':'Parameter','Id':name,'Segments':data})
    return {'Version':3,'Meta':{'Duration':duration,'Fps':30,'Loop':loop,'AreBeziersRestricted':True,
        'CurveCount':len(curves),'TotalSegmentCount':segments,'TotalPointCount':points,
        'UserDataCount':0,'TotalUserDataSize':0},'Curves':curves}

def main():
    grab={IDS[0]:[(0,0),(.06,.65),(.14,1),(.3,1)],
          IDS[1]:[(0,0),(.06,.1),(.2,.94),(.3,1)],
          IDS[2]:[(0,0),(.16,-.12),(.3,0)],
          'walk':[(0,0),(.3,-2)],'walk2':[(0,0),(.3,-1.7)],
          'walk9':[(0,0),(.3,0)],'walk8':[(0,0),(.3,0)],'walk10':[(0,0),(.3,0)]}
    idle={IDS[0]:[(0,1),(2,1)],IDS[1]:[(0,1),(2,1)],
          IDS[2]:[(0,0),(.5,.06),(1,0),(1.5,-.06),(2,0)],
          'walk':[(0,-2),(.5,-1.8),(1,-2),(1.5,-2.2),(2,-2)],
          'walk2':[(0,-1.7),(.5,-1.9),(1,-1.7),(1.5,-1.5),(2,-1.7)],
          'walk9':[(0,0),(.5,1.2),(1,0),(1.5,-1.2),(2,0)],
          'walk8':[(0,0),(.5,-1),(1,0),(1.5,1),(2,0)],'walk10':[(0,0),(2,0)]}
    release={IDS[0]:[(0,1),(.06,.25),(.12,0),(.18,0)],
             IDS[1]:[(0,1),(.06,.8),(.18,0)],IDS[2]:[(0,0),(.18,0)]}
    items={'collar grab.motion3.json':motion(.3,False,grab),
           'grabbed idle.motion3.json':motion(2,True,idle)}
    combined=json.loads((ART/'authored/release.motion3.json').read_text())
    extra=motion(.18,False,release)
    combined['Curves']+=extra['Curves']
    for key in ['CurveCount','TotalSegmentCount','TotalPointCount']:combined['Meta'][key]+=extra['Meta'][key]
    # Retain all held leg channels through release, including an interrupted idle cycle.
    for pid in ['walk9','walk8','walk10']:
        extra=motion(.18,False,{pid:[(0,0),(.18,0)]})
        combined['Curves']+=extra['Curves']
        for key in ['CurveCount','TotalSegmentCount','TotalPointCount']:combined['Meta'][key]+=extra['Meta'][key]
    items['release.motion3.json']=combined
    for name,data in items.items():write_json(OUT/'authored'/name,data)
    profile=json.loads((ART/'animation-profile.json').read_text())
    profile['rigReview']='pending';profile['grabSwayDraft']['enabled']=False
    profile['collarRig']={'enabled':True,'pull':IDS[0],'hang':IDS[1],'swing':IDS[2]}
    for binding in profile['bindings']:
        if binding['state'] not in ['Grabbed','Release']:continue
        names=['collar grab.motion3.json','grabbed idle.motion3.json'] if binding['state']=='Grabbed' else ['release.motion3.json']
        for stage,name in zip(binding['stages'],names):
            stage.update(motion='authored/'+name,contentPending=True,pose='',
                         ownedParameters=[c['Id'] for c in items[name]['Curves']])
    write_json(OUT/'animation-profile.candidate.json',profile)
    write_json(OUT/'keyframes.json',{'status':'requires new deformers; not a working rig',
                                   'grab':grab,'idle':idle,'release':release})
    write_json(OUT/'rig-contract.json',{'version':1,'status':'source model missing',
        'parameters':{IDS[0]:[0,0,1],IDS[1]:[0,0,1],IDS[2]:[-1,0,1]},
        'anchor':{'coordinateSystem':'visible neutral height from foot','x':0,'y':.78,'calibrated':False},
        'requiredSource':['editable .cmo3','layered collar/shoulder art if missing from source'],
        'runtimeModelModified':False})
    print('COLLAR_AUTHORING_INPUTS_READY: 3 candidate clips; candidate profile; no runtime model changes')

if __name__=='__main__':main()
