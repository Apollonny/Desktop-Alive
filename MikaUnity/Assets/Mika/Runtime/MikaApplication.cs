using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mika.Core;
using Mika.Windows;
using UnityEngine;

namespace Mika.Runtime
{
    // Sample after CubismModel.Update has synchronized last frame's native parameters.
    [DefaultExecutionOrder(100)]
    public sealed class MikaApplication : MonoBehaviour
    {
        WindowsHost host;
        SettingsService settings;
        MediaController media;
        MikaReminderService reminders;
        MikaReminder activeReminder;
        readonly MediaPolicy mediaPolicy=new MediaPolicy();
        MikaController controller;
        MikaAnimationDriver actor;
        DesktopSnapshot world;
        bool showSettings,previousLeft,previousRight,pressOnMika,hidden,paused,smoke;
        Point press;
        Rect portrait,settingsRect=new Rect(24,24,490,690),reminderRect;
        Vector2 scroll;
        string error="",capturePath;
        string newReminderTitle="";
        int newReminderInterval=60,settingsTab;
        float sampleAt;
        bool diagnostics;
        bool transientRun;
        Box lastMonitor;
        int menuPending=-1;
        readonly HashSet<string> knownSources=new HashSet<string>();
        GUIStyle windowStyle,headerStyle,bodyStyle,mutedStyle,sectionStyle,buttonStyle,primaryButtonStyle,
            tabStyle,selectedTabStyle,cardStyle,alertCardStyle,toggleStyle,fieldStyle;
        Texture2D windowTexture,panelTexture,buttonTexture,hoverTexture,accentTexture,alertTexture;
        static readonly int[] ReminderIntervals={15,30,60,120,240};
        static readonly string[] ReminderIntervalLabels={"15 dk","30 dk","1 saat","2 saat","4 saat"};
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if(Environment.GetCommandLineArgs().Contains("--mika-animation-lab"))
            {new GameObject("Mika Animation Lab").AddComponent<MikaAnimationLab>();return;}
            if(FindAnyObjectByType<MikaApplication>()==null)new GameObject("MIKA LIVES HERE").AddComponent<MikaApplication>();
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            QualitySettings.vSyncCount=0;
            var args=Environment.GetCommandLineArgs();smoke=args.Contains("--mika-smoke");
            transientRun=smoke||args.Contains("--mika-profile");
            int capture=Array.IndexOf(args,"--mika-capture");if(capture>=0&&capture+1<args.Length)capturePath=args[capture+1];
            yield return null;
            try
            {
                settings=new SettingsService();Application.targetFrameRate=settings.Value.fps;
                reminders=new MikaReminderService(settings.Value,DateTime.UtcNow);
                int fpsArg=Array.IndexOf(args,"--mika-fps");
                if(fpsArg>=0&&fpsArg+1<args.Length&&int.TryParse(args[fpsArg+1],out int fpsOverride))
                    Application.targetFrameRate=settings.Value.fps=fpsOverride==60?60:30;
                var camera=Camera.main??new GameObject("Desktop camera").AddComponent<Camera>();
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=0;camera.allowHDR=false;
                host=new WindowsHost();host.SelectedMonitor=settings.Value.monitor;host.MenuAction+=id=>menuPending=id;
                world=host.Sample(settings.Value.windowInteraction,settings.Value.taskbarInteraction);host.Place(world.Monitor);lastMonitor=world.Monitor;
                string profilePath=Path.Combine(Application.streamingAssetsPath,"behavior.json");
                var profile=File.Exists(profilePath)?JsonUtility.FromJson<BehaviorProfile>(File.ReadAllText(profilePath)):new BehaviorProfile();
                actor=new GameObject("Mika").AddComponent<MikaAnimationDriver>();actor.Initialize();
                controller=new MikaController(profile,Environment.TickCount,actor.Profile);
                controller.Rescue(world,settings.Value.characterHeight*world.DpiScale,settings.Value.homeX);
                controller.Changed+=actor.SetState;
                media=new MediaController();
                Debug.Log("MIKA_READY Unity="+Application.unityVersion+" rig="+actor.RigStatus);
            }
            catch(Exception ex) {error=ex.ToString();Debug.LogException(ex);showSettings=true;}
            if(smoke||capturePath!=null)
            {
                yield return new WaitForSecondsRealtime(5);
                if(actor!=null)
                {
                    try {actor.CheckHitMask();actor.SavePortrait(capturePath??Path.Combine(Application.persistentDataPath,"mika-preview.png"));}
                    catch(Exception ex) {error=ex.Message;Debug.LogException(ex);}
                }
                if(args.Contains("--mika-gallery")&&actor!=null)
                {
                    diagnostics=true;
                    string folder=Path.GetDirectoryName(capturePath??Path.Combine(Application.persistentDataPath,"mika-preview.png"));
                    foreach(var state in new[]{MikaState.Walk,MikaState.Dance,MikaState.Jump,MikaState.React,MikaState.Release,MikaState.Shock,MikaState.Fall,MikaState.Land,MikaState.Recover})
                    {
                        actor.SetState(state);actor.SetExpression(state==MikaState.React?"exp5":"");
                        float duration=actor.Profile.Duration(state,1);
                        int frames=Mathf.Max(2,Mathf.CeilToInt(Mathf.Min(duration*.6f,1.5f)*30));
                        for(int frame=0;frame<frames;frame++) {actor.Step(1/30f,Vector2.zero,false,1,false);yield return null;}
                        actor.SavePortrait(Path.Combine(folder,"mika-"+state.ToString().ToLowerInvariant()+".png"));
                    }
                    Debug.Log("MIKA_GALLERY passed locomotion-expression-release-shock-fall-land-recover");
                }
                if(args.Contains("--mika-animation-review")&&actor!=null)
                {
                    diagnostics=true;
                    yield return ExportAnimationReview(Path.Combine(Path.GetDirectoryName(capturePath??Path.Combine(Application.persistentDataPath,"preview.png")),"animation-review"));
                }
                int rigAudit=Array.IndexOf(args,"--mika-rig-audit");
                if(args.Contains("--mika-landing-review")&&actor!=null)
                {
                    diagnostics=true;
                    yield return MikaLandingReview.Export(actor,Path.Combine(Path.GetDirectoryName(capturePath??Path.Combine(Application.persistentDataPath,"preview.png")),"landing-review"),message=>{error=message;Debug.LogError(message);});
                }
                if(args.Contains("--mika-grab-review")&&actor!=null)
                {
                    diagnostics=true;
                    yield return ExportGrabReview(Path.Combine(Path.GetDirectoryName(capturePath??Path.Combine(Application.persistentDataPath,"preview.png")),"grab-review"));
                }
                if(rigAudit>=0&&rigAudit+1<args.Length&&actor!=null)
                {
                    diagnostics=true;
                    yield return MikaRigAudit.Export(actor,args[rigAudit+1],message=>error=message);
                }
                if(smoke)Application.Quit(string.IsNullOrEmpty(error)?0:1);
            }
            if(args.Contains("--mika-profile")&&actor!=null)
            {
                yield return new WaitForSecondsRealtime(5);
                var process=System.Diagnostics.Process.GetCurrentProcess();
                var watch=System.Diagnostics.Stopwatch.StartNew();double cpu=process.TotalProcessorTime.TotalSeconds;
                int frames=0;long maxRam=0;
                while(watch.Elapsed.TotalSeconds<15)
                {
                    frames++;process.Refresh();maxRam=Math.Max(maxRam,WindowsHost.WorkingSetBytes());
                    yield return null;
                }
                double seconds=watch.Elapsed.TotalSeconds;
                string report=string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "fpsTarget={0}\nmeasuredFps={1:F1}\nsampleSeconds={2:F2}\ncpuMachinePercent={3:F2}\npeakWorkingSetMiB={4:F1}\ngraphicsDriverAllocatedMiB={5:F1}\ngpu={6}\nprocessor={7}\nlogicalCores={8}\n",
                    settings.Value.fps,frames/seconds,seconds,(process.TotalProcessorTime.TotalSeconds-cpu)/seconds/Environment.ProcessorCount*100,
                    maxRam/1048576.0,UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver()/1048576.0,SystemInfo.graphicsDeviceName,SystemInfo.processorType,Environment.ProcessorCount);
                File.WriteAllText(Path.Combine(Application.persistentDataPath,"profile-"+settings.Value.fps+".txt"),report);
                Debug.Log("MIKA_PROFILE\n"+report);process.Dispose();Application.Quit();
            }
        }
        void Update()
        {
            if(controller==null||actor==null||host==null||diagnostics)return;
            if(menuPending>=0)
            {
                int action=menuPending;menuPending=-1;
                switch(action)
                {
                    case 0:host.ShowMenu(paused);break;
                    case 1:
                        showSettings=!showSettings;
                        if(showSettings)CenterSettingsWindow();
                        break;
                    case 2:paused=!paused;break;
                    case 3:controller.Rescue(world,settings.Value.characterHeight*world.DpiScale);break;
                    case 4:Application.Quit();break;
                }
            }
            var config=settings.Value;
            config.characterHeight=Mathf.Clamp(config.characterHeight,160,Mathf.Max(160,(int)(world.WorkArea.Height/world.DpiScale*.9f)));
            if(Time.realtimeSinceStartup>=sampleAt)
            {
                sampleAt=Time.realtimeSinceStartup+.033f;
                host.SelectedMonitor=config.monitor;world=host.Sample(config.windowInteraction,config.taskbarInteraction);
                if(world.Monitor.Left!=lastMonitor.Left||world.Monitor.Top!=lastMonitor.Top||world.Monitor.Width!=lastMonitor.Width||world.Monitor.Height!=lastMonitor.Height)
                {host.Place(world.Monitor);controller.Rescue(world,config.characterHeight*world.DpiScale);lastMonitor=world.Monitor;}
            }
            media.Poll();foreach(var session in media.Latest.sessions??new MediaSession[0])
                if(!string.IsNullOrEmpty(session.id))knownSources.Add(MediaPolicy.Matches(session.id,"Spotify")?"Spotify":session.id);
            if(activeReminder!=null&&(!activeReminder.enabled||!settings.Value.reminders.Contains(activeReminder)))activeReminder=null;
            if(activeReminder==null&&reminders!=null)
            {
                activeReminder=reminders.Poll(DateTime.UtcNow);
                if(activeReminder!=null)
                {
                    SaveSettings();
                    host.ShowNotification("Mika hatırlatıyor",activeReminder.title);
                }
            }
            bool music=mediaPolicy.Tick(Time.unscaledDeltaTime,media.Latest,media.Connected,config);
            hidden=world.Fullscreen&&config.hideInFullscreen&&!showSettings;
            Application.targetFrameRate=hidden?5:config.fps;
            controller.Paused=paused||hidden||showSettings;
            var cursor=host.Cursor;
            float height=config.characterHeight*world.DpiScale;
            // Render contains 5% top/bottom padding. Feet remain on the actual support, not the texture edge.
            float renderH=height*actor.Profile.textureHeight,renderW=renderH*2/3,padding=(renderH-height)*.5f;
            float footX=actor.Profile.foot.x*height*controller.Facing,footY=actor.Profile.foot.y*height;
            portrait=new Rect(controller.Feet.X-world.Monitor.Left-renderW*.5f-footX,controller.Feet.Y-world.Monitor.Top-height-padding+footY,renderW,renderH);
            Vector2 local=new Vector2(cursor.X-world.Monitor.Left,cursor.Y-world.Monitor.Top);
            bool hit=!hidden&&actor.Hit(new Vector2((local.x-portrait.x)/portrait.width,(local.y-portrait.y)/portrait.height));
            bool reminderVisible=activeReminder!=null&&activeReminder.enabled&&config.remindersEnabled&&!showSettings;
            reminderRect=new Rect(Mathf.Max(8,Screen.width-344),18,326,150);
            bool reminderHit=reminderVisible&&reminderRect.Contains(new Vector2(local.x,local.y));
            host.SetInteractive(hit||reminderHit||pressOnMika,showSettings||reminderHit);
            bool left=host.LeftDown,right=host.RightDown;
            if(hidden||showSettings||paused)
            {
                if(pressOnMika) {controller.Release();host.Capture(false);pressOnMika=false;}
            }
            else
            {
                if(left&&!previousLeft&&hit) {pressOnMika=true;press=cursor;host.Capture(true);}
                if(left&&pressOnMika)
                {
                    float distance=Mathf.Sqrt((cursor.X-press.X)*(cursor.X-press.X)+(cursor.Y-press.Y)*(cursor.Y-press.Y));
                    if(controller.State!=MikaState.Grabbed&&distance>=controller.Profile.dragThreshold*world.DpiScale)controller.Grab();
                    var grabOffset=actor.Profile.GrabOffset(height,controller.Facing);
                    var constrained=new Point(
                        Mathf.Clamp(cursor.X,world.WorkArea.Left+height*.37f+grabOffset.X,world.WorkArea.Right-height*.37f+grabOffset.X),
                        Mathf.Clamp(cursor.Y,world.Monitor.Top+height-grabOffset.Y,world.WorkArea.Bottom-grabOffset.Y));
                    controller.Drag(constrained,height);
                }
                if(!left&&previousLeft&&pressOnMika)
                {
                    if(controller.State==MikaState.Grabbed)controller.Release();else controller.Click();
                    pressOnMika=false;host.Capture(false);
                }
            }
            if(right&&!previousRight&&hit&&!showSettings)host.ShowMenu(paused);
            previousLeft=left;previousRight=right;
            controller.Tick(Time.unscaledDeltaTime,world,config,music);
            actor.SetExpression(controller.Expression);
            actor.SetGrabPosition(controller.Feet.X,height);
            actor.SetLandingStrength(controller.LandingStrength);
            actor.Step(Time.unscaledDeltaTime,new Vector2((cursor.X-controller.Feet.X)/height,(controller.Feet.Y-height*.8f-cursor.Y)/height),config.mouseTracking,controller.Facing,hidden||paused||showSettings);
            portrait.x=controller.Feet.X-world.Monitor.Left-renderW*.5f-actor.Profile.foot.x*height*controller.Facing;portrait.y=controller.Feet.Y-world.Monitor.Top-height-padding+footY;
        }
        void OnGUI()
        {
            EnsureGuiStyles();
            if(actor!=null&&!hidden)GUI.DrawTexture(portrait,actor.Texture,ScaleMode.StretchToFill,true);
            if(showSettings)settingsRect=GUILayout.Window(937,settingsRect,SettingsWindow,"MIKA   /   AYARLAR",windowStyle);
            if(activeReminder!=null&&activeReminder.enabled&&settings!=null&&settings.Value.remindersEnabled&&!showSettings)
                DrawReminderCard();
        }
        void SettingsWindow(int id)
        {
            if(settings==null) {GUILayout.Label(error);if(GUILayout.Button("Çıkış",buttonStyle))Application.Quit();return;}
            var s=settings.Value;
            GUILayout.BeginVertical();
            GUILayout.Label("Masaüstü arkadaşının ayarları ve düzenli hatırlatmalar.",mutedStyle);
            GUILayout.BeginHorizontal();
            string[] tabs={"Genel","Davranış","Hatırlatıcılar","Uygulama"};
            for(int i=0;i<tabs.Length;i++)if(GUILayout.Toggle(settingsTab==i,tabs[i],settingsTab==i?selectedTabStyle:tabStyle,GUILayout.Height(36)))settingsTab=i;
            GUILayout.EndHorizontal();
            GUILayout.Space(7);
            scroll=GUILayout.BeginScrollView(scroll,false,true,GUILayout.Height(Mathf.Max(210,settingsRect.height-188)));
            switch(settingsTab)
            {
                case 0:DrawGeneralSettings(s);break;
                case 1:DrawBehaviorSettings(s);break;
                case 2:DrawReminderSettings(s);break;
                case 3:DrawApplicationSettings(s);break;
            }
            GUILayout.EndScrollView();
            if(!string.IsNullOrEmpty(error))GUILayout.Label(error,mutedStyle);
            GUILayout.Space(7);GUILayout.BeginHorizontal();
            if(GUILayout.Button(paused?"Devam et":"Duraklat",buttonStyle,GUILayout.Width(112)))paused=!paused;
            if(GUILayout.Button("Mika'yı bul",buttonStyle,GUILayout.Width(112)))controller?.Rescue(world,s.characterHeight*world.DpiScale);
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("Kaydet ve kapat",primaryButtonStyle,GUILayout.Width(150)))
            {if(SaveSettings()){Application.targetFrameRate=s.fps;showSettings=false;}}
            GUILayout.EndHorizontal();GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0,0,settingsRect.width,38));
        }
        void DrawGeneralSettings(MikaSettings s)
        {
            BeginCard("GÖRÜNÜM","Masaüstündeki Mika'nın boyutunu ve akıcılığını ayarla.");
            GUILayout.Label("Karakter boyutu  ·  "+s.characterHeight+" px",bodyStyle);
            s.characterHeight=Mathf.RoundToInt(GUILayout.HorizontalSlider(s.characterHeight,160,640));
            GUILayout.Space(5);GUILayout.Label("Performans",sectionStyle);
            s.fps=GUILayout.Toolbar(s.fps==30?0:1,new[]{"30 FPS","60 FPS"})==0?30:60;
            EndCard();
            BeginCard("EKRAN","Mika'nın hangi ekranda duracağını seç.");
            if(host!=null&&host.Monitors.Count>0)
            {
                foreach(var monitor in host.Monitors.ToArray())
                    if(GUILayout.Toggle(s.monitor==monitor.Name,monitor.Name+(monitor.Primary?"  ·  Ana ekran":""),toggleStyle))s.monitor=monitor.Name;
            }
            else GUILayout.Label("Ekran bilgisi alınamadı.",mutedStyle);
            EndCard();
            if(actor!=null&&!actor.HasApprovedGrab)
            {
                BeginCard("GELİŞTİRME DURUMU","Geçiş animasyonları hazır. Tam yaka deformasyonu için rig teslimi bekleniyor.");EndCard();
            }
        }
        void DrawBehaviorSettings(MikaSettings s)
        {
            BeginCard("ETKİLEŞİM","Masaüstü ve fare ile nasıl etkileşeceğini belirle.");
            s.mouseTracking=GUILayout.Toggle(s.mouseTracking,"Fare hareketini takip et",toggleStyle);
            s.randomBehaviors=GUILayout.Toggle(s.randomBehaviors,"Kendi kendine dolaşsın",toggleStyle);
            s.windowInteraction=GUILayout.Toggle(s.windowInteraction,"Pencere kenarlarında yürüsün",toggleStyle);
            s.taskbarInteraction=GUILayout.Toggle(s.taskbarInteraction,"Görev çubuğunu zemin saysın",toggleStyle);
            s.hideInFullscreen=GUILayout.Toggle(s.hideInFullscreen,"Tam ekranda gizlensin",toggleStyle);
            EndCard();
            BeginCard("MÜZİK TEPKİLERİ",media?.Status??"Medya bağlantısı bekleniyor.");
            s.musicReactions=GUILayout.Toggle(s.musicReactions,"Müzik çalarken dans etsin",toggleStyle);
            var sources=new HashSet<string>((s.mediaSources??new string[0]).Select(source=>MediaPolicy.Matches(source,"Spotify")?"Spotify":source));
            knownSources.Add("Spotify");
            foreach(var source in knownSources.OrderBy(x=>x).ToArray())
            {
                bool on=GUILayout.Toggle(sources.Contains(source),source,toggleStyle);
                if(on)sources.Add(source);else sources.Remove(source);
            }
            s.mediaSources=sources.ToArray();
            EndCard();
        }
        void DrawReminderSettings(MikaSettings s)
        {
            BeginCard("HATIRLATICILAR","Zamanı gelen hatırlatmalar Windows bildirimi ve uygulama kartıyla gösterilir.");
            s.remindersEnabled=GUILayout.Toggle(s.remindersEnabled,"Hatırlatıcıları etkinleştir",toggleStyle);
            GUILayout.Label("Bir hatırlatma ekle",sectionStyle);
            newReminderTitle=GUILayout.TextField(newReminderTitle,fieldStyle);
            newReminderInterval=GUILayout.SelectionGrid(IntervalIndex(newReminderInterval),ReminderIntervalLabels,3,tabStyle);
            newReminderInterval=ReminderIntervals[Mathf.Clamp(newReminderInterval,0,ReminderIntervals.Length-1)];
            if(GUILayout.Button("＋  Hatırlatıcı ekle",primaryButtonStyle,GUILayout.Height(38)))
            {
                if(s.reminders.Count<30)
                {
                    reminders.Add(newReminderTitle,newReminderInterval,DateTime.UtcNow);
                    newReminderTitle="";SaveSettings();
                }
                else error="En fazla 30 hatırlatıcı ekleyebilirsin.";
            }
            EndCard();
            if(s.reminders.Count==0)
            {
                BeginCard("LİSTE BOŞ","Örneğin su içmek veya kısa bir mola vermek için hatırlatma ekle.");EndCard();
                return;
            }
            foreach(var reminder in s.reminders.Where(r=>r!=null).OrderByDescending(r=>r.pending).ThenBy(r=>r.nextDueUtcTicks).ToArray())
            {
                BeginCard(reminder.pending?"SIRASI GELDİ":"HATIRLATICI","");
                GUILayout.BeginHorizontal();GUILayout.Label(reminder.title,bodyStyle);GUILayout.FlexibleSpace();
                reminder.enabled=GUILayout.Toggle(reminder.enabled,reminder.enabled?"Açık":"Kapalı",toggleStyle,GUILayout.Width(78));
                if(GUILayout.Button("Sil",buttonStyle,GUILayout.Width(54)))
                {
                    reminders.Delete(reminder.id);if(activeReminder==reminder)activeReminder=null;SaveSettings();
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Her "+FormatInterval(reminder.intervalMinutes)+"  ·  "+ReminderTime(reminder),mutedStyle);
                if(reminder.pending)
                {
                    GUILayout.BeginHorizontal();
                    if(GUILayout.Button("5 dk ertele",buttonStyle))SnoozeReminder(reminder,5);
                    if(GUILayout.Button("Tamamlandı",primaryButtonStyle))CompleteReminder(reminder);
                    GUILayout.EndHorizontal();
                }
                EndCard();
            }
        }
        void DrawApplicationSettings(MikaSettings s)
        {
            BeginCard("BAŞLANGIÇ","Mika'yı Windows oturum açılışında çalıştır.");
            bool startup=GUILayout.Toggle(s.startWithWindows,"Windows ile başlat",toggleStyle);
            if(startup!=s.startWithWindows)try{settings.SetStartup(startup);}catch(Exception ex){error=ex.Message;}
            EndCard();
            BeginCard("DİĞER","Ses oynatma bu geliştirme sürümünde etkin değil.");
            GUILayout.Label("Ses düzeyi",sectionStyle);s.volume=GUILayout.HorizontalSlider(s.volume,0,1);
            if(GUILayout.Button("Mika'yı başlangıç konumuna getir",buttonStyle))controller?.Rescue(world,s.characterHeight*world.DpiScale);
            if(GUILayout.Button("Uygulamadan çık",buttonStyle))Application.Quit();
            EndCard();
        }
        void DrawReminderCard()
        {
            GUILayout.BeginArea(reminderRect,alertCardStyle);
            GUILayout.BeginVertical();GUILayout.Label("MIKA HATIRLATIYOR",sectionStyle);
            GUILayout.Label(activeReminder.title,headerStyle);
            GUILayout.Label("Her "+FormatInterval(activeReminder.intervalMinutes),mutedStyle);
            GUILayout.FlexibleSpace();GUILayout.BeginHorizontal();
            if(GUILayout.Button("5 dk ertele",buttonStyle))SnoozeReminder(activeReminder,5);
            if(GUILayout.Button("Tamamlandı",primaryButtonStyle))CompleteReminder(activeReminder);
            GUILayout.EndHorizontal();GUILayout.EndVertical();GUILayout.EndArea();
        }
        void SnoozeReminder(MikaReminder reminder,int minutes)
        {
            if(reminders.Snooze(reminder.id,minutes,DateTime.UtcNow))
            {SaveSettings();if(activeReminder==reminder)activeReminder=null;}
        }
        void CompleteReminder(MikaReminder reminder)
        {
            if(reminders.Complete(reminder.id,DateTime.UtcNow))
            {SaveSettings();if(activeReminder==reminder)activeReminder=null;}
        }
        bool SaveSettings()
        {
            try{settings?.Save();return settings!=null;}
            catch(Exception ex){error="Ayarlar kaydedilemedi: "+ex.Message;Debug.LogWarning(error);return false;}
        }
        static int IntervalIndex(int interval)
        {
            for(int i=0;i<ReminderIntervals.Length;i++)if(ReminderIntervals[i]==interval)return i;
            return 2;
        }
        static string FormatInterval(int minutes)=>minutes>=60?(minutes%60==0?(minutes/60)+" saat":(minutes/60)+" sa "+(minutes%60)+" dk"):minutes+" dakika";
        static string ReminderTime(MikaReminder reminder)
        {
            if(reminder.pending)return "Şimdi";
            long due=reminder.snoozeUntilUtcTicks>0?reminder.snoozeUntilUtcTicks:reminder.nextDueUtcTicks;
            long remaining=Math.Max(0,due-DateTime.UtcNow.Ticks);
            int minutes=(int)Math.Min(int.MaxValue,Math.Ceiling(TimeSpan.FromTicks(remaining).TotalMinutes));
            return minutes==0?"Şimdi":minutes<60?minutes+" dk sonra":(minutes/60)+" sa "+(minutes%60)+" dk sonra";
        }
        void BeginCard(string title,string detail)
        {
            GUILayout.BeginVertical(cardStyle);GUILayout.Label(title,sectionStyle);
            if(!string.IsNullOrEmpty(detail))GUILayout.Label(detail,mutedStyle);
            GUILayout.Space(4);
        }
        static void EndCard(){GUILayout.EndVertical();GUILayout.Space(8);}
        void CenterSettingsWindow()
        {
            settingsRect.width=Mathf.Min(500,Mathf.Max(280,Screen.width-24));
            settingsRect.height=Mathf.Min(690,Mathf.Max(420,Screen.height-24));
            settingsRect.x=Mathf.Clamp((Screen.width-settingsRect.width)*.5f,0,Mathf.Max(0,Screen.width-settingsRect.width));
            settingsRect.y=Mathf.Clamp((Screen.height-settingsRect.height)*.5f,0,Mathf.Max(0,Screen.height-settingsRect.height));
        }
        void EnsureGuiStyles()
        {
            if(windowStyle!=null)return;
            windowTexture=MakeTexture(new Color32(24,28,37,248));panelTexture=MakeTexture(new Color32(37,43,55,255));
            buttonTexture=MakeTexture(new Color32(54,62,77,255));hoverTexture=MakeTexture(new Color32(73,84,101,255));
            accentTexture=MakeTexture(new Color32(183,112,120,255));alertTexture=MakeTexture(new Color32(40,43,54,252));
            windowStyle=new GUIStyle(GUI.skin.window){fontSize=17,alignment=TextAnchor.UpperCenter,padding=new RectOffset(18,18,42,16)};
            windowStyle.normal.background=windowTexture;windowStyle.onNormal.background=windowTexture;windowStyle.normal.textColor=new Color(.98f,.95f,.93f);
            headerStyle=new GUIStyle(GUI.skin.label){fontSize=17,fontStyle=FontStyle.Bold,wordWrap=true};headerStyle.normal.textColor=new Color(.98f,.94f,.92f);
            bodyStyle=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};bodyStyle.normal.textColor=new Color(.95f,.94f,.92f);
            mutedStyle=new GUIStyle(GUI.skin.label){fontSize=12,wordWrap=true};mutedStyle.normal.textColor=new Color(.69f,.72f,.77f);
            sectionStyle=new GUIStyle(GUI.skin.label){fontSize=11,fontStyle=FontStyle.Bold,wordWrap=true};sectionStyle.normal.textColor=new Color(.91f,.67f,.68f);
            cardStyle=new GUIStyle(GUI.skin.box){padding=new RectOffset(13,13,11,11)};cardStyle.normal.background=panelTexture;
            alertCardStyle=new GUIStyle(cardStyle){padding=new RectOffset(15,15,13,13)};alertCardStyle.normal.background=alertTexture;
            buttonStyle=ButtonStyle(buttonTexture,hoverTexture);primaryButtonStyle=ButtonStyle(accentTexture,hoverTexture);
            tabStyle=ButtonStyle(panelTexture,hoverTexture);selectedTabStyle=ButtonStyle(accentTexture,hoverTexture);
            toggleStyle=new GUIStyle(GUI.skin.toggle){fontSize=13,wordWrap=true,fixedHeight=27};toggleStyle.normal.textColor=bodyStyle.normal.textColor;
            fieldStyle=new GUIStyle(GUI.skin.textField){fontSize=14,fixedHeight=32,padding=new RectOffset(9,9,6,6)};
            fieldStyle.normal.background=panelTexture;fieldStyle.normal.textColor=bodyStyle.normal.textColor;fieldStyle.focused.background=panelTexture;
        }
        GUIStyle ButtonStyle(Texture2D normal,Texture2D hover)
        {
            var style=new GUIStyle(GUI.skin.button){fontSize=13,fontStyle=FontStyle.Bold,fixedHeight=34,wordWrap=true,padding=new RectOffset(8,8,4,4)};
            style.normal.background=normal;style.hover.background=hover;style.active.background=hover;
            style.onNormal.background=normal;style.onHover.background=hover;style.onActive.background=hover;
            style.normal.textColor=Color.white;style.hover.textColor=Color.white;style.active.textColor=Color.white;
            style.onNormal.textColor=Color.white;style.onHover.textColor=Color.white;style.onActive.textColor=Color.white;
            return style;
        }
        static Texture2D MakeTexture(Color color)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixel(0,0,color);texture.Apply();return texture;
        }
        void OnApplicationQuit()
        {
            if(settings!=null&&controller!=null&&world!=null&&world.WorkArea.Width>0)
                settings.Value.homeX=Mathf.Clamp01((controller.Feet.X-world.WorkArea.Left)/world.WorkArea.Width);
            try {if(!transientRun)settings?.Save();}catch(Exception ex){Debug.LogWarning(ex.Message);}
            finally {media?.Dispose();host?.Dispose();}
        }
        IEnumerator ExportAnimationReview(string folder)
        {
            Directory.CreateDirectory(folder);actor.ConfigurePreviewPhysics(false,1/30f);
            var sequence=new[]{MikaState.Idle,MikaState.Release,MikaState.Fall,MikaState.Land,MikaState.Recover,MikaState.Idle,
                MikaState.Shock,MikaState.Fall,MikaState.Land,MikaState.Recover,MikaState.React,MikaState.Walk,MikaState.Dance};
            var durations=new[]{.4f,.18f,1.6f,.28f,.56f,.4f,.22f,.8f,.28f,.56f,.6f,2f,2f};
            var subtitles=new System.Text.StringBuilder();int frame=0;
            for(int i=0;i<sequence.Length;i++)
            {
                var state=sequence[i];actor.SetState(state);
                if(i==0)actor.Seek(0);
                actor.SetExpression(state==MikaState.Shock||state==MikaState.Fall||state==MikaState.Release?"exp6":state==MikaState.React?"exp2":"");
                int count=Mathf.CeilToInt(durations[i]*30)+1,start=frame;
                for(int j=0;j<count;j++)
                {
                    actor.Step(1/30f,Vector2.zero,false,1,false);yield return null;
                    try {actor.CheckMotionOwnership();actor.SavePortrait(Path.Combine(folder,$"frame-{frame:D4}.png"),false);}
                    catch(Exception ex){error=ex.Message;Debug.LogException(ex);yield break;}
                    frame++;
                }
                subtitles.AppendLine((i+1).ToString());
                subtitles.AppendLine(SubtitleTime(start/30.0)+" --> "+SubtitleTime(frame/30.0));
                subtitles.AppendLine(state.ToString()+" | Live2D runtime");subtitles.AppendLine();
            }
            File.WriteAllText(Path.Combine(folder,"review.srt"),subtitles.ToString());
            File.WriteAllText(Path.Combine(folder,"result.txt"),$"frames={frame}\nparameterOwnership=passed\nfiniteParameters=passed\ncollarRig=pending\n");
            Debug.Log("MIKA_ANIMATION_REVIEW passed frames="+frame);
        }
        IEnumerator ExportGrabReview(string folder)
        {
            Directory.CreateDirectory(folder);var scenario=new AnimationScenario(actor.Profile);
            scenario.Controller.Changed+=actor.SetState;actor.SetState(MikaState.Idle);actor.Seek(0);
            var csv=new System.Text.StringBuilder("frame,state,expression,walk,walk2,walk9,walk8,walk10\n");
            var subs=new System.Text.StringBuilder();string lastLabel="";int labelStart=0,subtitle=0;
            float[] previous=null;float releaseJump=0;bool pout=false,angry=false;
            for(int frame=0;frame<420;frame++)
            {
                if(frame==15)scenario.Grab();
                if(frame==330)scenario.Controller.Release();
                if(frame>=120&&frame<210)
                {
                    var offset=actor.Profile.GrabOffset(240,scenario.Controller.Facing);
                    scenario.Controller.Drag(new Point(400+90*Mathf.Sin((frame-120)/30f*2.094395f),330-offset.Y),240);
                }
                if(frame==180)scenario.Controller.Facing=-1;
                scenario.Tick(1/30f);pout|=scenario.Controller.Expression=="exp4";angry|=scenario.Controller.Expression=="exp5";
                actor.SetExpression(scenario.Controller.Expression);actor.SetGrabPosition(scenario.Controller.Feet.X,240);
                actor.SetLandingStrength(scenario.Controller.LandingStrength);
                actor.ConfigurePreviewPhysics(false,1/30f);actor.Step(1/30f,Vector2.zero,false,scenario.Controller.Facing,false);
                yield return null;
                try
                {
                    actor.CheckMotionOwnership();
                    var values=GrabSway.Parameters.Select(id=>actor.Model.Parameters.First(p=>p.Id==id).Value).ToArray();
                    if(frame==330)for(int i=0;i<values.Length;i++)releaseJump=Mathf.Max(releaseJump,Mathf.Abs(values[i]-previous[i]));
                    previous=values;
                    csv.Append(frame).Append(',').Append(scenario.Controller.State).Append(',').Append(scenario.Controller.Expression);
                    foreach(float value in values)csv.Append(',').Append(value.ToString("F5",System.Globalization.CultureInfo.InvariantCulture));
                    csv.AppendLine();actor.SaveScenarioFrame(Path.Combine(folder,$"frame-{frame:D4}.png"),scenario);
                }
                catch(Exception e){error=e.ToString();Debug.LogException(e);yield break;}
                string label=scenario.Controller.State+" | "+(frame>=120&&frame<210?"Moving":frame>=15&&frame<330?"Holding":"Release / landing")+" | collar rig pending";
                if(label!=lastLabel)
                {
                    if(frame>0)subs.AppendLine((++subtitle).ToString()).AppendLine(SubtitleTime(labelStart/30.0)+" --> "+SubtitleTime(frame/30.0)).AppendLine(lastLabel).AppendLine();
                    labelStart=frame;lastLabel=label;
                }
            }
            subs.AppendLine((++subtitle).ToString()).AppendLine(SubtitleTime(labelStart/30.0)+" --> "+SubtitleTime(14)).AppendLine(lastLabel).AppendLine();
            File.WriteAllText(Path.Combine(folder,"parameters.csv"),csv.ToString());File.WriteAllText(Path.Combine(folder,"review.srt"),subs.ToString());
            float interruptJump=0;
            foreach(var source in new[]{MikaState.Walk,MikaState.Jump,MikaState.Dance})
            {
                actor.SetState(source);
                for(int n=0;n<12;n++){actor.Step(1/30f,Vector2.zero,false,1,false);yield return null;}
                foreach(var target in new[]{MikaState.Grabbed,MikaState.Release})
                {
                    var before=GrabSway.Parameters.Select(id=>actor.Model.Parameters.First(p=>p.Id==id).Value).ToArray();
                    actor.SetState(target);actor.Step(1/30f,Vector2.zero,false,1,false);yield return null;
                    actor.CheckMotionOwnership();
                    for(int i=0;i<before.Length;i++)interruptJump=Mathf.Max(interruptJump,Mathf.Abs(before[i]-actor.Model.Parameters.First(p=>p.Id==GrabSway.Parameters[i]).Value));
                }
            }
            bool passed=pout&&angry&&releaseJump<.05f&&interruptJump<.05f&&scenario.Controller.Support?.Owner==1&&!actor.HasApprovedGrab;
            File.WriteAllText(Path.Combine(folder,"result.txt"),$"frames=420\npout={pout}\nangry={angry}\nreleaseFirstFrameMaxDelta={releaseJump}\nrapidGrabCases=3\nrapidGrabMaxDelta={interruptJump}\nlandedOnWindow={scenario.Controller.Support?.Owner==1}\nrigPending={!actor.HasApprovedGrab}\npassed={passed}\n");
            if(!passed){error="Grab review invariants failed";Debug.LogError(error);}
            else Debug.Log("MIKA_GRAB_REVIEW passed 420 frames, expressions, release continuity, landing, draft status");
        }
        static string SubtitleTime(double seconds)
        {var t=TimeSpan.FromSeconds(seconds);return $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2},{t.Milliseconds:D3}";}
    }
}
