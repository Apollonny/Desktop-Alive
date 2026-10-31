using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Mika.Core;
using Mika.Windows;

public static class NativeProbe
{
    [StructLayout(LayoutKind.Sequential)] struct MSG {public IntPtr hwnd;public uint message;public UIntPtr w;public IntPtr l;public uint time;public int x,y;public uint reserved;}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint ex,string cls,string title,uint style,int x,int y,int w,int h,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool PeekMessage(out MSG msg,IntPtr h,uint min,uint max,uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    public static void Run()
    {
        var queue=new ConcurrentQueue<string>();
        _=Task.Run(()=> {string line;while((line=Console.ReadLine())!=null)queue.Enqueue(line);queue.Enqueue("exit");});
        IntPtr window=IntPtr.Zero,cover=IntPtr.Zero;
        try
        {
            while(true)
            {
                while(PeekMessage(out var message,IntPtr.Zero,0,0,1)) {TranslateMessage(ref message);DispatchMessage(ref message);}
                if(queue.TryDequeue(out var cmd))
                {
                    var a=cmd.Split(' ');int x=0,y=0,w=0,h=0;
                    if(a.Length==5) {x=int.Parse(a[1]);y=int.Parse(a[2]);w=int.Parse(a[3]);h=int.Parse(a[4]);}
                    switch(a[0])
                    {
                        case "create":window=CreateWindowEx(0x08000008,"STATIC","MIKA: temporary desktop verification",0x90000000,x,y,w,h,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);Console.WriteLine(window.ToInt64());break;
                        case "move":SetWindowPos(window,new IntPtr(-1),x,y,w,h,0x10|0x40);Console.WriteLine("ok");break;
                        case "cover":cover=CreateWindowEx(0x08000088,"STATIC","MIKA: occlusion check",0x90000000,x,y,w,h,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);Console.WriteLine("ok");break;
                        case "uncover":DestroyWindow(cover);cover=IntPtr.Zero;Console.WriteLine("ok");break;
                        case "minimize":ShowWindow(window,6);Console.WriteLine("ok");break;
                        case "restore":ShowWindow(window,4);Console.WriteLine("ok");break;
                        case "close":DestroyWindow(window);window=IntPtr.Zero;Console.WriteLine("ok");break;
                        case "exit":return;
                    }
                }
                Thread.Sleep(5);
            }
        }
        finally {if(window!=IntPtr.Zero)DestroyWindow(window);if(cover!=IntPtr.Zero)DestroyWindow(cover);}
    }
}
public static class DesktopChecks
{
    static void Check(bool condition,string name) {if(!condition)throw new Exception("NATIVE FAILED: "+name);Console.WriteLine("NATIVE_OK "+name);}
    public static async Task Run()
    {
        using var host=new WindowsHost(false);
        using var child=Process.Start(new ProcessStartInfo(Environment.ProcessPath!,"--probe") {UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true})!;
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        async Task<string> Send(string cmd)
        {
            await child.StandardInput.WriteLineAsync(cmd);await child.StandardInput.FlushAsync();
            var response=await child.StandardOutput.ReadLineAsync(timeout.Token)??throw new Exception("Probe closed");
            await Task.Delay(150,timeout.Token);return response;
        }
        try
        {
            var world=host.Sample(true,true);
            int x=(int)world.WorkArea.Left+120,y=(int)world.WorkArea.Top+(int)(world.WorkArea.Height*.5f);
            long id=long.Parse(await Send($"create {x} {y} 520 180"));Check(id!=0,"test window created");
            world=host.Sample(true,true);
            var surface=world.Surfaces.FirstOrDefault(s=>s.Owner==id);Check(surface!=null,"real HWND discovered as support");
            var c=new MikaController(new BehaviorProfile());var settings=new MikaSettings {randomBehaviors=false};
            float height=settings.characterHeight*world.DpiScale;c.Rescue(world,height);
            c.Grab();c.Drag(new Point(x+260,y-height*.72f-40),height);c.Release();
            for(int i=0;i<100;i++)c.Tick(.02f,world,settings,false);
            Check(c.Support?.Owner==id,"drop lands on actual window");
            float beforeX=c.Feet.X,beforeY=c.Feet.Y;
            await Send($"move {x+70} {y+35} 520 180");world=host.Sample(true,true);c.Tick(.02f,world,settings,false);
            Check(Math.Abs(c.Feet.X-beforeX-70)<2&&Math.Abs(c.Feet.Y-beforeY-35)<2,"native move preserves local anchor");
            await Send($"move {x+70} {y+35} 250 180");world=host.Sample(true,true);c.Tick(.02f,world,settings,false);
            Check(c.Support?.Owner==id&&c.Support.Supports(c.Feet.X,height*.17f),"native resize clamps anchor");
            await Send($"cover {x+50} {y} 580 260");world=host.Sample(true,true);c.Tick(.02f,world,settings,false);
            Check(c.State==MikaState.Shock,"native tool window occludes support");
            await Send("uncover");await Send("minimize");world=host.Sample(true,true);
            Check(world.Surfaces.All(s=>s.Owner!=id),"minimized HWND excluded");
            await Send("restore");world=host.Sample(true,true);Check(world.Surfaces.Any(s=>s.Owner==id),"restored HWND discovered");
            await Send("close");world=host.Sample(true,true);Check(world.Surfaces.All(s=>s.Owner!=id),"destroyed HWND excluded");
            Check(WindowsHost.WorkingSetBytes()>0,"native working-set query");
            Console.WriteLine("NATIVE_DESKTOP_CHECKS passed=10");
        }
        finally {try {await child.StandardInput.WriteLineAsync("exit");}catch(IOException){}if(!child.WaitForExit(2000))child.Kill();}
    }
}
