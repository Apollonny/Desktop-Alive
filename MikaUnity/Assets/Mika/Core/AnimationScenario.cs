using System;

namespace Mika.Core
{
    // A deterministic miniature desktop using the production controller, with no native windows.
    public sealed class AnimationScenario
    {
        public readonly DesktopSnapshot World=new DesktopSnapshot {Monitor=new Box(0,0,800,700),WorkArea=new Box(0,0,800,660)};
        public readonly MikaSettings Settings=new MikaSettings {characterHeight=240,randomBehaviors=false,musicReactions=false};
        public readonly MikaController Controller;
        public AnimationScenario(ModelAnimationProfile profile)
        {
            World.Windows.Add(new DesktopWindow {Id=1,Bounds=new Box(180,460,620,640)});
            World.Surfaces=SurfaceNavigator.Build(World,true);
            Controller=new MikaController(new BehaviorProfile(),1234,profile);
            Controller.Rescue(World,Settings.characterHeight,.5f);
        }
        public void Grab(float feetY=330)
        {
            Controller.Grab();
            var offset=Controller.Animation.GrabOffset(Settings.characterHeight,Controller.Facing);
            Controller.Drag(new Point(400+offset.X,feetY-offset.Y),Settings.characterHeight);
        }
        public void Drop() {Grab();Controller.Release();}
        public void RemoveWindow()
        {World.Windows.Clear();World.Surfaces=SurfaceNavigator.Build(World,false);}
        public void Tick(float dt) {Controller.Tick(dt,World,Settings,false);}
    }
}
