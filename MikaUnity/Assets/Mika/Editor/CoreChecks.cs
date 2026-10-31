using System;
using System.Linq;
using Mika.Core;
using UnityEngine;
namespace Mika.Editor
{
    public static class CoreChecks
    {
        static int checks;
        static void Check(bool valid,string name) {if(!valid)throw new Exception("FAILED: "+name);checks++;}
        static DesktopSnapshot World()
        {
            var w=new DesktopSnapshot {Monitor=new Box(-1920,0,0,1080),WorkArea=new Box(-1920,0,0,1040),DpiScale=1};
            w.Surfaces=SurfaceNavigator.Build(w,true);return w;
        }
        public static void Run()
        {
            checks=0;var w=World();
            w.Windows.Add(new DesktopWindow {Id=1,Bounds=new Box(-1500,100,-1000,600)});
            w.Windows.Add(new DesktopWindow {Id=2,Bounds=new Box(-1800,300,-500,900)});
            w.Surfaces=SurfaceNavigator.Build(w,true);
            var spans=w.Surfaces.Where(s=>s.Owner==2).ToArray();
            Check(spans.Length==2,"Occluded top edge is split");
            Check(!spans.Any(s=>s.Supports(-1200,10)),"Occluded landing rejected");
            Check(SurfaceNavigator.Landing(w.Surfaces,new Point(-1700,200),new Point(-1700,400),40)?.Owner==2,"First crossed platform catches fall");
            Check(SurfaceNavigator.Landing(w.Surfaces,new Point(-1800,200),new Point(-1800,400),40)==null,"Narrow edge rejects feet");
            Check(SurfaceNavigator.Reachable(w.Surfaces,w.Surfaces[0],new Point(-100,1040),320,50).Count==0,"Unreachable high window rejected");
            var s=new MikaSettings();var c=new MikaController(new BehaviorProfile());c.Rescue(w,320);
            c.Grab();c.Drag(new Point(-1700,200),320);c.Release();
            Check(c.State==MikaState.Release,"Release has a dedicated animation phase");
            for(int i=0;i<200;i++)c.Tick(.02f,w,s,false);
            Check(c.Support!=null,"Falling character finds support");
            c.Grab();c.Tick(9,w,s,true); // dt is bounded, use real frame sequence.
            for(int i=0;i<450;i++)c.Tick(.02f,w,s,true);
            Check(c.State==MikaState.Grabbed&&c.Expression=="exp5","Grab preempts media and grows angry");
            c.Release();c.Paused=true;var before=c.Feet;c.Tick(.05f,w,s,false);Check(c.Feet.Y==before.Y,"Pause freezes physics");
            var p=new MediaPolicy();var f=new MediaFrame {sessions=new[]{new MediaSession{id="Spotify.exe",playing=true}}};
            Check(!p.Tick(1,f,true,s),"Media debounce");Check(p.Tick(1.1f,f,true,s),"Allowed source activates after debounce");
            Check(!p.Tick(.1f,f,false,s),"Helper disconnect clears music");
            Check(!MediaPolicy.Matches("NotSpotify.exe","Spotify"),"No substring source matching");
            Check(MediaPolicy.Matches("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify","Spotify"),"Packaged Spotify recognized");
            Check(!MediaPolicy.Matches("chrome.exe","Spotify"),"Browser disabled by default");
            var clicks=new MikaController(new BehaviorProfile());clicks.Rescue(w,320);
            for(int i=0;i<4;i++)clicks.Click();Check(clicks.Expression=="exp4","Four clicks pout");
            for(int i=0;i<3;i++)clicks.Click();Check(clicks.Expression=="exp5","Seven clicks angry");
            // Attach by dropping onto a controlled window, then track translation and loss.
            var tracked=World();tracked.Windows.Add(new DesktopWindow {Id=8,Bounds=new Box(-1700,500,-700,900)});
            tracked.Surfaces=SurfaceNavigator.Build(tracked,true);
            var follower=new MikaController(new BehaviorProfile());follower.Rescue(tracked,320);
            follower.Grab();follower.Drag(new Point(-1200,200),320);follower.Release();
            for(int i=0;i<90;i++)follower.Tick(.02f,tracked,s,false);
            Check(follower.Support?.Owner==8,"Drop attaches to window");
            float originalX=follower.Feet.X;
            tracked.Windows[0].Bounds=new Box(-1600,550,-600,950);tracked.Surfaces=SurfaceNavigator.Build(tracked,true);
            follower.Tick(.02f,tracked,s,false);
            Check(Math.Abs(follower.Feet.X-originalX-100)<.1f && follower.Feet.Y==550,"Window local anchor follows translation");
            tracked.Windows.Insert(0,new DesktopWindow {Id=9,Bounds=new Box(-1600,400,-600,800),Supports=false});
            tracked.Surfaces=SurfaceNavigator.Build(tracked,true);follower.Tick(.02f,tracked,s,false);
            Check(follower.State==MikaState.Shock,"Occluded support reacts before falling instead of teleporting");
            tracked.Windows.Clear();tracked.Surfaces=SurfaceNavigator.Build(tracked,true);
            for(int i=0;i<100;i++)follower.Tick(.02f,tracked,s,false);
            Check(follower.Support?.Owner==-1,"Closed window safely returns to desktop");
            var scale=World();scale.DpiScale=2;var scaled=new MikaController(new BehaviorProfile());scaled.Rescue(scale,640);
            Check(scaled.Feet.X<0&&scaled.Feet.Y==1040,"Negative-origin monitor rescue remains valid at 200 percent DPI");
            var high=World();high.Windows.Add(new DesktopWindow {Id=12,Bounds=new Box(-1600,10,-600,800)});high.Surfaces=SurfaceNavigator.Build(high,true);
            var safe=new MikaController(new BehaviorProfile());safe.Rescue(high,320);safe.Grab();safe.Drag(new Point(-1000,-300),320);safe.Release();
            for(int i=0;i<160;i++)safe.Tick(.02f,high,new MikaSettings {randomBehaviors=false},false);
            Check(safe.Support?.Owner==-1,"Insufficient headroom falls to desktop instead of disappearing above monitor");
            Debug.Log("MIKA_CORE_CHECKS passed="+checks);
        }
    }
}
