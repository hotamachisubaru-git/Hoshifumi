using Hoshifumi;

if(args.Contains("--verify"))
{
    Verification.Run();
    return;
}
if(args.Length==2 && args[0]=="--export-music")
{
    Directory.CreateDirectory(args[1]);
    foreach(var score in ContentFiles.Read<Score[]>("Music/score.json"))
        Chip.WriteWave(Path.Combine(args[1],score.Id+".wav"),Chip.Render(score));
    return;
}
using var game=new PostalGame(args.Length==2 && args[0]=="--capture"?args[1]:null);
game.Run();
