using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Mika.Runtime
{
    public static class MikaRigAudit
    {
        [Serializable] public sealed class Probe {public string file,parameter;public float value,facing=1;}
        [Serializable] public sealed class Manifest {public string note="Isolated parameter probes from frozen runtime idle; not authored animations or collar approval";public Probe[] probes;}
        public static IEnumerator Export(MikaAnimationDriver actor,string folder,Action<string> failure)
        {
            Directory.CreateDirectory(folder);
            var probes=new List<Probe> {new Probe {parameter="",value=0},new Probe {parameter="",value=0,facing=-1}};
            var ids=new[]{"ParamBodyAngleX","ParamBodyAngleY","ParamBodyAngleZ","ParamBodyAngleX2","ParamBodyAngleY2","ParamBodyAngleY3","ParamBodyAngleZ2",
                "pose1","pose2","pose3","legspread","legspread2","walk","walk2","walk7","walk8","walk9","walk10","walk11","walk12","walk13","walk14","walk15","walk16"};
            foreach(var id in ids)
            {
                var p=actor.Model.Parameters.First(x=>x.Id==id);
                if(p.MinimumValue!=p.DefaultValue)probes.Add(new Probe {parameter=id,value=p.MinimumValue});
                if(p.MaximumValue!=p.DefaultValue)probes.Add(new Probe {parameter=id,value=p.MaximumValue});
            }
            var selector=actor.Model.Parameters.First(p=>p.Id=="motion1");
            for(int value=(int)selector.MinimumValue;value<=selector.MaximumValue;value++)probes.Add(new Probe {parameter=selector.Id,value=value});
            probes.Add(new Probe {parameter="",value=0});
            for(int i=0;i<probes.Count;i++)
            {
                var p=probes[i];p.file=$"probe-{i:D3}.png";
                try {actor.SetRigProbe(p.parameter,p.value,p.facing);}
                catch(Exception e){failure(e.ToString());Debug.LogException(e);yield break;}
                // Let native evaluation, drawable meshes and clipping masks settle before capture.
                for(int settle=0;settle<8;settle++)
                {
                    actor.SetRigProbe(p.parameter,p.value,p.facing);
                    actor.RenderPreviewFrame();yield return null;
                }
                try {actor.CheckMotionOwnership();actor.SavePortrait(Path.Combine(folder,p.file),false);actor.SaveGeometryEvidence(Path.Combine(folder,p.file.Replace(".png",".geometry.json")));if(i==0||i==16||i==29||i==34)actor.SaveUnmaskedEvidence(Path.Combine(folder,p.file.Replace(".png",".unmasked.png")));}
                catch(Exception e){failure(e.ToString());Debug.LogException(e);yield break;}
            }
            File.WriteAllText(Path.Combine(folder,"manifest.json"),JsonUtility.ToJson(new Manifest {probes=probes.ToArray()},true));
            File.Copy(Path.Combine(Application.persistentDataPath,"model-parameters.tsv"),Path.Combine(folder,"model-parameters.tsv"),true);
            File.Copy(Path.Combine(Application.streamingAssetsPath,"Mika","animation-profile.json"),Path.Combine(folder,"animation-profile.json"),true);
            Debug.Log("MIKA_RIG_AUDIT passed probes="+probes.Count);
        }
    }
}
