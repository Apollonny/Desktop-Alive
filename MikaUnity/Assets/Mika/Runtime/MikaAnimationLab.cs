using System;
using System.Collections;
using System.IO;
using Mika.Core;
using UnityEngine;

namespace Mika.Runtime
{
    // Isolated preview window: no desktop host, media helper, startup setting, or saved position.
    [DefaultExecutionOrder(100)]
    public sealed class MikaAnimationLab : MonoBehaviour
    {
        MikaAnimationDriver actor;
        MikaState selected=MikaState.Idle;
        bool playing=true,anchors=true;
        float speed=1,playhead;
        int face;
        AnimationScenario scenario;
        float facing=1;
        bool dragPreview;
        float dragClock;
        Vector2 panelScroll;
        string error="",notice="";
        readonly string[] faces={"Normal","Şaşkın","Surat asma","Kızgın"};
        readonly string[] faceIds={"","exp6","exp4","exp5"};
        readonly MikaState[] states=(MikaState[])Enum.GetValues(typeof(MikaState));
        IEnumerator Start()
        {
            Application.runInBackground=true;Application.targetFrameRate=30;QualitySettings.vSyncCount=0;
            Screen.SetResolution(1100,800,FullScreenMode.Windowed);
            var camera=Camera.main??new GameObject("Preview background").AddComponent<Camera>();
            camera.cullingMask=0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.075f,.085f,.12f);
            try {actor=new GameObject("Mika preview").AddComponent<MikaAnimationDriver>();actor.Initialize();}
            catch(Exception e){error=e.ToString();Debug.LogException(e);}
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--mika-smoke")>=0)
            {
                yield return new WaitForSecondsRealtime(5);
                // Stop Update sampling for one frame so validation observes a complete LateUpdate.
                validating=true;yield return null;
                if(Array.IndexOf(args,"--mika-scenarios")>=0)
                    yield return CheckScenarios();
                try
                {
                    if(!string.IsNullOrEmpty(error))throw new InvalidOperationException(error);
                    actor.CheckHitMask();actor.CheckMotionOwnership();
                    int capture=Array.IndexOf(args,"--mika-capture");
                    if(capture>=0&&capture+1<args.Length)actor.SavePortrait(args[capture+1]);
                    Debug.Log("MIKA_ANIMATION_LAB passed startup-render-ownership");
                }
                catch(Exception e){error=e.ToString();Debug.LogException(e);}
                Application.Quit(string.IsNullOrEmpty(error)?0:1);
            }
        }
        void Update()
        {
            if(actor==null||!string.IsNullOrEmpty(error)||validating)return;
            float dt=playing?Mathf.Min(.05f,Time.unscaledDeltaTime*speed):0;
            actor.ConfigurePreviewPhysics(!playing,dt);
            if(scenario!=null)
            {
                scenario.Controller.Facing=facing;
                if(playing&&dragPreview&&scenario.Controller.State==MikaState.Grabbed)
                {
                    dragClock+=dt;var offset=actor.Profile.GrabOffset(scenario.Settings.characterHeight,facing);
                    scenario.Controller.Drag(new Point(400+100*Mathf.Sin(dragClock*2),330-offset.Y),scenario.Settings.characterHeight);
                }
                if(playing)scenario.Tick(dt);
                selected=scenario.Controller.State;actor.SetState(selected);
            }
            actor.SetExpression(scenario!=null?scenario.Controller.Expression:faceIds[face]);
            actor.SetGrabPosition(scenario!=null?scenario.Controller.Feet.X:0,scenario!=null?scenario.Settings.characterHeight:320);
            actor.SetLandingStrength(scenario!=null?scenario.Controller.LandingStrength:1);
            actor.Step(dt,Vector2.zero,false,facing,false);
            playhead=actor.PlaybackTime;
        }
        bool validating;
        void OnGUI()
        {
            GUI.skin.label.wordWrap=true;
            GUILayout.BeginArea(new Rect(24,24,310,Screen.height-48),GUI.skin.box);
            GUILayout.Label("MIKA · ANİMASYON STÜDYOSU");
            GUILayout.Label("Bu pencere masaüstü davranışlarını çalıştırmaz.");
            if(!string.IsNullOrEmpty(error)){GUILayout.Label(error);GUILayout.EndArea();return;}
            if(actor==null){GUILayout.Label("Model yükleniyor…");GUILayout.EndArea();return;}
            panelScroll=GUILayout.BeginScrollView(panelScroll);
            int choice=GUILayout.SelectionGrid(Array.IndexOf(states,selected),Array.ConvertAll(states,s=>s.ToString()),3);
            if(states[choice]!=selected){scenario=null;selected=states[choice];actor.SetState(selected);actor.Seek(0);playhead=0;}
            GUILayout.Space(10);
            GUILayout.Label(actor.CurrentMotion);
            float duration=actor.Profile.Duration(selected,1);
            var stages=actor.Profile.Binding(selected).stages;
            float intro=duration-stages[stages.Length-1].duration;
            float display=stages[stages.Length-1].loop&&playhead>=intro?intro+(playhead-intro)%stages[stages.Length-1].duration:Mathf.Min(playhead,duration);
            GUILayout.Label($"{display:F2} / {duration:F2} saniye");
            GUI.enabled=scenario==null;
            float seek=GUILayout.HorizontalSlider(display,0,duration);
            if(Mathf.Abs(seek-display)>.005f){actor.Seek(seek);playhead=seek;playing=false;}
            GUI.enabled=true;
            if(GUILayout.Button(playing?"Duraklat":"Oynat"))playing=!playing;
            if(GUILayout.Button("Baştan oynat"))
            {if(scenario!=null){scenario=new AnimationScenario(actor.Profile);scenario.Drop();}actor.Seek(0);playing=true;}
            GUILayout.Label("Hız");int rate=GUILayout.Toolbar(speed==.25f?0:speed==1?1:2,new[]{"0.25×","1×","2×"});speed=rate==0?.25f:rate==1?1:2;
            face=GUILayout.Toolbar(face,faces);
            if(GUILayout.Button(facing>0?"Yön: sağ — sola çevir":"Yön: sol — sağa çevir"))facing=-facing;
            GUILayout.Space(8);GUILayout.Label("Etkileşim senaryoları");
            foreach(int distance in new[]{20,180,360})
                if(GUILayout.Button(distance+" px yükseklikten bırak"))
                {scenario=new AnimationScenario(actor.Profile);scenario.RemoveWindow();scenario.Grab(660-distance);scenario.Controller.Release();actor.Seek(0);playing=true;}
            if(scenario!=null)GUILayout.Label($"Çarpma: {scenario.Controller.LandingSpeed:F0} px/sn · Tepki: {scenario.Controller.LandingStrength:F2}");
            if(GUILayout.Button("Pencereye bırak")){scenario=new AnimationScenario(actor.Profile);scenario.Drop();actor.Seek(0);playing=true;}
            GUI.enabled=scenario!=null&&scenario.Controller.Support!=null&&scenario.Controller.Support.IsWindow;
            if(GUILayout.Button("Destek penceresini kaldır"))scenario.RemoveWindow();
            GUI.enabled=true;
            if(GUILayout.Button("Tut / uzun tutmayı dene")){scenario=new AnimationScenario(actor.Profile);scenario.Grab();actor.Seek(0);playing=true;}
            GUI.enabled=scenario!=null&&scenario.Controller.State==MikaState.Grabbed;
            if(GUILayout.Button("Bırak"))scenario.Controller.Release();
            GUI.enabled=true;
            dragPreview=GUILayout.Toggle(dragPreview,"Tutarken yatay taşıma önizlemesi");
            if(scenario!=null&&GUILayout.Button("Tek klip görünümüne dön")){scenario=null;actor.Seek(0);}
            anchors=GUILayout.Toggle(anchors,"Ayak ve yaka bağlantı noktalarını göster");
            GUILayout.Space(10);GUILayout.Label("Yaka rig durumu: "+actor.Profile.rigReview);
            if(selected==MikaState.Grabbed&&!actor.HasApprovedGrab)GUILayout.Label("Bacak salınımı geliştirme taslağı. Yaka çekilmesi ve sarkık gövde henüz yok; tutma rig'i onaysız.");
            if(GUILayout.Button("Kareyi PNG kaydet"))
            {
                string path=Path.Combine(Application.persistentDataPath,"preview-"+selected+".png");actor.SavePortrait(path);notice=path;
            }
            GUILayout.Label(notice);
            if(GUILayout.Button("Çıkış"))Application.Quit();
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            float h=Mathf.Min(Screen.height-80,720),w=h*2/3;
            Rect r=new Rect(350+(Screen.width-350-w)*.5f,40,w,h);
            if(scenario!=null)
            {
                float scale=Mathf.Min((Screen.width-370)/800f,(Screen.height-60)/700f);
                Vector2 origin=new Vector2(350,20);
                foreach(var surface in scenario.World.Surfaces)
                {GUI.color=surface.IsWindow?new Color(.4f,.6f,.9f):Color.gray;GUI.DrawTexture(new Rect(origin.x+surface.Left*scale,origin.y+surface.Y*scale,(surface.Right-surface.Left)*scale,3),Texture2D.whiteTexture);}
                GUI.color=Color.white;
                float height=scenario.Settings.characterHeight*scale;
                float rh=height*actor.Profile.textureHeight,rw=rh*2/3,pad=(rh-height)/2;
                r=new Rect(origin.x+scenario.Controller.Feet.X*scale-rw/2-actor.Profile.foot.x*height*facing,
                    origin.y+scenario.Controller.Feet.Y*scale-height-pad+actor.Profile.foot.y*height,rw,rh);
            }
            GUI.DrawTexture(r,actor.Texture,ScaleMode.StretchToFill,true);
            if(anchors)
            {
                float bodyH=h/actor.Profile.textureHeight,bottom=r.yMax-(h-bodyH)*.5f;
                Mark(new Vector2(r.center.x+actor.Profile.foot.x*bodyH*facing,bottom-actor.Profile.foot.y*bodyH),"AYAK",Color.cyan);
                Mark(new Vector2(r.center.x+actor.Profile.collar.x*bodyH*facing,bottom-actor.Profile.collar.y*bodyH),"YAKA",Color.yellow);
            }
        }
        IEnumerator CheckScenarios()
        {
            scenario=new AnimationScenario(actor.Profile);scenario.Drop();
            var visited=new System.Collections.Generic.HashSet<MikaState>();
            bool removed=false,pout=false,angry=false;
            for(int frame=0;frame<420;frame++)
            {
                if(frame==90){scenario.RemoveWindow();removed=true;visited.Clear();}
                if(frame==180)scenario.Grab();
                // The hold uses 0.05 s steps so eight-second anger is reached within this run.
                float dt=frame>=180?.05f:1/30f;
                scenario.Tick(dt);selected=scenario.Controller.State;visited.Add(selected);
                pout|=scenario.Controller.Expression=="exp4";angry|=scenario.Controller.Expression=="exp5";
                actor.SetState(selected);actor.SetExpression(scenario.Controller.Expression);
                actor.SetGrabPosition(scenario.Controller.Feet.X,scenario.Settings.characterHeight);
                actor.SetLandingStrength(scenario.Controller.LandingStrength);
                actor.ConfigurePreviewPhysics(false,dt);actor.Step(dt,Vector2.zero,false,frame%120<60?1:-1,false);
                yield return null;
                try
                {
                    actor.CheckMotionOwnership();
                    if(frame==89&&(scenario.Controller.Support?.Owner!=1||!visited.Contains(MikaState.Land)||!visited.Contains(MikaState.Recover)))throw new Exception("Window drop chain failed");
                    if(frame==179&&(scenario.Controller.Support?.Owner!=-1||!visited.Contains(MikaState.Shock)||!visited.Contains(MikaState.Land)))throw new Exception("Support loss chain failed");
                }
                catch(Exception e){error=e.ToString();Debug.LogException(e);yield break;}
            }
            if(!removed||!pout||!angry){error="Hold expression progression failed";Debug.LogError(error);}
            else Debug.Log("MIKA_SCENARIOS passed window-drop support-loss long-hold mirrored-render");
        }
        static void Mark(Vector2 point,string label,Color color)
        {
            var old=GUI.color;GUI.color=color;
            GUI.DrawTexture(new Rect(point.x-9,point.y-1,18,2),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(point.x-1,point.y-9,2,18),Texture2D.whiteTexture);
            GUI.Label(new Rect(point.x+13,point.y-11,120,24),label);GUI.color=old;
        }
    }
}
