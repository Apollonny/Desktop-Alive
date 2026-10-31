using System;
using System.Collections.Generic;
using System.Linq;

namespace Mika.Core
{
    [Serializable] public sealed class AnchorPoint {public float x,y;}
    [Serializable] public sealed class MotionStage
    {
        public string motion="",pose="";
        public float duration=1,blend=.15f,sourceStart=0,sourceEnd=-1;
        public bool loop,contentPending;
        public string[] ownedParameters=new string[0];
    }
    [Serializable] public sealed class AnimationBinding
    {
        public string state="Idle";
        public bool look=true,blink=true,breath=true;
        public MotionStage[] stages=new MotionStage[0];
    }
    [Serializable] public sealed class ModelAnimationProfile
    {
        public int version=1;
        public GrabSwayProfile grabSwayDraft=new GrabSwayProfile();
        public LandingResponseProfile landingResponse=new LandingResponseProfile();
        public CollarRigProfile collarRig=new CollarRigProfile();
        public string model="165 218.model3.json";
        // Coordinates in neutral character-height units, from visible bottom centre.
        public AnchorPoint foot=new AnchorPoint(),collar=new AnchorPoint {y=.78f};
        public float textureHeight=1.1f,expressionFade=.15f;
        public string[] expressionParameters=new string[0],physicsParameters=new string[0];
        public string rigReview="pending";
        public AnimationBinding[] bindings=new AnimationBinding[0];
        public AnimationBinding Binding(MikaState state) => bindings.FirstOrDefault(b=>b.state==state.ToString());
        public float Duration(MikaState state,float fallback)
        {
            var binding=Binding(state);
            return binding==null||binding.stages.Length==0?fallback:binding.stages.Sum(s=>s.duration);
        }
        public Point GrabOffset(float height,float facing) => new Point((collar.x-foot.x)*height*facing,(collar.y-foot.y)*height);
        public List<string> StructuralErrors()
        {
            var errors=new List<string>();
            if(version!=1)errors.Add("Unsupported animation profile version");
            if(grabSwayDraft==null||!grabSwayDraft.Valid())errors.Add("Invalid draft grab sway profile");
            if(landingResponse==null||!landingResponse.Valid())errors.Add("Invalid landing response profile");
            if(collarRig==null||!collarRig.Valid())errors.Add("Invalid collar rig mapping");
            if(foot==null||collar==null||!Finite(foot.x)||!Finite(foot.y)||!Finite(collar.x)||!Finite(collar.y)||collar.y<=foot.y||collar.y-foot.y>1.2f)
                errors.Add("Invalid foot/collar anchors");
            if(!Finite(textureHeight)||textureHeight<1||textureHeight>2)errors.Add("Invalid render padding");
            if(!Finite(expressionFade)||expressionFade<=0)errors.Add("Invalid expression fade");
            if(expressionParameters==null||expressionParameters.Any(string.IsNullOrWhiteSpace)||expressionParameters.Distinct().Count()!=expressionParameters.Length)errors.Add("Invalid expression ownership");
            if(physicsParameters==null||physicsParameters.Any(string.IsNullOrWhiteSpace)||physicsParameters.Distinct().Count()!=physicsParameters.Length)errors.Add("Invalid physics ownership");
            if(bindings==null) {errors.Add("Missing bindings");return errors;}
            var names=new HashSet<string>();
            foreach(var binding in bindings)
            {
                if(binding==null){errors.Add("Null binding");continue;}
                if(!Enum.TryParse(binding.state,out MikaState parsed)||!Enum.IsDefined(typeof(MikaState),parsed))errors.Add("Unknown state: "+binding.state);
                if(!names.Add(binding.state))errors.Add("Duplicate state: "+binding.state);
                if(binding.stages==null||binding.stages.Length==0){errors.Add("No stages: "+binding.state);continue;}
                for(int i=0;i<binding.stages.Length;i++)
                {
                    var s=binding.stages[i];
                    if(s==null){errors.Add("Null stage: "+binding.state);continue;}
                    if(!SafeRelativePath(s.motion)||(!string.IsNullOrEmpty(s.pose)&&!SafeRelativePath(s.pose)))errors.Add("Unsafe motion/pose path: "+binding.state);
                    if(!Finite(s.duration)||s.duration<=0||!Finite(s.blend)||s.blend<0||s.blend>s.duration)errors.Add("Invalid stage timing: "+binding.state);
                    if(!Finite(s.sourceStart)||s.sourceStart<0||!Finite(s.sourceEnd)||(s.sourceEnd!=-1&&s.sourceEnd<=s.sourceStart))errors.Add("Invalid source range: "+binding.state);
                    if(s.loop&&i!=binding.stages.Length-1)errors.Add("Loop must be the final stage: "+binding.state);
                    if(s.ownedParameters==null||s.ownedParameters.Any(string.IsNullOrWhiteSpace)||s.ownedParameters.Distinct().Count()!=s.ownedParameters.Length)errors.Add("Invalid ownership: "+binding.state);
                }
            }
            foreach(MikaState state in Enum.GetValues(typeof(MikaState)))if(!names.Contains(state.ToString()))errors.Add("Missing state: "+state);
            return errors;
        }
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
        public static bool SafeRelativePath(string path) => !string.IsNullOrWhiteSpace(path)&&!path.Contains(":")&&!path.Contains("\\")&&!path.StartsWith("/")&&!path.Split('/').Any(s=>s==".."||s=="."||s.Length==0);
    }
    public struct MotionSample
    {
        public int Stage;
        public float LocalTime,ClipTime;
        public bool Finished;
    }
    public static class MotionTimeline
    {
        public static MotionSample Evaluate(AnimationBinding binding,float time,Func<string,float> clipLength)
        {
            time=Math.Max(0,time);
            for(int i=0;i<binding.stages.Length;i++)
            {
                var stage=binding.stages[i];
                if(time>=stage.duration&&!stage.loop&&i<binding.stages.Length-1){time-=stage.duration;continue;}
                float local=stage.loop?time%stage.duration:Math.Min(time,stage.duration);
                float end=stage.sourceEnd<0?clipLength(stage.motion):stage.sourceEnd;
                return new MotionSample {Stage=i,LocalTime=local,ClipTime=stage.sourceStart+(end-stage.sourceStart)*local/stage.duration,Finished=!stage.loop&&time>=stage.duration};
            }
            throw new InvalidOperationException("Animation has no stages");
        }
    }
}
