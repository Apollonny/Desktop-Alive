using System;
namespace Mika.Core
{
    [Serializable] public sealed class MediaSession { public string id; public bool playing; }
    [Serializable] public sealed class MediaFrame { public MediaSession[] sessions=new MediaSession[0]; public string error=""; }
    public interface IMediaSessionSource { MediaFrame Latest { get; } bool Connected { get; } }
    public sealed class MediaPolicy
    {
        float playingFor;
        public bool Tick(float delta,MediaFrame frame,bool connected,MikaSettings settings)
        {
            bool playing=false;
            if(connected && settings.musicReactions && frame!=null && string.IsNullOrEmpty(frame.error))
                foreach(var session in frame.sessions??new MediaSession[0])
                    foreach(var allowed in settings.mediaSources??new string[0])
                        if(session.playing && Matches(session.id,allowed))playing=true;
            playingFor=playing?playingFor+delta:0;
            return playingFor>=2;
        }
        public static bool Matches(string source,string allowed)
        {
            if(string.IsNullOrEmpty(source)||string.IsNullOrEmpty(allowed))return false;
            if(allowed=="Spotify")return source.Equals("Spotify.exe",StringComparison.OrdinalIgnoreCase)
                ||source.Equals("Spotify",StringComparison.OrdinalIgnoreCase)
                ||source.StartsWith("SpotifyAB.SpotifyMusic_",StringComparison.OrdinalIgnoreCase);
            return source.Equals(allowed,StringComparison.OrdinalIgnoreCase);
        }
    }
}
