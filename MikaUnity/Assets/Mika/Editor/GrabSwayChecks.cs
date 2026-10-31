using System;
using Mika.Core;
namespace Mika.Editor
{
    public static class GrabSwayChecks
    {
        static void Require(bool value,string name){if(!value)throw new Exception("Grab sway: "+name);}
        public static void Run()
        {
            var p=new GrabSwayProfile();var a=new GrabSway(p);var b=new GrabSway(p);
            for(int i=0;i<60;i++)a.Tick(1/30f,1);
            for(int i=0;i<120;i++)b.Tick(1/60f,1);
            for(int i=0;i<5;i++)Require(Math.Abs(a.Values()[i]-b.Values()[i])<.00001,"30/60 FPS agreement");
            var before=a.Values();a.Tick(0,10000);for(int i=0;i<5;i++)Require(a.Values()[i]==before[i],"pause");
            a=new GrabSway(p);for(int i=0;i<900;i++)a.Tick(1/60f,0);
            Require(Math.Abs(a.Values()[2])<.0001&&Math.Abs(a.Values()[3])<.0001,"settles when stationary");
            a=new GrabSway(p);for(int i=0;i<600;i++){a.Tick(1/60f,i%2==0?100000:-100000);foreach(float v in a.Values())Require(!float.IsNaN(v)&&Math.Abs(v)<=3,"bounded cursor spikes");}
            a=new GrabSway(p);a.Tick(1,2);b=new GrabSway(p);b.Tick(1,-2);
            Require(a.Values()[2]<b.Values()[2],"directional response");
            a.Begin();var c=new GrabSway(p);for(int i=0;i<5;i++)Require(a.Values()[i]==c.Values()[i],"new grab resets");
            a.Tick(float.NaN,float.PositiveInfinity);Require(!float.IsNaN(a.Values()[0]),"invalid input ignored");
            Require(!new GrabSwayProfile {damping=10}.Valid(),"invalid damping rejected");
            UnityEngine.Debug.Log("MIKA_GRAB_SWAY_CHECKS passed=8");
        }
    }
}
