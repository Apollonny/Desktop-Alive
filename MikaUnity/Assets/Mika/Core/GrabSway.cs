using System;

namespace Mika.Core
{
    [Serializable] public sealed class GrabSwayProfile
    {
        public bool enabled;
        public float frequency=4.5f,damping=1.6f,initialVelocity=1.8f;
        public float speedLimit=3,inputGain=.18f,angle=2,lift=.6f,hip=.35f;
        public bool Valid()=>Finite(frequency)&&Finite(damping)&&frequency>0&&damping>0&&damping*1.1f<frequency*1.08f&&
            Finite(initialVelocity)&&Math.Abs(initialVelocity)<=4&&Finite(speedLimit)&&speedLimit>0&&speedLimit<=10&&
            Finite(inputGain)&&inputGain>=0&&inputGain<=.3f&&Finite(angle)&&angle>=0&&angle<=3&&
            Finite(lift)&&lift>=0&&lift<=1&&Finite(hip)&&hip>=0&&hip<=.5f;
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
    // Damped springs integrated analytically for a constant cursor speed over each frame.
    public sealed class GrabSway
    {
        public static readonly string[] Parameters={"walk","walk2","walk9","walk8","walk10"};
        readonly GrabSwayProfile profile;
        double left,right,lv,rv;
        public GrabSway(GrabSwayProfile profile){if(!profile.Valid())throw new ArgumentException("Invalid grab sway profile");this.profile=profile;Begin();}
        public void Begin(){left=right=0;lv=profile.initialVelocity;rv=-profile.initialVelocity*.75;}
        public void Tick(float dt,float speed)
        {
            if(dt<=0||float.IsNaN(dt)||float.IsInfinity(dt))return;
            if(float.IsNaN(speed)||float.IsInfinity(speed))speed=0;
            double target=-Clamp(speed,-profile.speedLimit,profile.speedLimit)*profile.inputGain;
            Integrate(ref left,ref lv,target,dt,profile.frequency,profile.damping);
            Integrate(ref right,ref rv,target*.8,dt,profile.frequency*1.08,profile.damping*1.1);
        }
        static void Integrate(ref double x,ref double v,double target,double dt,double frequency,double damping)
        {
            double w=Math.Sqrt(frequency*frequency-damping*damping),y=x-target;
            double e=Math.Exp(-damping*dt),c=Math.Cos(w*dt),s=Math.Sin(w*dt);
            x=Clamp(target+e*(y*c+(v+damping*y)*s/w),-1,1);
            v=e*(v*c-(damping*v+frequency*frequency*y)*s/w);
            v=Clamp(v,-8,8);
        }
        public float[] Values()=>new[]{(float)(-2+profile.lift*left),(float)(-1.7+profile.lift*right),
            (float)(profile.angle*left),(float)(profile.angle*right),(float)(profile.hip*(left+right)*.5)};
        static double Clamp(double x,double min,double max)=>Math.Max(min,Math.Min(max,x));
    }
}
