// The native desktop adapter has no dependency on rendering. These narrow shims
// let the exact production Win32 implementation run in the standalone harness.
namespace UnityEngine
{
    public enum RuntimePlatform {WindowsPlayer}
    public static class Application {public static bool isEditor=false,isBatchMode=false;public static RuntimePlatform platform=RuntimePlatform.WindowsPlayer;}
    public struct Vector3 {public float x,y,z;}
    public static class Input {public static Vector3 mousePosition;public static bool GetMouseButton(int key)=>false;}
    public static class Screen {public static int width=1920,height=1080;}
}
