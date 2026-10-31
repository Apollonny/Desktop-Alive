using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

if(args.Length>0 && args[0]=="--probe") {NativeProbe.Run();return;}
Mika.Editor.CoreChecks.Run();
var projectRoot=new DirectoryInfo(AppContext.BaseDirectory);
while(projectRoot!=null&&!Directory.Exists(Path.Combine(projectRoot.FullName,"MikaUnity")))projectRoot=projectRoot.Parent;
if(projectRoot==null)throw new Exception("Project root not found");
var profile=JsonSerializer.Deserialize<Mika.Core.ModelAnimationProfile>(File.ReadAllText(Path.Combine(projectRoot.FullName,"MikaUnity/Assets/StreamingAssets/Mika/animation-profile.json")),new JsonSerializerOptions{IncludeFields=true})!;
Mika.Editor.AnimationChecks.Run(profile);
Mika.Editor.GrabSwayChecks.Run();
Mika.Editor.LandingChecks.Run();
Mika.Editor.CollarRigChecks.Run();
if(args.Length>0 && args[0]=="--desktop") await DesktopChecks.Run();
if(args.Length==2 && args[0]=="--media")
{
    string name="mika-test-"+Guid.NewGuid().ToString("N");
    using var pipe=new NamedPipeServerStream(name,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
    using var helper=Process.Start(new ProcessStartInfo(Path.GetFullPath(args[1]),"--pipe "+name) {UseShellExecute=false,CreateNoWindow=true});
    using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));
    try
    {
        await pipe.WaitForConnectionAsync(deadline.Token);
        using var reader=new StreamReader(pipe);
        for(int i=0;i<3;i++)
        {
            string line=await reader.ReadLineAsync(deadline.Token)??throw new Exception("Helper disconnected");
            using var doc=JsonDocument.Parse(line);
            if(doc.RootElement.GetProperty("error").GetString()!="")throw new Exception(line);
            Console.WriteLine("MEDIA_FRAME_OK sessions="+doc.RootElement.GetProperty("sessions").GetArrayLength());
        }
    }
    finally {if(helper!=null&&!helper.HasExited)helper.Kill();}
}
namespace UnityEngine { public static class Debug {public static void Log(object message)=>System.Console.WriteLine(message);} }
