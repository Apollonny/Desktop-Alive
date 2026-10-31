using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mika.Editor
{
    public static class BuildMika
    {
        public static void Build()
        {
            PlayerSettings.companyName="Mika";PlayerSettings.productName="MIKA LIVES HERE";
            PlayerSettings.defaultIsNativeResolution=true;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.runInBackground=true;PlayerSettings.resizableWindow=false;
            PlayerSettings.allowUnsafeCode=true;PlayerSettings.useFlipModelSwapchain=false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            PlayerSettings.colorSpace=ColorSpace.Gamma;
            var audio=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset")[0]);
            audio.FindProperty("m_DisableAudio").boolValue=true;audio.ApplyModifiedPropertiesWithoutUndo();
            var shaders=AssetDatabase.FindAssets("t:Shader",new[]{"Assets/Live2D"}).Select(g=>AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(g))).Where(s=>s!=null).ToArray();
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var always=graphics.FindProperty("m_AlwaysIncludedShaders");
            foreach(var shader in shaders)
            {
                bool exists=false;for(int i=0;i<always.arraySize;i++)if(always.GetArrayElementAtIndex(i).objectReferenceValue==shader)exists=true;
                if(!exists){int n=always.arraySize;always.InsertArrayElementAtIndex(n);always.GetArrayElementAtIndex(n).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
            Directory.CreateDirectory("Assets/Mika/Scenes");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Desktop camera").AddComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=0;
            EditorSceneManager.SaveScene(scene,"Assets/Mika/Scenes/Desktop.unity");
            AssetDatabase.SaveAssets();
            CoreChecks.Run();
            GrabSwayChecks.Run();
            LandingChecks.Run();
            CollarRigChecks.Run();
            AnimationChecks.Run(JsonUtility.FromJson<Mika.Core.ModelAnimationProfile>(File.ReadAllText("Assets/StreamingAssets/Mika/animation-profile.json")));
            string output=Path.GetFullPath("../Builds/Windows/Mika.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Mika/Scenes/Desktop.unity"},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            Debug.Log("MIKA_BUILD "+report.summary.result+" errors="+report.summary.totalErrors);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Mika build failed");
        }
    }
}
