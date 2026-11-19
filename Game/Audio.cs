using System.Text;
using Microsoft.Xna.Framework.Audio;

namespace Hoshifumi;

public sealed record Score(string Id, string Name, int Bpm, int Root, string Mode, double Duty,
    int[] Chords, string[] Phrases, string Bass, string Drums);

public static class Chip
{
    public const int Rate = 22050;
    static int[] Scale(string mode) => mode switch {
        "major" => [0,2,4,5,7,9,11], "dorian" => [0,2,3,5,7,9,10],
        "phrygian" => [0,1,3,5,7,8,10], _ => [0,2,3,5,7,8,10] };

    public static byte[] Render(Score score)
    {
        int[] scale = Scale(score.Mode);
        string bass = score.Bass.Replace(" ", ""), drums = score.Drums.Replace(" ", "");
        double tick = 30.0 / score.Bpm;
        int samples = (int)Math.Round(score.Chords.Length * bass.Length * tick * Rate);
        float[] mix = new float[samples];
        double Frequency(int degree, int transpose) =>
            440 * Math.Pow(2, (score.Root + transpose + scale[degree % 7] + 12 * (degree / 7) - 69) / 12.0);

        void Tone(int step, double length, double freq, double duty, double volume, bool triangle = false)
        {
            int begin = (int)Math.Round(step*tick*Rate);
            int count = Math.Min(samples-begin, (int)(length*tick*Rate));
            for (int i=0; i<count; i++)
            {
                double p = i*freq/Rate % 1;
                double wave = triangle ? 1 - 4*Math.Abs(p-0.5) : (p<duty ? 1 : -1) - (2*duty-1);
                double env = Math.Min(1, i/30.0)*Math.Min(1,(count-1-i)/70.0)*(1-0.36*i/count);
                mix[begin+i] += (float)(wave*env*volume);
            }
        }
        int noiseRegister = 1;
        for (int bar=0; bar<score.Chords.Length; bar++)
        {
            string phrase = score.Phrases[(bar + bar/4) % score.Phrases.Length].Replace(" ", "");
            int chord = score.Chords[bar];
            for (int j=0; j<bass.Length; j++)
            {
                int step = bar*bass.Length+j;
                void Line(string line, int offset, double duty, double volume, bool tri)
                {
                    if (!char.IsAsciiDigit(line[j])) return;
                    int end=j+1;
                    while(end<line.Length && line[end]=='.') end++;
                    Tone(step,(end-j)*0.93,Frequency(chord+line[j]-'0',offset),duty,volume,tri);
                }
                Line(phrase,0,score.Duty,0.22,false);
                Line(bass,-24,0.5,0.28,true);
                if (j%2==1)
                    Tone(step,0.67,Frequency(chord+new[]{0,2,4,2}[(j/2+bar)%4],-12),0.125,0.105);
                char drum=drums[j];
                if (drum=='.') continue;
                int start=(int)Math.Round(step*tick*Rate);
                int count=Math.Min(samples-start,(int)(Rate*(drum=='s'?0.11:0.055)));
                for(int i=0; i<count; i++)
                {
                    if(i%(drum=='k'?17:drum=='s'?4:1)==0)
                        noiseRegister=(noiseRegister>>1)|(((noiseRegister^(noiseRegister>>1))&1)<<14);
                    mix[start+i]+=(float)(((noiseRegister&1)*2-1)*Math.Exp(-4.2*i/count)*(drum=='h'?0.065:0.17));
                }
            }
        }
        return Pcm(mix);
    }
    static byte[] Pcm(float[] mix)
    {
        byte[] bytes=new byte[mix.Length*2];
        for(int i=0;i<mix.Length;i++)
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2*i), (short)(Math.Clamp(mix[i],-1,1)*30000));
        return bytes;
    }
    public static byte[] Blip(int[] notes)
    {
        const int step=1400;
        float[] mix=new float[notes.Length*step];
        for(int n=0;n<notes.Length;n++)
            for(int i=0;i<step;i++)
                mix[n*step+i]=(float)((i*440*Math.Pow(2,(notes[n]-69)/12.0)/Rate%1<0.25?0.34:-0.12)*
                    Math.Min(1,i/15.0)*Math.Min(1,(step-i)/110.0));
        return Pcm(mix);
    }
    public static void WriteWave(string path, byte[] pcm)
    {
        using var w=new BinaryWriter(File.Create(path));
        void Id(string value)=>w.Write(Encoding.ASCII.GetBytes(value));
        Id("RIFF"); w.Write(36+pcm.Length); Id("WAVE"); Id("fmt "); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate*2); w.Write((short)2); w.Write((short)16);
        Id("data"); w.Write(pcm.Length); w.Write(pcm);
    }
}

public sealed class Jukebox : IDisposable
{
    readonly Dictionary<string,Score> scores;
    readonly Dictionary<string,SoundEffect> sounds=new();
    SoundEffectInstance? loop;
    string current="";
    bool unavailable;
    public bool Muted {get; private set;}
    public Jukebox(IEnumerable<Score> tracks) => scores=tracks.ToDictionary(s=>s.Id);
    void PlaySafe(Action action)
    {
        if(unavailable) return;
        try { action(); }
        catch(NoAudioHardwareException) { unavailable=true; }
        catch(DllNotFoundException) { unavailable=true; }
    }
    public void Music(string id)
    {
        if(id==current)return;
        current=id;
        PlaySafe(()=>{
            loop?.Dispose();
            if(!sounds.TryGetValue(id,out var sound))
                sounds[id]=sound=new SoundEffect(Chip.Render(scores[id]),Chip.Rate,AudioChannels.Mono);
            loop=sound.CreateInstance(); loop.IsLooped=true; loop.Volume=Muted?0:0.52f; loop.Play();
        });
    }
    public void Toggle()
    {
        Muted=!Muted;
        if(loop!=null) loop.Volume=Muted?0:0.52f;
    }
    public void Cue(string name)
    {
        if(Muted)return;
        PlaySafe(()=>{
            if(!sounds.TryGetValue(name,out var s))
            {
                int[] notes=name switch {"stamp"=>[67,72,76,79], "hit"=>[51,43], "guard"=>[52,64],
                    "heal"=>[60,64,67], "win"=>[60,64,67,72], _=>[76]};
                sounds[name]=s=new SoundEffect(Chip.Blip(notes),Chip.Rate,AudioChannels.Mono);
            }
            s.Play(0.5f,0,0);
        });
    }
    public void Dispose(){ loop?.Dispose(); foreach(var s in sounds.Values)s.Dispose(); }
}
