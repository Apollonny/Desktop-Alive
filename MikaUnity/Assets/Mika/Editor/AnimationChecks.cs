using System;
using System.Collections.Generic;
using System.Linq;
using Mika.Core;
using UnityEngine;

namespace Mika.Editor
{
    public static class AnimationChecks
    {
        public static void Run(ModelAnimationProfile profile)
        {
            int count=0;
            Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("ANIMATION FAILED: "+name);count++;};
            check(profile.StructuralErrors().Count==0,"Shipped profile structure");
            var sequence=new AnimationBinding {stages=new[]{new MotionStage{motion="grab",duration=.3f},new MotionStage{motion="hang",duration=2,loop=true}}};
            var sample=MotionTimeline.Evaluate(sequence,.3f,_=>2);
            check(sample.Stage==1&&Math.Abs(sample.ClipTime)<.0001f,"Second stage starts at zero, not state time");
            sample=MotionTimeline.Evaluate(sequence,2.8f,_=>2);
            check(Math.Abs(sample.ClipTime-.5f)<.0001f,"Loop uses time relative to its own start");
            var single=new AnimationBinding {stages=new[]{new MotionStage{motion="land",duration=.28f,sourceStart=.2f,sourceEnd=.7f}}};
            check(Math.Abs(MotionTimeline.Evaluate(single,.14f,_=>1).ClipTime-.45f)<.0001f,"Source range retiming");
            sample=MotionTimeline.Evaluate(single,5,_=>1);
            check(sample.Finished&&Math.Abs(sample.ClipTime-.7f)<.0001f,"One-shot holds final pose");
            var anchors=new ModelAnimationProfile {foot=new AnchorPoint{x=.1f,y=.05f},collar=new AnchorPoint{x=.2f,y=.85f}};
            var c=new MikaController(new BehaviorProfile(),3,anchors);c.Facing=-1;c.Grab();c.Drag(new Point(-900,200),640);
            check(Math.Abs(c.Feet.X+836)<.001f&&Math.Abs(c.Feet.Y-712)<.001f,"Mirrored, scaled non-zero anchors");
            var world=new DesktopSnapshot {Monitor=new Box(0,0,1920,1080),WorkArea=new Box(0,0,1920,1040)};
            world.Surfaces=SurfaceNavigator.Build(world,true);
            c=new MikaController(new BehaviorProfile(),3,profile);c.Rescue(world,320);c.Grab();c.Drag(new Point(800,300),320);
            var order=new List<MikaState>();c.Changed+=s=>order.Add(s);c.Release();
            for(int i=0;i<200;i++)c.Tick(.02f,world,new MikaSettings{randomBehaviors=false},false);
            check(order.SequenceEqual(new[]{MikaState.Release,MikaState.Fall,MikaState.Land,MikaState.Recover,MikaState.Idle}),"Release/fall/land/recover sequence");
            c.Grab();c.Release();c.Grab();check(c.State==MikaState.Grabbed,"Grab interrupts release");
            c.Release();c.Paused=true;var y=c.Feet.Y;
            for(int i=0;i<30;i++)c.Tick(.02f,world,new MikaSettings(),false);
            check(c.State==MikaState.Release&&c.Feet.Y==y,"Pause freezes animation phase clock");
            check(profile.Binding(MikaState.Fall).stages[0].loop&&!profile.Binding(MikaState.Land).stages[0].loop,"Fall loops but landing does not");
            check(profile.Binding(MikaState.Grabbed).stages.All(s=>s.contentPending)&&profile.rigReview=="pending","No counterfeit collar approval");
            check(!ModelAnimationProfile.SafeRelativePath("../escape.motion3.json")&&!ModelAnimationProfile.SafeRelativePath("C:/escape.json"),"Reject path traversal and absolute paths");
            var original=profile.Binding(MikaState.Release).stages[0].duration;
            profile.Binding(MikaState.Release).stages[0].duration=-1;
            check(profile.StructuralErrors().Count>0,"Reject negative duration");profile.Binding(MikaState.Release).stages[0].duration=original;
            original=profile.Binding(MikaState.Grabbed).stages[0].duration;
            profile.Binding(MikaState.Grabbed).stages[0].loop=true;
            check(profile.StructuralErrors().Count>0,"Reject unreachable stage after loop");profile.Binding(MikaState.Grabbed).stages[0].loop=false;
            Debug.Log("MIKA_ANIMATION_CHECKS passed="+count);
        }
    }
}
