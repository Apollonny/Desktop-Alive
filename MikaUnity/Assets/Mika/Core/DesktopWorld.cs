using System;
using System.Collections.Generic;
using System.Linq;

namespace Mika.Core
{
    public struct Point
    {
        public float X, Y;
        public Point(float x, float y) { X = x; Y = y; }
    }
    public struct Box
    {
        public float Left, Top, Right, Bottom;
        public float Width => Right - Left;
        public float Height => Bottom - Top;
        public Box(float l, float t, float r, float b) { Left=l; Top=t; Right=r; Bottom=b; }
        public bool Contains(Point p) => p.X>=Left && p.X<Right && p.Y>=Top && p.Y<Bottom;
    }
    public sealed class DesktopWindow
    {
        public long Id;
        public Box Bounds;
        public bool Supports = true;
    }
    public sealed class Surface
    {
        public long Owner;
        public float Left, Right, Y, OwnerLeft, OwnerRight;
        public bool IsWindow => Owner > 0;
        public bool Supports(float x, float margin) => x >= Left+margin && x <= Right-margin;
    }
    public sealed class DesktopSnapshot
    {
        public Box Monitor, WorkArea;
        public float DpiScale = 1;
        public bool Fullscreen;
        public List<DesktopWindow> Windows = new List<DesktopWindow>();
        public List<Surface> Surfaces = new List<Surface>();
    }
    public interface IDesktopEnvironment { DesktopSnapshot Sample(bool windows, bool taskbar); }
    public static class SurfaceNavigator
    {
        // Input windows are in front-to-back Z order. Occluders need not be walkable.
        public static List<Surface> Build(DesktopSnapshot snapshot, bool includeWindows)
        {
            var result = new List<Surface> { new Surface { Owner=-1, Left=snapshot.WorkArea.Left,
                Right=snapshot.WorkArea.Right, Y=snapshot.WorkArea.Bottom, OwnerLeft=snapshot.WorkArea.Left,OwnerRight=snapshot.WorkArea.Right } };
            if (!includeWindows) return result;
            for (int i=0; i<snapshot.Windows.Count; ++i)
            {
                var w = snapshot.Windows[i];
                if (!w.Supports || w.Bounds.Top < snapshot.Monitor.Top || w.Bounds.Top >= snapshot.WorkArea.Bottom) continue;
                var spans = new List<Point> { new Point(Math.Max(w.Bounds.Left,snapshot.WorkArea.Left), Math.Min(w.Bounds.Right,snapshot.WorkArea.Right)) };
                for (int j=0; j<i; ++j)
                {
                    var front = snapshot.Windows[j].Bounds;
                    if (front.Top > w.Bounds.Top || front.Bottom <= w.Bounds.Top) continue;
                    var next = new List<Point>();
                    foreach (var span in spans)
                    {
                        if (front.Right <= span.X || front.Left >= span.Y) { next.Add(span); continue; }
                        if (front.Left > span.X) next.Add(new Point(span.X,front.Left));
                        if (front.Right < span.Y) next.Add(new Point(front.Right,span.Y));
                    }
                    spans=next;
                }
                foreach(var span in spans) if(span.Y>span.X)
                    result.Add(new Surface { Owner=w.Id, Left=span.X, Right=span.Y, Y=w.Bounds.Top, OwnerLeft=w.Bounds.Left,OwnerRight=w.Bounds.Right });
            }
            return result;
        }
        public static Surface Landing(IEnumerable<Surface> surfaces, Point previous, Point current, float margin)
            => surfaces.Where(s => s.Supports(current.X,margin) && s.Y>=previous.Y-.5f && s.Y<=current.Y+.5f).OrderBy(s=>s.Y).FirstOrDefault();
        public static List<Surface> Reachable(IEnumerable<Surface> surfaces, Surface from, Point feet, float height, float margin)
            => surfaces.Where(s=>s.Owner!=from.Owner && s.Right-s.Left>=2*margin && from.Y-s.Y<=height*.5f
                && s.Y-from.Y<=height && Distance(feet.X,s.Left+margin,s.Right-margin)<=height).ToList();
        public static float Clamp(float v,float min,float max) => Math.Max(min,Math.Min(max,v));
        static float Distance(float x,float l,float r) => x<l ? l-x : x>r ? x-r : 0;
    }
    public enum MikaState { Idle, Walk, WindowWalk, Jump, React, Grabbed, Release, Shock, Fall, Land, Recover, Dance }
    [Serializable] public sealed class BehaviorProfile
    {
        public float decisionMin=15, decisionMax=40, walkSpeed=65, gravity=1500;
        public float danceCooldown=90, danceDuration=15.967f, poutAfter=3, angryAfter=8;
        public float curiosity=.65f, energy=.55f, dragThreshold=6;
    }
    [Serializable] public sealed class MikaSettings
    {
        public int fps=30, characterHeight=320;
        public string monitor="";
        public bool mouseTracking=true, musicReactions=true, randomBehaviors=true, windowInteraction=true,
            taskbarInteraction=true, startWithWindows=false, hideInFullscreen=true;
        public float volume=0;
        public float homeX=.85f;
        public string[] mediaSources=new[] { "Spotify" };
        public bool remindersEnabled=true;
        public List<MikaReminder> reminders=new List<MikaReminder>();
    }
    public interface IMikaAnimationDriver { void SetState(MikaState state); void SetExpression(string expression); }
    public sealed class MikaController
    {
        public MikaState State { get; private set; } = MikaState.Idle;
        public Point Feet;
        public Surface Support { get; private set; }
        public string Expression { get; private set; }="";
        public bool Paused;
        public float Facing=1;
        public float LandingSpeed {get;private set;}
        public float LandingStrength => Animation.landingResponse.Strength(LandingSpeed);
        public readonly BehaviorProfile Profile;
        public readonly ModelAnimationProfile Animation;
        readonly Random random;
        readonly Queue<float> clicks=new Queue<float>();
        float time, entered, decisionAt, velocity, walkTarget, localX, lastDance=-1000, expressionUntil;
        Point jumpStart, jumpEnd;
        Surface jumpSurface;
        int previousDecision=-1;
        public event Action<MikaState> Changed;
        public MikaController(BehaviorProfile profile, int seed=1234,ModelAnimationProfile animation=null) { Profile=profile;Animation=animation??new ModelAnimationProfile();random=new Random(seed); Schedule(); }
        public void Rescue(DesktopSnapshot world,float height,float? homeX=null)
        {
            var ground=world.Surfaces.First(s=>s.Owner==-1);
            float x=homeX.HasValue?ground.Left+SurfaceNavigator.Clamp(homeX.Value,0,1)*(ground.Right-ground.Left):world.WorkArea.Right-height*.7f;
            Feet=new Point(SurfaceNavigator.Clamp(x,ground.Left+height*.2f,ground.Right-height*.2f),ground.Y);
            LandingSpeed=0; Attach(ground); Change(MikaState.Idle);
        }
        public void Click()
        {
            while(clicks.Count>0 && time-clicks.Peek()>3) clicks.Dequeue();
            clicks.Enqueue(time);
            Expression=clicks.Count>=7?"exp5":clicks.Count>=4?"exp4":"exp2";
            expressionUntil=time+2;
            if(State!=MikaState.Grabbed && State!=MikaState.Release && State!=MikaState.Shock && State!=MikaState.Fall && State!=MikaState.Jump && State!=MikaState.Land && State!=MikaState.Recover) Change(MikaState.React);
        }
        public void Grab() { LandingSpeed=0; Support=null; jumpSurface=null; Expression="exp6"; Change(MikaState.Grabbed); }
        public void Drag(Point cursor,float height) { if(State==MikaState.Grabbed) {var offset=Animation.GrabOffset(height,Facing);Feet=new Point(cursor.X-offset.X,cursor.Y+offset.Y);} }
        public void Release() { if(State==MikaState.Grabbed) { velocity=0; Expression="exp6"; expressionUntil=time+.8f; Change(MikaState.Release); } }
        void LoseSupport() {Support=null;velocity=0;Expression="exp6";expressionUntil=time+.9f;Change(MikaState.Shock);}
        public void Tick(float dt,DesktopSnapshot world,MikaSettings settings,bool music)
        {
            if(Paused) return;
            dt=Math.Min(dt,.05f); time+=dt;
            float height=settings.characterHeight*world.DpiScale, margin=height*.17f;
            // A character cannot stand above a maximized/top-edge window without leaving the monitor.
            var surfaces=world.Surfaces.Where(s=>!s.IsWindow||s.Y>=world.Monitor.Top+height).ToList();
            if(State==MikaState.Grabbed) { Expression=time-entered>=Profile.angryAfter?"exp5":time-entered>=Profile.poutAfter?"exp4":"exp6"; return; }
            if(time>expressionUntil) Expression="";
            if(Support!=null)
            {
                var choices=surfaces.Where(s=>s.Owner==Support.Owner && s.Right-s.Left>=margin*2).ToList();
                float proposed=choices.Count>0?choices[0].OwnerLeft+localX:Feet.X;
                var next=choices.FirstOrDefault(s=>s.Supports(proposed,margin));
                // Resize may clamp to a remaining visible segment; complete occlusion drops the character.
                if(next==null && choices.Count>0 && Math.Abs((Support.OwnerRight-Support.OwnerLeft)-(choices[0].OwnerRight-choices[0].OwnerLeft))>.5f)
                    next=choices.OrderBy(s=>Math.Abs((s.Left+s.Right)*.5f-proposed)).First();
                if(next==null) LoseSupport();
                else { Feet=new Point(SurfaceNavigator.Clamp(proposed,next.Left+margin,next.Right-margin),next.Y); Attach(next); }
            }
            if(State==MikaState.Jump)
            {
                var target=surfaces.FirstOrDefault(s=>s.Owner==jumpSurface.Owner && s.Supports(jumpEnd.X,margin) && Math.Abs(s.Y-jumpEnd.Y)<1 && Math.Abs(s.OwnerLeft-jumpSurface.OwnerLeft)<1);
                if(target==null) LoseSupport();
                else
                {
                    float u=Math.Min(1,(time-entered)/Animation.Duration(MikaState.Jump,.8f));
                    Feet=new Point(jumpStart.X+(jumpEnd.X-jumpStart.X)*u,jumpStart.Y+(jumpEnd.Y-jumpStart.Y)*u-height*.6f*4*u*(1-u));
                    if(u>=1) { LandingSpeed=Math.Max(0,(jumpEnd.Y-jumpStart.Y+height*2.4f)/Animation.Duration(MikaState.Jump,.8f)/world.DpiScale); Attach(target); Change(MikaState.Land); }
                    return;
                }
            }
            if(State==MikaState.Release||State==MikaState.Shock)
            {
                if(time-entered>=Animation.Duration(State,State==MikaState.Release?.18f:.22f))Change(MikaState.Fall);
                return;
            }
            if(State==MikaState.Fall)
            {
                Point prev=Feet;
                float acceleration=Profile.gravity*world.DpiScale,previousVelocity=velocity;
                Feet=new Point(SurfaceNavigator.Clamp(Feet.X,world.WorkArea.Left+margin,world.WorkArea.Right-margin),Feet.Y+velocity*dt+.5f*acceleration*dt*dt);
                velocity+=acceleration*dt;
                var landing=SurfaceNavigator.Landing(surfaces,prev,Feet,margin);
                if(landing==null && Feet.Y>=world.WorkArea.Bottom) landing=world.Surfaces.First(s=>s.Owner==-1);
                if(landing!=null)
                {
                    // Contact speed at the surface, not the overshot end of the frame.
                    LandingSpeed=(float)Math.Sqrt(previousVelocity*previousVelocity+2*acceleration*Math.Max(0,landing.Y-prev.Y))/world.DpiScale;
                    Feet.Y=landing.Y; Attach(landing); Change(MikaState.Land);
                }
                return;
            }
            if(State==MikaState.Land) { if(time-entered>=Animation.Duration(State,.28f)) Change(MikaState.Recover); return; }
            if(State==MikaState.Recover || State==MikaState.React) { if(time-entered>=Animation.Duration(State,.6f)) Change(MikaState.Idle); return; }
            if(State==MikaState.Dance) { if(!music || time-entered>=Animation.Duration(State,Profile.danceDuration)) Change(MikaState.Idle); return; }
            if(State==MikaState.Walk || State==MikaState.WindowWalk)
            {
                if(!settings.randomBehaviors) { Change(MikaState.Idle); return; }
                walkTarget=SurfaceNavigator.Clamp(walkTarget,Support.Left+margin,Support.Right-margin);
                float difference=walkTarget-Feet.X, step=Profile.walkSpeed*world.DpiScale*dt;
                Facing=difference>=0?1:-1;
                Feet.X+=Math.Sign(difference)*Math.Min(Math.Abs(difference),step); localX=Feet.X-Support.OwnerLeft;
                if(Math.Abs(difference)<step+1) Change(MikaState.Idle);
                return;
            }
            if(Support==null) { velocity=0; Change(MikaState.Fall); return; }
            if(music && settings.musicReactions && time-lastDance>=Profile.danceCooldown) { lastDance=time; Change(MikaState.Dance); return; }
            if(!settings.randomBehaviors || time<decisionAt) return;
            Schedule();
            int decision=random.NextDouble()<Profile.curiosity?2:random.NextDouble()<Profile.energy?1:0;
            if(decision==previousDecision) decision=(decision+1)%3;
            previousDecision=decision;
            if(decision==2)
            {
                var targets=SurfaceNavigator.Reachable(surfaces,Support,Feet,height,margin);
                if(targets.Count>0)
                {
                    jumpSurface=targets[random.Next(targets.Count)]; jumpStart=Feet;
                    jumpEnd=new Point(SurfaceNavigator.Clamp(Feet.X,jumpSurface.Left+margin,jumpSurface.Right-margin),jumpSurface.Y);
                    Facing=jumpEnd.X>=Feet.X?1:-1; Support=null; Change(MikaState.Jump); return;
                }
                decision=1;
            }
            if(decision==1) { walkTarget=Support.Left+margin+(float)random.NextDouble()*Math.Max(0,Support.Right-Support.Left-2*margin); Change(Support.IsWindow?MikaState.WindowWalk:MikaState.Walk); }
        }
        void Attach(Surface s) { Support=s; localX=Feet.X-s.OwnerLeft; }
        void Schedule() { decisionAt=time+Profile.decisionMin+(float)random.NextDouble()*(Profile.decisionMax-Profile.decisionMin); }
        void Change(MikaState next) { if(State==next)return; State=next; entered=time; Changed?.Invoke(next); }
    }
}
