using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Mika.Core;
using UnityEngine;

namespace Mika.Windows
{
    public sealed class MediaController : IMediaSessionSource,IDisposable
    {
        public MediaFrame Latest {get;private set;}=new MediaFrame();
        public bool Connected => process!=null && !process.HasExited && Time.realtimeSinceStartup-lastFrame<3;
        string pending;
        float lastFrame=-100;
        NamedPipeServerStream pipe;
        Process process;
        CancellationTokenSource cancellation=new CancellationTokenSource();
        public string Status {get;private set;}="Medya bekleniyor";
        public MediaController()
        {
            var exe=Path.Combine(Application.streamingAssetsPath,"MediaHost/Mika.MediaHost.exe");
            if(!File.Exists(exe)) {Status="Medya yardimcisi bulunamadi";return;}
            try
            {
                string name="mika-"+Guid.NewGuid().ToString("N");
                pipe=new NamedPipeServerStream(name,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                process=Process.Start(new ProcessStartInfo(exe,"--pipe "+name) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
                _=Read(cancellation.Token);
            }
            catch(Exception e) {Status=e.Message;}
        }
        async Task Read(CancellationToken token)
        {
            try
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                using(var reader=new StreamReader(pipe))
                    while(!token.IsCancellationRequested)
                    {
                        var line=await reader.ReadLineAsync().ConfigureAwait(false);
                        if(line==null)break;
                        if(line.Length<=65536)Interlocked.Exchange(ref pending,line);
                    }
            }
            catch(Exception) { /* Poll marks a disconnected or stale helper unavailable. */ }
        }
        public void Poll()
        {
            var json=Interlocked.Exchange(ref pending,null);
            if(json!=null)
            {
                try {Latest=JsonUtility.FromJson<MediaFrame>(json)??new MediaFrame();lastFrame=Time.realtimeSinceStartup;}
                catch(ArgumentException) {Latest=new MediaFrame();}
            }
            Status=Connected?(string.IsNullOrEmpty(Latest.error)?"Medya bagli":Latest.error):"Medya kullanilamiyor";
        }
        public void Dispose()
        {
            cancellation.Cancel();pipe?.Dispose();
            if(process!=null) {try {if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}process.Dispose();}
            cancellation.Dispose();
        }
    }
}
