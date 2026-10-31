using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Mika.Core;
using UnityEngine;

namespace Mika.Runtime
{
    public static class MikaLandingReview
    {
        public static IEnumerator Export(MikaAnimationDriver actor,string folder,Action<string> fail)
        {
            Directory.CreateDirectory(folder);
            var csv=new StringBuilder("case,frame,state,speed,strength,bodyY,headY\n");
            var result=new StringBuilder();var subtitles=new StringBuilder();
            float previousPeak=0;bool passed=true;int number=0;
            foreach(int distance in new[]{20,180,360})
            {
                var scenario=new AnimationScenario(actor.Profile);scenario.RemoveWindow();
                scenario.Controller.Changed+=actor.SetState;
                actor.SetState(MikaState.Idle);actor.Seek(0);actor.SetExpression("");
                actor.ConfigurePreviewPhysics(false,1/30f);
                bool landed=false,recovered=false;float peak=0,impact=0,strength=0;
                for(int frame=0;frame<120;frame++)
                {
                    if(frame==10)scenario.Grab(660-distance);
                    if(frame==30)scenario.Controller.Release();
                    scenario.Tick(1/30f);
                    actor.SetExpression(scenario.Controller.Expression);
                    actor.SetGrabPosition(scenario.Controller.Feet.X,240);
                    actor.SetLandingStrength(scenario.Controller.LandingStrength);
                    actor.Step(1/30f,Vector2.zero,false,1,false);
                    yield return null;
                    actor.CheckMotionOwnership();
                    float body=actor.Model.Parameters.First(p=>p.Id=="ParamBodyAngleY").Value;
                    float head=actor.Model.Parameters.First(p=>p.Id=="ParamAngleY").Value;
                    if(scenario.Controller.State==MikaState.Land)
                    {
                        landed=true;peak=Mathf.Max(peak,-body);
                        impact=scenario.Controller.LandingSpeed;strength=scenario.Controller.LandingStrength;
                    }
                    recovered|=scenario.Controller.State==MikaState.Recover;
                    csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2},{3:F4},{4:F4},{5:F4},{6:F4}",distance,frame,scenario.Controller.State,scenario.Controller.LandingSpeed,scenario.Controller.LandingStrength,body,head));
                    actor.SaveScenarioFrame(Path.Combine(folder,$"frame-{number*120+frame:D4}.png"),scenario);
                }
                bool valid=landed&&recovered&&scenario.Controller.State==MikaState.Idle&&scenario.Controller.Support?.Owner==-1&&peak>previousPeak;
                passed&=valid;previousPeak=peak;
                result.AppendLine(string.Format(CultureInfo.InvariantCulture,"drop={0}px speed={1:F2} strength={2:F3} peakBodyCompression={3:F3} passed={4}",distance,impact,strength,peak,valid));
                subtitles.AppendLine((number+1).ToString()).AppendLine($"00:00:{number*4:D2},000 --> 00:00:{(number+1)*4:D2},000").AppendLine(string.Format(CultureInfo.InvariantCulture,"Drop {0}px | Impact {1:F0}px/s | Strength {2:F2} | collar rig pending",distance,impact,strength)).AppendLine();
                number++;
            }
            File.WriteAllText(Path.Combine(folder,"parameters.csv"),csv.ToString());
            File.WriteAllText(Path.Combine(folder,"review.srt"),subtitles.ToString());
            result.AppendLine("passed="+passed);File.WriteAllText(Path.Combine(folder,"result.txt"),result.ToString());
            if(!passed)fail("Landing review failed: "+result);else Debug.Log("MIKA_LANDING_REVIEW passed 360 frames, three drops, increasing compression, recover and ownership");
        }
    }
}
