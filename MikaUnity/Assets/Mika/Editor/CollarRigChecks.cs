using System;
using System.Collections.Generic;
using System.Linq;
using Mika.Core;
namespace Mika.Editor
{
    public static class CollarRigChecks
    {
        public static void Run()
        {
            int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("Collar rig: "+name);count++;};
            var p=new ModelAnimationProfile();var ranges=new Dictionary<string,RigParameterRange>();
            check(CollarRigContract.Validate(p,ranges).Count==0,"existing draft still loads");
            p.rigReview="approved";check(CollarRigContract.Validate(p,ranges).Count>0,"approval cannot bypass missing rig");p.rigReview="pending";
            p.collarRig.enabled=true;
            p.bindings=new[]{MikaState.Grabbed,MikaState.Release}.Select(s=>new AnimationBinding {state=s.ToString(),stages=new[]{new MotionStage {ownedParameters=p.collarRig.Parameters}}}).ToArray();
            check(CollarRigContract.Validate(p,ranges).Count==3,"all missing parameters reported");
            foreach(var id in p.collarRig.Parameters)ranges[id]=new RigParameterRange(id==p.collarRig.swing?-1:0,0,1);
            check(CollarRigContract.Validate(p,ranges).Count==0,"valid artist contract");
            ranges[p.collarRig.pull]=new RigParameterRange(0,0,.5f);
            check(CollarRigContract.Validate(p,ranges).Count>0,"insufficient range rejected");ranges[p.collarRig.pull]=new RigParameterRange(0,0,1);
            ranges[p.collarRig.hang]=new RigParameterRange(0,float.NaN,1);
            check(CollarRigContract.Validate(p,ranges).Count>0,"nonfinite neutral rejected");ranges[p.collarRig.hang]=new RigParameterRange(0,0,1);
            p.grabSwayDraft.enabled=true;check(CollarRigContract.Validate(p,ranges).Count>0,"draft cannot overwrite artist rig");p.grabSwayDraft.enabled=false;
            p.physicsParameters=new[]{p.collarRig.pull};check(CollarRigContract.Validate(p,ranges).Count>0,"physics ownership rejected");p.physicsParameters=new string[0];
            p.Binding(MikaState.Release).stages[0].ownedParameters=new string[0];check(CollarRigContract.Validate(p,ranges).Count==3,"release must retain all collar channels");
            p.collarRig.hang=p.collarRig.pull;check(!p.collarRig.Valid(),"duplicate mapping rejected");
            UnityEngine.Debug.Log("MIKA_COLLAR_RIG_CHECKS passed="+count);
        }
    }
}
