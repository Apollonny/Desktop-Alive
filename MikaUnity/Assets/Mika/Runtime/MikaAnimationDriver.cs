using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Json;
using Mika.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mika.Runtime
{
    public sealed class MikaAnimationDriver : MonoBehaviour, IMikaAnimationDriver
    {
        public CubismModel Model {get;private set;}
        public RenderTexture Texture {get;private set;}
        public ModelAnimationProfile Profile {get;private set;}
        public bool HasApprovedGrab {get;private set;}
        public string RigStatus => HasApprovedGrab?"Grab rig ready":"Authored transition motions ready; collar rig pending";
        public string CurrentMotion {get;private set;}="";
        public float PlaybackTime=>stateTime;
        public string ValidationReport {get;private set;}
        CubismDynamicDrawableData[] latestDrawableData;
        readonly Dictionary<string,AnimationClip> clips=new Dictionary<string,AnimationClip>();
        readonly Dictionary<string,CubismExp3Json> expressions=new Dictionary<string,CubismExp3Json>();
        Dictionary<string,CubismParameter> parameters;
        float[] transition;
        float[] previousValues,finalValues;
        int[] ownerIndices=new int[0];
        HashSet<string> owners=new HashSet<string>(),physicsOwners=new HashSet<string>();
        Dictionary<string,int> parameterIndices;
        readonly Dictionary<string,float> faceWeights=new Dictionary<string,float>();
        readonly Dictionary<string,CubismExp3Json> faces=new Dictionary<string,CubismExp3Json>();
        float blendTime;
        GrabSway grabSway;
        float grabX,grabHeight=320,lastGrabX;
        bool hasGrabPosition;
        bool DraftGrab=>Profile.grabSwayDraft.enabled&&!HasApprovedGrab;
        public void SetGrabPosition(float x,float height){grabX=x;grabHeight=Mathf.Max(1,height);}
        float landingStrength=1;
        public void SetLandingStrength(float value){landingStrength=float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp(value,.01f,1.25f);}
        int lastStage=-1;
        bool seeking;
        float stateTime, elapsed;
        string expression="";
        MikaState state;
        Camera actorCamera;
        Color32[] pixels;
        bool readPending;
        bool framed;
        RenderTexture hitTexture;
        float nextRead;
        float nativeHeight,centerY;
        float eyeX,eyeY,nearFor;
        readonly List<Texture2D> textures=new List<Texture2D>();
        public void Initialize()
        {
            string root=Path.Combine(Application.streamingAssetsPath,"Mika");
            Profile=JsonUtility.FromJson<ModelAnimationProfile>(File.ReadAllText(Path.Combine(root,"animation-profile.json")));
            if(Profile==null)throw new InvalidOperationException("Animation profile missing");
            var errors=Profile.StructuralErrors();
            if(!ModelAnimationProfile.SafeRelativePath(Profile.model))errors.Add("Invalid model path");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Model=CubismModel3Json.LoadAtPath(Path.Combine(root,Profile.model),LoadAsset).ToModel();
            if(Model==null)throw new InvalidOperationException("Live2D model could not load");
            Model.OnDynamicDrawableData+=(model,data)=>latestDrawableData=data;
            Model.transform.SetParent(transform,false);
            // Clips are sampled explicitly; Unity's automatic Animator must not reset their values.
            var animator=Model.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
            foreach(var t in Model.GetComponentsInChildren<Transform>())t.gameObject.layer=8;
            var blink=Model.GetComponent<CubismEyeBlinkController>(); if(blink!=null)blink.enabled=false;
            parameters=Model.Parameters.ToDictionary(p=>p.Id);
            parameterIndices=Model.Parameters.Select((p,i)=>new {p.Id,i}).ToDictionary(p=>p.Id,p=>p.i);
            transition=Model.Parameters.Select(p=>p.Value).ToArray();
            previousValues=new float[transition.Length];finalValues=new float[transition.Length];
            physicsOwners=new HashSet<string>(Profile.physicsParameters);
            var physics=JsonUtility.FromJson<PhysicsDocument>(File.ReadAllText(Path.Combine(root,"165 218.physics3.json")));
            var actualPhysics=new HashSet<string>(physics.PhysicsSettings.SelectMany(s=>s.Output).Select(o=>o.Destination.Id));
            if(!actualPhysics.SetEquals(physicsOwners))errors.Add("Physics ownership does not match model physics file");
            var pending=new HashSet<string>();var warnings=new HashSet<string>();
            errors.AddRange(CollarRigContract.Validate(Profile,parameters.ToDictionary(p=>p.Key,p=>new RigParameterRange(p.Value.MinimumValue,p.Value.DefaultValue,p.Value.MaximumValue))));
            foreach(var binding in Profile.bindings)foreach(var stage in binding.stages)
            {
                foreach(var id in stage.ownedParameters)if(!parameters.ContainsKey(id))errors.Add("Unknown owned parameter: "+id);
                if(stage.contentPending)pending.Add(stage.motion);
                string path=Path.Combine(root,stage.motion);
                if(!File.Exists(path)) {if(!stage.contentPending)errors.Add("Missing motion: "+stage.motion);continue;}
                var motion=CubismMotion3Json.LoadFrom(File.ReadAllText(path));
                if(Profile.collarRig.enabled&&(binding.state=="Grabbed"||binding.state=="Release"))
                    foreach(var id in Profile.collarRig.Parameters)
                        if(!motion.Curves.Any(c=>c.Target=="Parameter"&&c.Id==id))errors.Add("Collar curve missing: "+id+" in "+stage.motion);
                if(stage.sourceStart>=motion.Meta.Duration||stage.sourceEnd>motion.Meta.Duration+.001f)errors.Add("Source range exceeds motion: "+stage.motion);
                foreach(var curve in motion.Curves)
                {
                    if(curve.Target!="Parameter") {errors.Add("Unsupported non-parameter curve: "+stage.motion);continue;}
                    if(!parameters.ContainsKey(curve.Id))errors.Add("Unknown curve parameter: "+curve.Id);
                    else if(!stage.ownedParameters.Contains(curve.Id))
                    {
                        if(physicsOwners.Contains(curve.Id))warnings.Add("Physics retains "+curve.Id+" in "+stage.motion);
                        else errors.Add("Curve has no owner: "+curve.Id+" in "+stage.motion);
                    }
                }
                if(!clips.ContainsKey(stage.motion)) {var clip=motion.ToAnimationClip();clip.legacy=true;clips[stage.motion]=clip;}
                if(!string.IsNullOrEmpty(stage.pose))
                {
                    string posePath=Path.Combine(root,stage.pose);
                    if(!File.Exists(posePath))errors.Add("Missing pose: "+stage.pose);
                    else expressions[stage.pose]=CubismExp3Json.LoadFrom(File.ReadAllText(posePath));
                }
            }
            foreach(var path in Directory.GetFiles(Path.Combine(root,"exp"),"*.exp3.json"))
            {
                var face=CubismExp3Json.LoadFrom(File.ReadAllText(path));
                foreach(var id in Profile.expressionParameters)if(face.Parameters.Any(p=>p.Id==id))faces[id]=face;
            }
            foreach(var id in Profile.expressionParameters)
            {if(!parameters.ContainsKey(id))errors.Add("Unknown expression parameter: "+id);faceWeights[id]=0;}
            ValidationReport=JsonUtility.ToJson(new AnimationReport {errors=errors.Distinct().ToArray(),warnings=warnings.ToArray(),pending=pending.ToArray(),rigReview=Profile.rigReview},true);
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(Path.Combine(Application.persistentDataPath,"animation-validation.json"),ValidationReport);
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors.Distinct()));
            HasApprovedGrab=Profile.collarRig.enabled&&pending.Count==0&&Profile.rigReview=="approved";
            grabSway=new GrabSway(Profile.grabSwayDraft);
            if(DraftGrab)foreach(var id in GrabSway.Parameters)if(!parameters.ContainsKey(id))throw new InvalidOperationException("Draft sway parameter missing: "+id);
            Model.gameObject.AddComponent<MikaParameterLayer>().Driver=this;
            var vertices=Model.Drawables.SelectMany(d=>d.VertexPositions).ToArray();
            float bottom=vertices.Min(v=>v.y),top=vertices.Max(v=>v.y);
            nativeHeight=top-bottom;centerY=(top+bottom)*.5f;
            Texture=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32) {name="Mika transparent render",antiAliasing=1}; Texture.Create();
            hitTexture=new RenderTexture(128,192,0,RenderTextureFormat.ARGB32);hitTexture.Create();
            actorCamera=new GameObject("Mika portrait camera").AddComponent<Camera>();
            actorCamera.orthographic=true; actorCamera.orthographicSize=nativeHeight*.5f*Profile.textureHeight;
            actorCamera.transform.position=new Vector3(0,centerY,-10);
            actorCamera.clearFlags=CameraClearFlags.SolidColor;actorCamera.backgroundColor=Color.clear;
            actorCamera.cullingMask=1<<8;actorCamera.targetTexture=Texture;actorCamera.allowHDR=false;actorCamera.allowMSAA=false;
            actorCamera.depth=-10;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllLines(Path.Combine(Application.persistentDataPath,"model-parameters.tsv"),Model.Parameters.Select(p=>$"{p.Id}\t{p.MinimumValue}\t{p.DefaultValue}\t{p.MaximumValue}"));
        }
        object LoadAsset(Type type,string path)
        {
            if(type==typeof(byte[]))return File.ReadAllBytes(path);
            if(type==typeof(string))return File.ReadAllText(path);
            if(type==typeof(Texture2D)) {var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);texture.LoadImage(File.ReadAllBytes(path),true);textures.Add(texture);return texture;}
            throw new NotSupportedException(type.FullName);
        }
        public void SetState(MikaState next)
        {
            if(state==next)return;
            CaptureTransition();state=next;stateTime=0;lastStage=-1;
            if(next==MikaState.Grabbed){grabSway.Begin();hasGrabPosition=false;}
        }
        void CaptureTransition() {for(int i=0;i<transition.Length;i++)transition[i]=Model.Parameters[i].Value;blendTime=0;}
        public void Seek(float time) {stateTime=Math.Max(0,time);lastStage=-1;seeking=true;}
        public void ConfigurePreviewPhysics(bool paused,float step)
        {
            Live2D.Cubism.Framework.Physics.CubismPhysics.UseFixedDeltaTime=true;
            Time.fixedDeltaTime=Mathf.Max(.0001f,step);
            var physics=Model.GetComponent<Live2D.Cubism.Framework.Physics.CubismPhysicsController>();
            if(physics!=null)physics.enabled=!paused;
        }
        public void SetExpression(string value) {expression=value;}
        public void Step(float dt,Vector2 look,bool tracking,float facing,bool frozen)
        {
            actorCamera.enabled=!frozen;
            if(Model.gameObject.activeSelf==frozen)Model.gameObject.SetActive(!frozen);
            if(frozen){lastGrabX=grabX;return;}
            elapsed+=dt;
            var binding=Profile.Binding(state);
            var sample=MotionTimeline.Evaluate(binding,stateTime,name=>clips.TryGetValue(name,out var c)?c.length:1);
            var stage=binding.stages[sample.Stage];CurrentMotion=stage.motion;
            if(lastStage!=sample.Stage)
            {
                if(lastStage>=0)CaptureTransition();lastStage=sample.Stage;
                owners=new HashSet<string>(stage.ownedParameters);
                if(DraftGrab&&(state==MikaState.Grabbed||state==MikaState.Release))owners.UnionWith(GrabSway.Parameters);
                ownerIndices=owners.Select(id=>parameterIndices[id]).ToArray();
            }
            for(int i=0;i<Model.Parameters.Length;i++)
            {
                var p=Model.Parameters[i];previousValues[i]=p.Value;
                if(!physicsOwners.Contains(p.Id)||owners.Contains(p.Id))p.Value=p.DefaultValue;
            }
            if(clips.TryGetValue(stage.motion,out var clip))clip.SampleAnimation(Model.gameObject,sample.ClipTime);
            // Cubism clips may contain augmented physics curves. Only declared owners retain their values.
            for(int i=0;i<Model.Parameters.Length;i++)if(!owners.Contains(Model.Parameters[i].Id))
                Model.Parameters[i].Value=physicsOwners.Contains(Model.Parameters[i].Id)?previousValues[i]:Model.Parameters[i].DefaultValue;
            if(!string.IsNullOrEmpty(stage.pose)&&expressions.TryGetValue(stage.pose,out var pose))ApplyExpression(pose,1,owners);
            if(state==MikaState.Land||state==MikaState.Recover)
                foreach(var id in new[]{"ParamAngleY","ParamBodyAngleY","walk","walk2"})
                    if(owners.Contains(id)&&parameters.TryGetValue(id,out var landingParameter))
                        landingParameter.Value=landingParameter.DefaultValue+(landingParameter.Value-landingParameter.DefaultValue)*landingStrength;
            if(DraftGrab&&state==MikaState.Grabbed)
            {
                float speed=hasGrabPosition&&dt>0?(grabX-lastGrabX)/grabHeight/dt*facing:0;
                if(seeking){grabSway.Begin();grabSway.Tick(stateTime,0);}
                grabSway.Tick(dt,speed);lastGrabX=grabX;hasGrabPosition=true;
                var values=grabSway.Values();for(int i=0;i<values.Length;i++)Set(GrabSway.Parameters[i],values[i]);
                CurrentMotion+=" [leg-sway draft; collar pending]";
            }
            if(binding.breath&&!owners.Contains("ParamBreath"))Set("ParamBreath",.5f+.5f*Mathf.Sin(elapsed*1.7f));
            float blinkPhase=elapsed%4.7f,blink=blinkPhase<.18f?Mathf.Abs(blinkPhase-.09f)/.09f:1;
            if(binding.blink)
            {if(!owners.Contains("ParamEyeLOpen"))Set("ParamEyeLOpen",blink);if(!owners.Contains("ParamEyeROpen"))Set("ParamEyeROpen",blink);}
            bool near=look.magnitude<1.6f;
            nearFor=near?nearFor+dt:0;
            eyeX=Mathf.Lerp(eyeX,tracking&&near?Mathf.Clamp(look.x,-1,1):0,1-Mathf.Exp(-dt*9));
            eyeY=Mathf.Lerp(eyeY,tracking&&near?Mathf.Clamp(look.y,-1,1):0,1-Mathf.Exp(-dt*9));
            if(!owners.Contains("ParamEyeBallX"))Set("ParamEyeBallX",eyeX);
            if(!owners.Contains("ParamEyeBallY"))Set("ParamEyeBallY",eyeY);
            if(tracking && nearFor>.5f && binding.look)
            {if(!owners.Contains("ParamAngleX"))Set("ParamAngleX",eyeX*18);if(!owners.Contains("ParamAngleY"))Set("ParamAngleY",eyeY*12);}
            var faceOwners=new HashSet<string>(Profile.expressionParameters);
            foreach(var id in Profile.expressionParameters)
            {
                float target=id==expression?1:0;
                faceWeights[id]=seeking?target:Mathf.MoveTowards(faceWeights[id],target,dt/Profile.expressionFade);
                if(faceWeights[id]>0)
                {if(faces.TryGetValue(id,out var face))ApplyExpression(face,faceWeights[id],faceOwners);else Set(id,faceWeights[id]);}
            }
            float blend=seeking||stage.blend==0?1:Mathf.SmoothStep(0,1,blendTime/stage.blend);
            for(int i=0;i<transition.Length;i++)
            {
                var p=Model.Parameters[i];
                if(!physicsOwners.Contains(p.Id)||owners.Contains(p.Id))
                    p.Value=Mathf.Clamp(Mathf.Lerp(transition[i],p.Value,blend),p.MinimumValue,p.MaximumValue);
                finalValues[i]=p.Value;
            }
            blendTime+=dt;stateTime+=dt;seeking=false;
            Model.transform.localScale=new Vector3(facing,1,1);
            // Headless graphics smoke tests do not run Unity's normal camera render loop.
            if(Application.isBatchMode)actorCamera.Render();
            if(Time.realtimeSinceStartup>=nextRead && !readPending && SystemInfo.supportsAsyncGPUReadback)
            {
                nextRead=Time.realtimeSinceStartup+.05f;readPending=true;
                Graphics.Blit(Texture,hitTexture);
                AsyncGPUReadback.Request(hitTexture,0,TextureFormat.RGBA32,request=> {
                    readPending=false;if(this==null||request.hasError)return;
                    pixels=request.GetData<Color32>().ToArray();
                    if(!framed&&elapsed>=1)
                    {
                        int low=hitTexture.height,high=-1;
                        for(int y=0;y<hitTexture.height;y++)for(int x=0;x<hitTexture.width;x++)
                            if(pixels[y*hitTexture.width+x].a>25) {low=Math.Min(low,y);high=Math.Max(high,y);}
                        if(high-low>20)
                        {
                            float fullHeight=actorCamera.orthographicSize*2;
                            float offset=((low+high+1)*.5f/hitTexture.height-.5f)*fullHeight;
                            actorCamera.transform.position+=new Vector3(0,offset,0);
                            actorCamera.orthographicSize=fullHeight*(high-low+1)/hitTexture.height*.5f*Profile.textureHeight;
                            framed=true;pixels=null;
                        }
                    }
                });
            }
        }
        public void ApplyFinalOwners()
        {
            foreach(int i in ownerIndices)Model.Parameters[i].Value=finalValues[i];
        }
        void ApplyExpression(CubismExp3Json exp,float weight,HashSet<string> allowed)
        {
            foreach(var item in exp.Parameters)
                if(allowed.Contains(item.Id)&&parameters.TryGetValue(item.Id,out var p))
                    p.Value=item.Blend=="Multiply"?p.Value*Mathf.Lerp(1,item.Value,weight):item.Blend=="Overwrite"?Mathf.Lerp(p.Value,item.Value,weight):p.Value+item.Value*weight;
        }
        void Set(string id,float value) {if(parameters.TryGetValue(id,out var p))p.Value=value;}
        public bool Hit(Vector2 normalized)
        {
            if(pixels==null || normalized.x<0||normalized.x>=1||normalized.y<0||normalized.y>=1)return false;
            int x=Mathf.Clamp((int)(normalized.x*hitTexture.width),0,hitTexture.width-1);
            int y=Mathf.Clamp((int)((1-normalized.y)*hitTexture.height),0,hitTexture.height-1);
            return pixels[y*hitTexture.width+x].a>25;
        }
        public void SavePortrait(string path,bool log=true)
        {
            actorCamera.Render();var old=RenderTexture.active;RenderTexture.active=Texture;
            var image=new Texture2D(Texture.width,Texture.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,Texture.width,Texture.height),0,0);image.Apply();
            int visible=image.GetPixels32().Count(p=>p.a>25);
            if(log)Debug.Log("MIKA_RENDER visiblePixels="+visible+" total="+(Texture.width*Texture.height));
            File.WriteAllBytes(path,image.EncodeToPNG());Destroy(image);RenderTexture.active=old;
            if(visible<100)throw new InvalidOperationException("Live2D render is empty");
        }
        public void SaveScenarioFrame(string path,AnimationScenario scenario)
        {
            actorCamera.Render();var previous=RenderTexture.active;
            var canvas=RenderTexture.GetTemporary(800,700,0,RenderTextureFormat.ARGB32);
            var picture=new Texture2D(800,700,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=canvas;GL.Clear(true,true,new Color(.04f,.055f,.08f));
                GL.PushMatrix();
                try
                {
                    GL.LoadPixelMatrix(0,800,700,0);
                    foreach(var surface in scenario.World.Surfaces)
                        Graphics.DrawTexture(new Rect(surface.Left,surface.Y,surface.Right-surface.Left,2),Texture2D.whiteTexture,new Rect(0,0,1,1),0,0,0,0,surface.IsWindow?new Color(.35f,.6f,.9f):Color.gray);
                    float h=scenario.Settings.characterHeight,rh=h*Profile.textureHeight,rw=rh*2/3,pad=(rh-h)/2;
                    Graphics.DrawTexture(new Rect(scenario.Controller.Feet.X-rw/2-Profile.foot.x*h*scenario.Controller.Facing,
                        scenario.Controller.Feet.Y-h-pad+Profile.foot.y*h,rw,rh),Texture);
                }
                finally {GL.PopMatrix();}
                picture.ReadPixels(new Rect(0,0,800,700),0,0);picture.Apply();File.WriteAllBytes(path,picture.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(canvas);Destroy(picture);}
        }
        public void CheckMotionOwnership()
        {
            foreach(int i in ownerIndices)
                if(Math.Abs(Model.Parameters[i].Value-finalValues[i])>.001f)
                    throw new InvalidOperationException("Motion owner overwritten after physics: "+Model.Parameters[i].Id);
            foreach(var p in Model.Parameters)if(float.IsNaN(p.Value)||float.IsInfinity(p.Value))
                throw new InvalidOperationException("Non-finite model parameter: "+p.Id);
        }
        float[] rigBaseline;
        public void SetRigProbe(string parameter,float value,float facing=1)
        {
            // Explicit diagnostic mode only: isolate the exported deformation from motion and physics.
            ConfigurePreviewPhysics(true,1/30f);
            // Freeze the settled runtime idle, including physics outputs required by this model.
            if(rigBaseline==null)rigBaseline=Model.Parameters.Select(p=>p.Value).ToArray();
            for(int i=0;i<Model.Parameters.Length;i++)Model.Parameters[i].Value=rigBaseline[i];
            if(!string.IsNullOrEmpty(parameter))
            {
                if(!parameters.TryGetValue(parameter,out var p))throw new InvalidOperationException("Unknown rig probe parameter: "+parameter);
                if(value<p.MinimumValue||value>p.MaximumValue)throw new InvalidOperationException("Rig probe outside exported range");
                p.Value=value;
            }
            Model.transform.localScale=new Vector3(facing,1,1);
            ownerIndices=Enumerable.Range(0,Model.Parameters.Length).ToArray();
            for(int i=0;i<finalValues.Length;i++)finalValues[i]=Model.Parameters[i].Value;
        }
        public void RenderPreviewFrame()
        {
            actorCamera.Render();
        }
        public void SaveGeometryEvidence(string path)
        {
            if(latestDrawableData==null||latestDrawableData.Length!=Model.Drawables.Length)throw new InvalidOperationException("Drawable evidence mapping unavailable");
            var rows=new List<DrawableEvidence>();
            for(int i=0;i<Model.Drawables.Length;i++)
            {
                var drawable=Model.Drawables[i];var data=latestDrawableData[i];
                var renderer=drawable.GetComponent<MeshRenderer>();var mesh=drawable.GetComponent<MeshFilter>().sharedMesh;
                var vertices=mesh.vertices;float error=0;
                if(vertices.Length!=data.VertexPositions.Length)throw new InvalidOperationException("Drawable vertex count mismatch");
                for(int v=0;v<vertices.Length;v++)error=Mathf.Max(error,Vector3.Distance(vertices[v],data.VertexPositions[v]));
                rows.Add(new DrawableEvidence {id=drawable.Id,visible=data.IsVisible,enabled=renderer.enabled,opacity=data.Opacity,
                    vertexError=error,maskCount=drawable.Masks.Length,shader=renderer.sharedMaterial.shader.name,
                    minY=data.VertexPositions.Min(v=>v.y),maxY=data.VertexPositions.Max(v=>v.y),z=drawable.transform.localPosition.z});
            }
            File.WriteAllText(path,JsonUtility.ToJson(new GeometryEvidence {drawables=rows.ToArray()},true));
        }
        public void SaveUnmaskedEvidence(string path)
        {
            var renderers=Model.GetComponentsInChildren<MeshRenderer>();
            var originals=renderers.Select(r=>r.sharedMaterial).ToArray();
            var copies=new Dictionary<Material,Material>();
            try
            {
                for(int i=0;i<renderers.Length;i++)
                {
                    if(!copies.TryGetValue(originals[i],out var copy))
                    {copy=new Material(originals[i]);copy.DisableKeyword("CUBISM_MASK_ON");copy.DisableKeyword("CUBISM_INVERT_ON");copies.Add(originals[i],copy);}
                    renderers[i].sharedMaterial=copy;
                }
                SavePortrait(path,false);
            }
            finally
            {for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterial=originals[i];foreach(var copy in copies.Values)Destroy(copy);}
        }
        public void CheckHitMask()
        {
            if(pixels==null)throw new InvalidOperationException("Hit mask never became available");
            int solid=Array.FindIndex(pixels,p=>p.a>25),clear=Array.FindIndex(pixels,p=>p.a==0);
            if(solid<0||clear<0)throw new InvalidOperationException("Hit mask lacks opaque or transparent pixels");
            Func<int,Vector2> position=i=>new Vector2((i%hitTexture.width+.5f)/hitTexture.width,1-(i/hitTexture.width+.5f)/hitTexture.height);
            if(!Hit(position(solid))||Hit(position(clear)))throw new InvalidOperationException("Alpha hit test mismatch");
            Debug.Log("MIKA_HITMASK passed opaque-and-transparent");
        }
        void OnDestroy()
        {
            if(RenderTexture.active==Texture||RenderTexture.active==hitTexture)RenderTexture.active=null;
            if(Texture!=null) {Texture.Release();Destroy(Texture);}
            if(hitTexture!=null) {hitTexture.Release();Destroy(hitTexture);}
            if(actorCamera!=null)Destroy(actorCamera.gameObject);
            foreach(var texture in textures)Destroy(texture);
            foreach(var clip in clips.Values)Destroy(clip);
        }
    }
    [Serializable] public sealed class AnimationReport {public string[] errors,warnings,pending;public string rigReview;}
    [Serializable] public sealed class GeometryEvidence {public DrawableEvidence[] drawables;}
    [Serializable] public sealed class DrawableEvidence {public string id,shader;public bool visible,enabled;public float opacity,vertexError,minY,maxY,z;public int maskCount;}
    [Serializable] sealed class PhysicsDocument {public PhysicsSetting[] PhysicsSettings;}
    [Serializable] sealed class PhysicsSetting {public PhysicsOutput[] Output;}
    [Serializable] sealed class PhysicsOutput {public PhysicsDestination Destination;}
    [Serializable] sealed class PhysicsDestination {public string Id;}
    // Cubism physics runs in the ordinary LateUpdate. Reassert only explicitly owned motion
    // parameters afterwards; Cubism's model evaluation follows in the player loop.
    [DefaultExecutionOrder(1000)]
    public sealed class MikaParameterLayer : MonoBehaviour
    {
        public MikaAnimationDriver Driver;
        void LateUpdate() {if(Driver!=null)Driver.ApplyFinalOwners();}
    }
}
