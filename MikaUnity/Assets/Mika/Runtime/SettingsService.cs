using System;
using System.IO;
using Mika.Core;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
namespace Mika.Runtime
{
    public sealed class SettingsService
    {
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegCreateKeyEx(IntPtr key,string subkey,int reserved,string cls,int options,int access,IntPtr security,out IntPtr result,out int disposition);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegSetValueEx(IntPtr key,string name,int reserved,int type,byte[] value,int size);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode)] static extern int RegDeleteValue(IntPtr key,string name);
        [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
        public MikaSettings Value {get;private set;}
        readonly string path=Path.Combine(Application.persistentDataPath,"settings.json");
        public SettingsService()
        {
            try
            {
                string json=File.Exists(path)?File.ReadAllText(path):null;
                Value=json!=null?JsonUtility.FromJson<MikaSettings>(json):new MikaSettings();
                // Existing installs predate reminders; keep the new feature enabled after migration.
                if(json!=null&&!json.Contains("\"remindersEnabled\""))Value.remindersEnabled=true;
            }
            catch(Exception e) {Debug.LogWarning("Settings reset: "+e.Message);}
            if(Value==null)Value=new MikaSettings();
            Value.characterHeight=Mathf.Clamp(Value.characterHeight,160,640);
            Value.fps=Value.fps==60?60:30;
            Value.volume=Mathf.Clamp01(Value.volume);
            if(Value.mediaSources==null)Value.mediaSources=new[]{"Spotify"};
        }
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp=path+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(Value,true));
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public void SetStartup(bool enabled)
        {
            if(Application.platform!=RuntimePlatform.WindowsPlayer)return;
            int result=RegCreateKeyEx(new IntPtr(unchecked((int)0x80000001)),@"Software\Microsoft\Windows\CurrentVersion\Run",0,null,0,2,IntPtr.Zero,out var key,out int disposition);
            if(result!=0)throw new IOException("Startup setting failed: "+result);
            try
            {
                if(enabled)
                {
                    var value=Encoding.Unicode.GetBytes("\""+System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName+"\"\0");
                    result=RegSetValueEx(key,"MikaLivesHere",0,1,value,value.Length);
                }
                else {result=RegDeleteValue(key,"MikaLivesHere");if(result==2)result=0;}
                if(result!=0)throw new IOException("Startup setting failed: "+result);
            }
            finally {RegCloseKey(key);}
            Value.startWithWindows=enabled;Save();
        }
    }
}
