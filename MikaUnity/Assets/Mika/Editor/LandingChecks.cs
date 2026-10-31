using System;
using Mika.Core;
namespace Mika.Editor
{
    public static class LandingChecks
    {
        static void Require(bool value,string name){if(!value)throw new Exception("Landing: "+name);}
        static MikaController Drop(float distance,float dpi,float dt)
        {
            var world=new DesktopSnapshot {Monitor=new Box(0,0,800*dpi,700*dpi),WorkArea=new Box(0,0,800*dpi,660*dpi),DpiScale=dpi};
            world.Surfaces=SurfaceNavigator.Build(world,false);
            var controller=new MikaController(new BehaviorProfile());
            var settings=new MikaSettings {characterHeight=240,randomBehaviors=false};
            controller.Grab();controller.Feet=new Point(400*dpi,(660-distance)*dpi);controller.Release();
            for(int i=0;i<1000&&controller.State!=MikaState.Land;i++)controller.Tick(dt,world,settings,false);
            Require(controller.State==MikaState.Land,"drop reaches land");
            float speed=controller.LandingSpeed;
            controller.Paused=true;controller.Tick(1,world,settings,false);
            Require(controller.State==MikaState.Land&&speed==controller.LandingSpeed,"paused contact is stable");
            controller.Paused=false;
            for(int i=0;i<100&&controller.State!=MikaState.Recover;i++)controller.Tick(dt,world,settings,false);
            Require(controller.State==MikaState.Recover&&speed==controller.LandingSpeed,"recover retains contact");
            return controller;
        }
        public static void Run()
        {
            var soft=Drop(20,1,1/30f);var hard=Drop(360,1,1/30f);
            Require(hard.LandingStrength>soft.LandingStrength+.5f,"short/high contrast");
            foreach(float distance in new[]{0f,20f,130f,360f})
                foreach(float dpi in new[]{1f,1.25f,1.5f,2f})
                    foreach(float dt in new[]{1/30f,1/60f})
                        Require(Math.Abs(Drop(distance,dpi,dt).LandingSpeed-Math.Sqrt(3000*distance))<.02f,"contact speed independent of DPI/FPS");
            var p=new LandingResponseProfile();
            Require(p.Strength(-10)==p.minStrength&&p.Strength(100000)==p.maxStrength,"bounded strength");
            Require(p.Strength(float.NaN)==p.minStrength,"invalid speed fallback");
            Require(!new LandingResponseProfile {hardSpeed=200}.Valid()&&!new LandingResponseProfile {maxStrength=float.NaN}.Valid(),"bad profile rejected");
            hard.Grab();Require(hard.LandingSpeed==0,"grab resets impact");
            var jump=new AnimationScenario(new ModelAnimationProfile());
            jump.World.Windows[0].Bounds=new Box(180,560,620,640);
            jump.World.Surfaces=SurfaceNavigator.Build(jump.World,true);
            jump.Settings.randomBehaviors=true;jump.Controller.Profile.decisionMin=0;jump.Controller.Profile.decisionMax=0;jump.Controller.Profile.curiosity=1;
            bool jumped=false;
            for(int i=0;i<1500&&jump.Controller.State!=MikaState.Land;i++){jump.Tick(1/30f);jumped|=jump.Controller.State==MikaState.Jump;}
            Require(jumped&&jump.Controller.State==MikaState.Land&&Math.Abs(jump.Controller.LandingSpeed-595)<.02f,"jump uses trajectory contact speed");
            jump.Controller.Rescue(jump.World,240);Require(jump.Controller.LandingSpeed==0,"rescue resets impact");
            UnityEngine.Debug.Log("MIKA_LANDING_CHECKS passed=9 dropConfigurations=32");
        }
    }
}
