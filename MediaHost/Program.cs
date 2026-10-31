using System.IO.Pipes;
using System.Text.Json;
using Windows.Media.Control;

if(args.Length!=2 || args[0]!="--pipe" || !args[1].StartsWith("mika-",StringComparison.Ordinal)) return 2;
using var cancel=new CancellationTokenSource();
Console.CancelKeyPress+=(_,e)=> {e.Cancel=true;cancel.Cancel();};
try
{
    using var pipe=new NamedPipeClientStream(".",args[1],PipeDirection.Out,PipeOptions.Asynchronous);
    await pipe.ConnectAsync(15000,cancel.Token);
    using var writer=new StreamWriter(pipe) {AutoFlush=true};
    var manager=await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    while(!cancel.IsCancellationRequested && pipe.IsConnected)
    {
        try
        {
            var sessions=manager.GetSessions().Select(s=>new {
                id=s.SourceAppUserModelId,
                playing=s.GetPlaybackInfo().PlaybackStatus==GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing
            }).ToArray();
            await writer.WriteLineAsync(JsonSerializer.Serialize(new { sessions,error="" }));
        }
        catch(Exception e)
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(new {sessions=Array.Empty<object>(),error=e.GetType().Name}));
        }
        await Task.Delay(500,cancel.Token);
    }
}
catch(OperationCanceledException) { }
catch(IOException) { }
catch(Exception e) {Console.Error.WriteLine(e.Message); return 1;}
return 0;
