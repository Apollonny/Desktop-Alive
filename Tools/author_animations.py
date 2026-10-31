"""Author motions against exported Mika parameters; never fabricate a collar rig.
Original motion/texture files are kept unchanged. Generated motions are owned here.
"""
from pathlib import Path
import json,sys
root=Path(__file__).resolve().parents[1]
art=root/'MikaUnity/Assets/StreamingAssets/Mika'
if (art/'animation-profile.json').exists() and '--force' not in sys.argv:
    raise SystemExit('Profile exists. To intentionally regenerate the authored motions and profile, pass --force. This overwrites manual changes.')
out=art/'authored';out.mkdir(exist_ok=True)
physics=json.loads((root/'165 218.physics3.json').read_text(encoding='utf-8'))
physics_ids=sorted({o['Destination']['Id'] for s in physics['PhysicsSettings'] for o in s['Output']})
def write_motion(name,duration,loop,keys):
    curves=[];segments=points=0
    for pid,keyframes in keys.items():
        values=[0,keyframes[0][1]]
        for (t0,v0),(t1,v1) in zip(keyframes,keyframes[1:]):
            step=(t1-t0)/3
            values.extend([1,round(t0+step,5),v0,round(t1-step,5),v1,t1,v1])
        curves.append({'Target':'Parameter','Id':pid,'Segments':values})
        segments+=len(keyframes)-1;points+=1+(len(keyframes)-1)*3
    data={'Version':3,'Meta':{'Duration':duration,'Fps':30,'Loop':loop,'AreBeziersRestricted':True,
          'CurveCount':len(curves),'TotalSegmentCount':segments,'TotalPointCount':points,'UserDataCount':0,'TotalUserDataSize':0},'Curves':curves}
    path=out/(name+'.motion3.json');path.write_text(json.dumps(data,indent=2))
    return 'authored/'+path.name
generated={}
generated['shock']=write_motion('shock',.22,False,{
    'ParamAngleY':[(0,0),(.09,12),(.22,7)],'ParamBodyAngleZ':[(0,0),(.1,-2),(.22,0)],
    'walk7':[(0,0),(.1,-3),(.22,-1)]})
generated['release']=write_motion('release',.18,False,{
    'ParamAngleY':[(0,2),(.18,7)],'ParamBodyAngleY':[(0,0),(.18,2)],
    'walk':[(0,0),(.18,-4)],'walk2':[(0,0),(.18,-3)]})
generated['fall']=write_motion('fall',.8,True,{
    'ParamAngleY':[(0,7),(.4,10),(.8,7)],'ParamBodyAngleY':[(0,2),(.4,3),(.8,2)],
    'ParamBodyAngleZ':[(0,0),(.2,-1.2),(.6,1.2),(.8,0)],
    'walk':[(0,-4),(.4,-5),(.8,-4)],'walk2':[(0,-3),(.4,-4),(.8,-3)]})
generated['land']=write_motion('land',.28,False,{
    'ParamAngleY':[(0,7),(.12,-9),(.28,-5)],'ParamBodyAngleY':[(0,2),(.12,-6),(.28,-4)],
    'walk':[(0,-4),(.12,-1),(.28,0)],'walk2':[(0,-3),(.12,-1),(.28,0)]})
generated['recover']=write_motion('recover',.56,False,{
    'ParamAngleY':[(0,-5),(.3,3),(.56,0)],'ParamBodyAngleY':[(0,-4),(.3,2),(.56,0)],
    'walk':[(0,0),(.56,0)],'walk2':[(0,0),(.56,0)]})
generated['react']=write_motion('react',.6,False,{
    'ParamAngleY':[(0,0),(.18,-5),(.38,2),(.6,0)],'ParamAngleZ':[(0,0),(.25,3),(.6,0)]})
def stage(path,duration,loop=False,blend=.12,pose='',end=-1,pending=False):
    motion=json.loads((art/path).read_text()) if (art/path).exists() else {'Curves':[]}
    owned={c['Id'] for c in motion['Curves'] if c['Target']=='Parameter'}
    if pose:
        owned.update(p['Id'] for p in json.loads((art/pose).read_text())['Parameters'])
    # Body Y and hip motion are intentionally motion-owned; augmented body/hair outputs stay with Cubism.
    owned=sorted(p for p in owned if p not in physics_ids or p in ('ParamBodyAngleY','walk10'))
    return dict(motion=path,pose=pose,duration=duration,loop=loop,blend=blend,sourceStart=0,sourceEnd=end,contentPending=pending,ownedParameters=owned)
bindings=[]
def bind(name,stages,look=False):bindings.append(dict(state=name,look=look,blink=True,breath=True,stages=stages))
bind('Idle',[stage('motion/idle.motion3.json',1,True,pose='exp/idle.exp3.json')],True)
for name in ('Walk','WindowWalk'):bind(name,[stage('motion/walk 2b.motion3.json',8.9,True,pose='exp/walk 2b.exp3.json')])
bind('Jump',[stage('motion/normal jump.motion3.json',.8,end=.7,pose='exp/normal jump.exp3.json')])
bind('Dance',[stage('motion/02 dance 2b.motion3.json',15.967,pose='exp/02 dance 2b.exp3.json')])
bind('React',[stage(generated['react'],.6)],True)
bind('Grabbed',[stage('authored/collar grab.motion3.json',.3,pending=True),stage('authored/grabbed idle.motion3.json',2,True,pending=True)])
for name,duration,loop in [('Release',.18,False),('Shock',.22,False),('Fall',.8,True),('Land',.28,False),('Recover',.56,False)]:
    bind(name,[stage(generated[name.lower()],duration,loop,blend=.06 if name=='Land' else .1)])
profile=dict(version=1,model='165 218.model3.json',foot=dict(x=0,y=0),collar=dict(x=0,y=.78),textureHeight=1.1,
    expressionFade=.15,expressionParameters=[f'exp{i}' for i in range(1,11)],physicsParameters=physics_ids,rigReview='pending',bindings=bindings)
(art/'animation-profile.json').write_text(json.dumps(profile,indent=2))
print('Authored 6 motion clips and animation profile; collar deformation remains pending.')
