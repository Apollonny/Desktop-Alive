using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Mika.Core;
using UnityEngine;

namespace Mika.Windows
{
    public sealed class WindowsHost : IDesktopEnvironment, IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; public Box Box => new Box(L,T,R,B); }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct MONITORINFO
        { public int Size; public RECT Monitor,Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Device; }
        [StructLayout(LayoutKind.Sequential)] struct MARGINS { public int L,R,T,B; }
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct NOTIFYICONDATA
        {
            public int Size; public IntPtr Window; public uint Id,Flags,Callback; public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Tip;
            public uint State,StateMask;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string Info;
            public uint Timeout;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string InfoTitle;
            public uint InfoFlags; public Guid Guid; public IntPtr Balloon;
        }
        delegate bool EnumProc(IntPtr hwnd,IntPtr param);
        delegate bool MonitorProc(IntPtr monitor,IntPtr dc,ref RECT rect,IntPtr data);
        delegate IntPtr WndProc(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr param);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder s,int size);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out RECT rect);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetLong(IntPtr h,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetLong(IntPtr h,int index,IntPtr value);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] static extern IntPtr SetCapture(IntPtr h);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,MonitorProc callback,IntPtr data);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h,ref MONITORINFO info);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor,int type,out uint x,out uint y);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out RECT value,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] static extern int DwmInt(IntPtr h,int attribute,out int value,int size);
        [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref MARGINS margins);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
        [DllImport("user32.dll")] static extern IntPtr CallWindowProc(IntPtr previous,IntPtr h,uint m,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] static extern IntPtr LoadIcon(IntPtr instance,IntPtr name);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern bool Shell_NotifyIcon(uint command,ref NOTIFYICONDATA data);
        [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string label);
        [DllImport("user32.dll")] static extern uint TrackPopupMenu(IntPtr menu,uint flags,int x,int y,int reserved,IntPtr h,IntPtr rect);
        [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [StructLayout(LayoutKind.Sequential)] struct MEMORY
        {public uint Size,Faults;public UIntPtr PeakWorking,Working,PeakPaged,Paged,PeakNonPaged,NonPaged,Pagefile,PeakPagefile;}
        [DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(IntPtr process,out MEMORY memory,uint size);
        public static long WorkingSetBytes()
        {
            return GetProcessMemoryInfo(new IntPtr(-1),out var memory,(uint)Marshal.SizeOf<MEMORY>())?(long)memory.Working.ToUInt64():-1;
        }
        public sealed class Monitor { public string Name; public Box Bounds,Work; public float Scale; public bool Primary; }
        public readonly List<Monitor> Monitors=new List<Monitor>();
        public string SelectedMonitor="";
        public IntPtr Handle { get; private set; }
        public bool Native => !Application.isEditor && !Application.isBatchMode && Application.platform==RuntimePlatform.WindowsPlayer;
        public event Action<int> MenuAction;
        WndProc callback; IntPtr previousProc,originalStyle,originalEx;
        bool interactive,settingsOpen;
        uint taskbarCreated;
        IntPtr previousDpi;
        NOTIFYICONDATA tray;
        readonly uint processId=(uint)Process.GetCurrentProcess().Id;
        public WindowsHost(bool manageOverlay=true)
        {
            if(!Native) return;
            previousDpi=SetThreadDpiAwarenessContext(new IntPtr(-4));
            if(!manageOverlay)return;
            taskbarCreated=RegisterWindowMessage("TaskbarCreated");
            EnumWindows((h,p)=> {
                GetWindowThreadProcessId(h,out uint id);
                if(id==processId) {var cls=new StringBuilder(128);GetClassName(h,cls,128);if(cls.ToString()=="UnityWndClass") {Handle=h;return false;}}
                return true;
            },IntPtr.Zero);
            if(Handle==IntPtr.Zero) throw new InvalidOperationException("Unity window not found");
            originalStyle=GetLong(Handle,-16); originalEx=GetLong(Handle,-20);
            SetLong(Handle,-16,new IntPtr(unchecked((long)0x80000000)));
            SetLong(Handle,-20,new IntPtr(0x80000|0x80|0x08000000|0x20));
            // DWM glass preserves alpha from the D3D11 backbuffer; color-key fallback is not used.
            var margins=new MARGINS { L=-1,R=-1,T=-1,B=-1 }; DwmExtendFrameIntoClientArea(Handle,ref margins);
            SetLayeredWindowAttributes(Handle,0,255,2);
            callback=WindowMessage; previousProc=SetLong(Handle,-4,Marshal.GetFunctionPointerForDelegate(callback));
            tray=new NOTIFYICONDATA { Size=Marshal.SizeOf<NOTIFYICONDATA>(),Window=Handle,Id=1,Flags=1|2|4,Callback=0x8001,
                Icon=LoadIcon(IntPtr.Zero,new IntPtr(32512)),Tip="MIKA LIVES HERE" };
            Shell_NotifyIcon(0,ref tray);
        }
        public Point Cursor
        {
            get { if(Native) { GetCursorPos(out POINT p); return new Point(p.X,p.Y); }
                return new Point(Input.mousePosition.x,Screen.height-Input.mousePosition.y); }
        }
        public bool LeftDown => Native ? (GetAsyncKeyState(1)&0x8000)!=0 : Input.GetMouseButton(0);
        public bool RightDown => Native ? (GetAsyncKeyState(2)&0x8000)!=0 : Input.GetMouseButton(1);
        public void Capture(bool capture) { if(!Native)return; if(capture)SetCapture(Handle); else ReleaseCapture(); }
        public void SetInteractive(bool hit,bool settings)
        {
            if(!Native || (interactive==hit && settingsOpen==settings)) return;
            interactive=hit; settingsOpen=settings;
            SetLong(Handle,-20,new IntPtr(0x80000|0x80|(settings?0:0x08000000)|(hit||settings?0:0x20)));
            if(settings) SetForegroundWindow(Handle);
        }
        public void Place(Box monitor)
        {
            if(Native) SetWindowPos(Handle,new IntPtr(-1),(int)monitor.Left,(int)monitor.Top,(int)monitor.Width,(int)monitor.Height,0x10|0x20|0x40);
        }
        public DesktopSnapshot Sample(bool windows,bool taskbar)
        {
            var snapshot=new DesktopSnapshot();
            if(!Native) { snapshot.Monitor=snapshot.WorkArea=new Box(0,0,Screen.width,Screen.height); snapshot.Surfaces=SurfaceNavigator.Build(snapshot,false); return snapshot; }
            Monitors.Clear();
            EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr h,IntPtr dc,ref RECT r,IntPtr data)=> {
                var info=new MONITORINFO {Size=Marshal.SizeOf<MONITORINFO>()}; GetMonitorInfo(h,ref info);
                GetDpiForMonitor(h,0,out uint x,out uint y);
                Monitors.Add(new Monitor {Name=info.Device,Bounds=info.Monitor.Box,Work=info.Work.Box,Primary=(info.Flags&1)!=0,Scale=x/96f}); return true;
            },IntPtr.Zero);
            var selected=Monitors.Find(m=>m.Name==SelectedMonitor)??Monitors.Find(m=>m.Primary)??Monitors[0];
            snapshot.Monitor=selected.Bounds; snapshot.WorkArea=taskbar?selected.Work:selected.Bounds; snapshot.DpiScale=selected.Scale;
            IntPtr foreground=GetForegroundWindow();
            EnumWindows((h,p)=> {
                if(h==Handle || !IsWindowVisible(h) || IsIconic(h))return true;
                GetWindowThreadProcessId(h,out uint id); if(id==processId)return true;
                DwmInt(h,14,out int cloaked,4); if(cloaked!=0)return true;
                var cls=new StringBuilder(128); GetClassName(h,cls,128); string name=cls.ToString();
                if(name=="Progman" || name=="WorkerW" || name=="Shell_TrayWnd" || name=="Shell_SecondaryTrayWnd")return true;
                if(DwmGetWindowAttribute(h,9,out RECT r,Marshal.SizeOf<RECT>())!=0)GetWindowRect(h,out r);
                var b=r.Box;
                if(b.Right<=snapshot.Monitor.Left||b.Left>=snapshot.Monitor.Right||b.Bottom<=snapshot.Monitor.Top||b.Top>=snapshot.Monitor.Bottom)return true;
                if(h==foreground && b.Left<=snapshot.Monitor.Left && b.Top<=snapshot.Monitor.Top && b.Right>=snapshot.Monitor.Right && b.Bottom>=snapshot.Monitor.Bottom) snapshot.Fullscreen=true;
                long ex=GetLong(h,-20).ToInt64();
                snapshot.Windows.Add(new DesktopWindow {Id=h.ToInt64(),Bounds=b,Supports=(ex&0x80)==0 && b.Width>100 && b.Height>80}); return true;
            },IntPtr.Zero);
            snapshot.Surfaces=SurfaceNavigator.Build(snapshot,windows); return snapshot;
        }
        IntPtr WindowMessage(IntPtr h,uint message,IntPtr w,IntPtr l)
        {
            if(message==taskbarCreated) {Shell_NotifyIcon(0,ref tray);return IntPtr.Zero;}
            if(message==0x21 && !settingsOpen)return new IntPtr(3); // MA_NOACTIVATE
            if(message==0x8001)
            {
                int m=l.ToInt32()&0xffff;
                if(m==0x205 || m==0x203) MenuAction?.Invoke(m==0x203?1:0);
                return IntPtr.Zero;
            }
            return CallWindowProc(previousProc,h,message,w,l);
        }
        public void ShowMenu(bool paused)
        {
            if(!Native) {MenuAction?.Invoke(1);return;}
            var menu=CreatePopupMenu();
            AppendMenu(menu,0,new UIntPtr(1),"Ayarlar / Settings");
            AppendMenu(menu,0,new UIntPtr(2),paused?"Devam et":"Davranislari duraklat");
            AppendMenu(menu,0,new UIntPtr(3),"Mika'yi geri getir");
            AppendMenu(menu,0,new UIntPtr(4),"Cikis");
            GetCursorPos(out POINT cursor); SetForegroundWindow(Handle);
            uint choice=TrackPopupMenu(menu,0x100|0x2,cursor.X,cursor.Y,0,Handle,IntPtr.Zero);
            DestroyMenu(menu); if(choice!=0)MenuAction?.Invoke((int)choice);
        }
        public void ShowNotification(string title,string message)
        {
            if(!Native)return;
            tray.Flags=1|2|4|0x10; // NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_INFO
            tray.InfoTitle=Shorten(title,63);tray.Info=Shorten(message,255);
            tray.Timeout=5000;tray.InfoFlags=0;
            Shell_NotifyIcon(1,ref tray); // NIM_MODIFY
            tray.Flags=1|2|4;
        }
        static string Shorten(string value,int limit)
        {
            value=(value??"").Replace('\r',' ').Replace('\n',' ').Trim();
            return value.Length>limit?value.Substring(0,limit):value;
        }
        public void Dispose()
        {
            if(!Native)return;
            if(Handle==IntPtr.Zero) {if(previousDpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(previousDpi);return;}
            ReleaseCapture(); Shell_NotifyIcon(2,ref tray);
            if(previousProc!=IntPtr.Zero)SetLong(Handle,-4,previousProc);
            SetLong(Handle,-16,originalStyle); SetLong(Handle,-20,originalEx);
            if(previousDpi!=IntPtr.Zero)SetThreadDpiAwarenessContext(previousDpi);
        }
    }
}
