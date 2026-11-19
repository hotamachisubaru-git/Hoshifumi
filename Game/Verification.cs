namespace Hoshifumi;

public static class Verification
{
    static void Require(bool condition,string detail)
    {
        if(!condition)throw new InvalidDataException(detail);
    }
    static void CloseText(Adventure game)
    {
        for(int i=0;i<10 && game.Mode==Screen.Text;i++)game.Confirm();
    }
    static void Win(Adventure game)
    {
        int turns=0;
        while(game.Mode==Screen.Battle)
        {
            var e=game.Enemy!; int action;
            bool canFinish=game.P.Ink>=2 && game.P.Power*2+3>=e.Hp;
            if(canFinish)action=1;
            else if(game.P.Hp<=e.Attack*2 && game.P.Sweets>0)action=3;
            else if(e.Next==Intent.Heavy)action=2;
            else if(game.P.Ink>=2)action=1;
            else if(e.Next==Intent.Armor)action=2;
            else action=0;
            game.Act(action);CloseText(game);
            Require(++turns<80,"Battle failed to terminate");
        }
    }
    // Travel through normal movement and encounter handling so a sealed or unreachable road is detected.
    public static void Visit(Adventure game,char goal,bool leaveFightOpen=false)
    {
        (int x,int y) target=default;
        bool found=false;
        for(int y=0;y<11;y++)for(int x=0;x<16;x++)
            if(game.Tile(x,y)==goal){target=(x,y);found=true;}
        Require(found,$"Missing {goal} in {game.C.Title}");
        var directions=new (int x,int y)[]{(0,-1),(-1,0),(1,0),(0,1)};
        for(int attempt=0;attempt<150;attempt++)
        {
            var start=(x:game.P.X,y:game.P.Y);
            if(start==target)return;
            var queue=new Queue<(int x,int y)>();
            var prev=new Dictionary<(int,int),(int x,int y)>{{start,start}};
            queue.Enqueue(start);
            while(queue.Count>0&&!prev.ContainsKey(target)){
                var u=queue.Dequeue();
                foreach(var d in directions){
                    var v=(u.x+d.x,u.y+d.y);char tile=game.Tile(v.Item1,v.Item2);
                    if(tile=='#'||(tile=='B'&&!game.P.Address&&goal!='B')||!prev.TryAdd(v,u))continue;
                    queue.Enqueue(v);
                }
            }
            Require(prev.ContainsKey(target),$"No route to {goal} in {game.C.Title}");
            var next=target;
            while(prev[next]!=start)next=prev[next];
            game.Move(next.x-start.x,next.y-start.y);
            if(game.Mode==Screen.Battle)
            {
                if(leaveFightOpen && next==target)return;
                Win(game);
            }
            else Require(game.Mode==Screen.Field,$"Unexpected event on road to {goal}");
        }
        throw new InvalidDataException($"Stuck en route to {goal}");
    }
    public static void Run()
    {
        var chapters=ContentFiles.Read<Chapter[]>("chapters.json");
        var scores=ContentFiles.Read<Score[]>("Music/score.json");
        Require(chapters.Length==7 && scores.Length==11,"Campaign content counts");
        foreach(var c in chapters){
            Require(c.Map.Length==11 && c.Map.All(r=>r.Length==16),"Map dimensions");
            foreach(char marker in "PNRBMT")Require(c.Map.Sum(r=>r.Count(ch=>ch==marker))==1,$"Map marker {marker}");
        }
        foreach(var s in scores){
            var data=Chip.Render(s);Require(data.Length>Chip.Rate*10 && data.Any(x=>x!=0),"Audio "+s.Name);
        }
        string dir=Path.Combine(Path.GetTempPath(),"Hoshifumi-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            for(int ending=0;ending<2;ending++)
            {
                var slot=new SaveSlot(Path.Combine(dir,$"route{ending}.json"));
                var game=new Adventure(chapters,slot);game.Confirm();CloseText(game);
                for(int i=0;i<chapters.Length;i++)
                {
                    Require(game.P.Chapter==i,"Chapter sequence");
                    Visit(game,'R');game.Confirm();Require(game.Page==game.C.Clue,"Sign text");CloseText(game);
                    Visit(game,'N');game.Confirm();CloseText(game);
                    if(game.C.Key==0)game.Navigate(1);
                    game.Confirm();CloseText(game);Require(!game.P.Address,"Incorrect answer accepted");
                    game.Confirm();CloseText(game);game.Navigate(game.C.Key);
                    game.Confirm();CloseText(game);Require(game.P.Address,"Address not granted");
                    if(ending==0){Visit(game,'T');game.Confirm();CloseText(game);}
                    while(ending==0 && game.C.Map.SelectMany((r,y)=>r.Select((_,x)=>game.Tile(x,y))).Contains('E'))Visit(game,'E');
                    Visit(game,'P');game.Confirm();CloseText(game);
                    var resumed=new Adventure(chapters,slot);resumed.Resume();
                    Require(resumed.P.Chapter==i&&resumed.P.Address&&resumed.P.X==game.P.X&&
                        resumed.P.Used.SetEquals(game.P.Used),"Save round-trip");
                    game=resumed;
                    Visit(game,'B',true);Require(game.Enemy?.Boss==true,"Guardian encounter");
                    if(i==0 && ending==0){
                        for(int n=0;n<128 && game.Mode==Screen.Battle;n++){game.Act(2);CloseText(game);}
                        Require(!game.P.BossClear&&game.P.Hp==game.P.MaxHp&&game.Tile(game.P.X,game.P.Y)=='P',"Defeat recovery");
                        Visit(game,'B',true);
                    }
                    Win(game);Require(game.P.BossClear,$"Failed to clear guardian {game.C.Boss}");
                    Visit(game,'M');game.Confirm();CloseText(game);
                }
                Require(game.Mode==Screen.Choice,"Missing final choice");game.Navigate(ending);game.Confirm();
                Require(game.Mode==Screen.Ending && game.P.Ending==ending,"Ending mismatch");
                game=new Adventure(chapters,slot);game.Resume();
                Require(game.Mode==Screen.Ending && game.P.Ending==ending,"Ending save");
            }
            Console.WriteLine("PASS: 7 maps, 14 campaign chapter clears, both endings, defeat/retry, save/resume, 11 audio scores.");
        }finally{Directory.Delete(dir,true);}
    }
}
