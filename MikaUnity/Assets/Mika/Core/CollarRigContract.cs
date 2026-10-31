using System;
using System.Collections.Generic;
using System.Linq;

namespace Mika.Core
{
    // New model parameters are supplied by the artist's exported rig, never created at runtime.
    [Serializable] public sealed class CollarRigProfile
    {
        public bool enabled;
        public string pull="ParamCollarPull",hang="ParamHangBody",swing="ParamHangAngle";
        public string[] Parameters=>new[]{pull,hang,swing};
        public bool Valid()=>Parameters.All(p=>!string.IsNullOrWhiteSpace(p))&&Parameters.Distinct().Count()==3;
    }
    public struct RigParameterRange
    {
        public float Min,Default,Max;
        public RigParameterRange(float min,float neutral,float max){Min=min;Default=neutral;Max=max;}
    }
    public static class CollarRigContract
    {
        public static List<string> Validate(ModelAnimationProfile profile,IDictionary<string,RigParameterRange> ranges)
        {
            var errors=new List<string>();var rig=profile.collarRig;
            if(rig==null||!rig.Valid()){errors.Add("Invalid collar rig mapping");return errors;}
            if(!rig.enabled)
            {
                if(profile.rigReview=="approved")errors.Add("Approved grab requires enabled collar rig");
                return errors;
            }
            if(profile.grabSwayDraft.enabled)errors.Add("Disable draft sway before enabling authored collar rig");
            foreach(var id in rig.Parameters)
            {
                if(!ranges.TryGetValue(id,out var range)){errors.Add("Collar rig parameter missing: "+id);continue;}
                float minimum=id==rig.swing?-1:0;
                if(!Finite(range.Min)||!Finite(range.Max)||!Finite(range.Default)||range.Min>minimum||range.Max<1||Math.Abs(range.Default)>.0001f)
                    errors.Add("Collar rig range/neutral mismatch: "+id);
                if(profile.physicsParameters.Contains(id)||profile.expressionParameters.Contains(id))errors.Add("Collar rig ownership conflict: "+id);
            }
            foreach(var state in new[]{MikaState.Grabbed,MikaState.Release})
            {
                var binding=profile.Binding(state);
                if(binding==null||binding.stages.Length==0){errors.Add("Collar rig binding missing: "+state);continue;}
                foreach(var stage in binding.stages)
                    foreach(var id in rig.Parameters)
                        if(!stage.ownedParameters.Contains(id))errors.Add("Collar rig stage must own "+id+": "+state);
            }
            return errors;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
