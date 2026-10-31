using System;

namespace Mika.Core
{
    [Serializable] public sealed class LandingResponseProfile
    {
        public float softSpeed=200,hardSpeed=1100,minStrength=.45f,maxStrength=1.15f;
        public bool Valid() => Finite(softSpeed)&&Finite(hardSpeed)&&Finite(minStrength)&&Finite(maxStrength)
            &&softSpeed>=0&&hardSpeed>softSpeed&&minStrength>0&&maxStrength>=minStrength&&maxStrength<=1.25f;
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        public float Strength(float logicalSpeed)
        {
            if(!Finite(logicalSpeed))return minStrength;
            float u=SurfaceNavigator.Clamp((logicalSpeed-softSpeed)/(hardSpeed-softSpeed),0,1);
            return minStrength+(maxStrength-minStrength)*u*u*(3-2*u);
        }
    }
}
