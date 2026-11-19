using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hoshifumi;

public sealed record Chapter(string Title,string Place,string Recipient,string Foe,string Boss,string Blow,
    string Opening,string Arrival,string QuestionIntro,string Clue,string Question,string A,string B,
    int Key,string Correct,string Delivered,string[] Map);

public static class ContentFiles
{
    public static readonly string Root=Path.Combine(AppContext.BaseDirectory,"Assets");
    public static T Read<T>(string name) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root,name)),
        new JsonSerializerOptions{PropertyNameCaseInsensitive=true}) ?? throw new InvalidDataException(name);
}
public sealed class Progress
{
    public int Version {get;set;}=1;
    public int Chapter {get;set;}
    public int X {get;set;}
    public int Y {get;set;}
    public int Xp {get;set;}
    public int Hp {get;set;}=36;
    public int Ink {get;set;}=3;
    public int Sweets {get;set;}=2;
    public bool Address {get;set;}
    public bool BossClear {get;set;}
    public int Ending {get;set;}=-1;
    public HashSet<int> Used {get;set;}=[];
    [JsonIgnore] public int Level=>1+Xp/12;
    [JsonIgnore] public int MaxHp=>30+Level*6;
    [JsonIgnore] public int Power=>6+Level*2;
    public void Rest(){Hp=MaxHp; Ink=5;}
}
public sealed class SaveSlot(string path)
{
    public Progress? Load(Chapter[] chapters)
    {
        try
        {
            if(!File.Exists(path))return null;
            var p=JsonSerializer.Deserialize<Progress>(File.ReadAllText(path));
            if(p==null || p.Version!=1 || p.Chapter<0 || p.Chapter>=chapters.Length ||
                p.Y<0 || p.Y>=11 || p.X<0 || p.X>=16 || chapters[p.Chapter].Map[p.Y][p.X]=='#' ||
                p.Used==null || p.Xp is <0 or >1000 || p.Hp<1 || p.Hp>p.MaxHp ||
                p.Ink is <0 or >5 || p.Sweets is <0 or >99 || p.Ending is < -1 or >1)return null;
            return p;
        }
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){return null;}
    }
    public bool Write(Progress data)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            string temp=path+".tmp";
            File.WriteAllText(temp,JsonSerializer.Serialize(data));
            File.Move(temp,path,true);
            return true;
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException){return false;}
    }
}

public enum Screen {Title,Field,Text,Choice,Battle,Pause,Ending}
public enum Intent {Strike,Heavy,Armor,Drain}
public sealed class Fight(int chapter,bool boss,int id)
{
    static readonly string[] Patterns=["nnh","nahn","hnna","ndnh","anhn","ahnnh","adnhh"];
    public readonly bool Boss=boss;
    public readonly int Id=id;
    public int MaxHp {get;}=boss?38+chapter*14:15+chapter*6;
    public int Hp {get;set;}=boss?38+chapter*14:15+chapter*6;
    public int Turn {get;set;}
    public int Attack=>Boss?6+chapter*2:4+chapter;
    public Intent Next
    {
        get{
            var pattern=Boss?Patterns[chapter]:"nnh";
            return pattern[Turn%pattern.Length] switch {'h'=>Intent.Heavy,'a'=>Intent.Armor,'d'=>Intent.Drain,_=>Intent.Strike};
        }
    }
}

public sealed class Adventure
{
    public readonly Chapter[] Chapters;
    readonly SaveSlot slot;
    public Progress P {get;private set;}=new();
    public Chapter C=>Chapters[P.Chapter];
    public Screen Mode {get;private set;}=Screen.Title;
    public Screen Backdrop {get;private set;}=Screen.Field;
    public Fight? Enemy {get;private set;}
    public bool HasSave {get;private set;}
    public bool SaveError {get;private set;}
    public int Cursor {get;private set;}
    public int FaceX {get;private set;}=0;
    public int FaceY {get;private set;}=1;
    public string Caption {get;private set;}="";
    public string Page {get;private set;}="";
    public string[] Options {get;private set;}=[];
    public string Cue {get;private set;}="";
    readonly Queue<string> pages=new();
    Action? afterText;
    Action<int>? answer;
    public Adventure(Chapter[] chapters, SaveSlot slot)
    {
        Chapters=chapters;this.slot=slot;HasSave=slot.Load(chapters)!=null;
    }
    public string Music => (Mode is Screen.Text or Screen.Choice ? Backdrop : Mode) switch {
        Screen.Title=>"title",Screen.Battle=>Enemy?.Boss==true?"boss":"battle",Screen.Ending=>"ending",_=>$"field{P.Chapter+1}"};
    public string TakeCue(){var c=Cue;Cue="";return c;}
    public void Save(){SaveError=!slot.Write(P);if(!SaveError)HasSave=true;}
    public void NewGame(){P=new();EnterChapter(0);}
    public void Resume()
    {
        var loaded=slot.Load(Chapters);
        if(loaded==null){HasSave=false;Dialog(Screen.Title,"記録",["記録を読み込めませんでした。"],Title);return;}
        P=loaded;Enemy=null;Cursor=0;Mode=P.Ending>=0?Screen.Ending:Screen.Field;
    }
    void Title(){Mode=Screen.Title;Cursor=0;Enemy=null;}
    void EnterChapter(int chapter)
    {
        P.Chapter=chapter;P.Address=false;P.BossClear=false;P.Used.Clear();P.Rest();
        P.Sweets=Math.Max(2,P.Sweets);AtPost();Save();Cue="stamp";
        Dialog(Screen.Field,$"第{chapter+1}章　{C.Title}",[C.Opening,C.Arrival],Field);
    }
    void AtPost()
    {
        P.Y=Array.FindIndex(C.Map,r=>r.Contains('P'));P.X=C.Map[P.Y].IndexOf('P');
        FaceX=0;FaceY=1;
    }
    void Field(){Mode=Screen.Field;Cursor=0;Enemy=null;}
    void Dialog(Screen background,string caption,IEnumerable<string> lines,Action next)
    {
        Backdrop=background;Mode=Screen.Text;Caption=caption;pages.Clear();
        foreach(string s in lines)pages.Enqueue(s);
        Page=pages.Dequeue();afterText=next;
    }
    void Choice(Screen background,string caption,string question,string[] options,Action<int> select)
    {
        Backdrop=background;Mode=Screen.Choice;Caption=caption;Page=question;
        Options=options;answer=select;Cursor=0;
    }
    public void Navigate(int delta)
    {
        int count=Mode switch {Screen.Title=>(HasSave?2:1),Screen.Pause=>3,Screen.Battle=>4,Screen.Choice=>Options.Length,_=>0};
        if(count>0){Cursor=(Cursor+count+delta)%count;Cue="tap";}
    }
    public void Cancel()
    {
        if(Mode==Screen.Field){Save();Mode=Screen.Pause;Cursor=0;Cue="tap";}
        else if(Mode==Screen.Pause)Field();
        else if(Mode==Screen.Title)Cursor=0;
    }
    public void Confirm()
    {
        Cue="tap";
        switch(Mode)
        {
            case Screen.Title:
                if(Cursor==0 && HasSave){Resume();break;}
                if(Cursor==1 && HasSave){
                    Choice(Screen.Title,"新しい配達","今の記録を上書きしますか？",["もどる","はじめる"],
                        i=>{if(i==1)NewGame();else Title();});break;}
                NewGame();break;
            case Screen.Field: Interact();break;
            case Screen.Text: if(pages.Count>0)Page=pages.Dequeue();else afterText?.Invoke();break;
            case Screen.Choice: answer?.Invoke(Cursor);break;
            case Screen.Battle: Act(Cursor);break;
            case Screen.Pause:
                if(Cursor==0)Field();
                else if(Cursor==1)Dialog(Screen.Field,"配達手帳",[Objective(),$"宛先　{C.Recipient}","案内人と立札を調べよう。守り手を倒したら赤いポストへ。局舎では体力とインクを回復できる。"],Field);
                else {Save();Title();}break;
            case Screen.Ending:Title();break;
        }
    }
    public string Objective()=>!P.Address?"案内人に宛先をたずねよう":!P.BossClear?"守り手の封印を解こう":"赤いポストに投函しよう";
    public char Tile(int x,int y)
    {
        if(x<0||x>=16||y<0||y>=11)return '#';
        char ch=C.Map[y][x];
        return (ch=='B'&&P.BossClear) || (ch is 'E' or 'T' && P.Used.Contains(y*16+x)) ? '.' : ch;
    }
    public void Move(int dx,int dy)
    {
        if(Mode!=Screen.Field||Math.Abs(dx)+Math.Abs(dy)!=1)return;
        FaceX=dx;FaceY=dy;int x=P.X+dx,y=P.Y+dy;
        char ch=Tile(x,y);if(ch=='#')return;
        if(ch is 'E' or 'B'){Touch(ch,x,y);return;}
        P.X=x;P.Y=y;
    }
    public void Interact()
    {
        char ch=Tile(P.X,P.Y);
        if(ch is 'N' or 'R' or 'M' or 'P' or 'T')Touch(ch,P.X,P.Y);
        else Touch(Tile(P.X+FaceX,P.Y+FaceY),P.X+FaceX,P.Y+FaceY);
    }
    void Touch(char ch,int x,int y)
    {
        switch(ch)
        {
            case 'P':P.Rest();Save();Cue="heal";Dialog(Screen.Field,"出張郵便局",["お茶と休憩。体力とインクが全快した。"],Field);break;
            case 'R':Dialog(Screen.Field,"古い立札",[C.Clue],Field);break;
            case 'T':
                P.Used.Add(y*16+x);P.Sweets=Math.Min(99,P.Sweets+2);Save();Cue="stamp";
                Dialog(Screen.Field,"補給箱",["星あめを二つ見つけた。戦闘中に食べると体力が回復する。"],Field);break;
            case 'N':
                if(P.Address){Dialog(Screen.Field,C.Recipient,[C.Correct],Field);break;}
                Dialog(Screen.Field,C.Recipient,[C.QuestionIntro],()=>Choice(Screen.Field,"宛先の照合",C.Question,[C.A,C.B],i=>{
                    if(i==C.Key){P.Address=true;Cue="stamp";Save();Dialog(Screen.Field,C.Recipient,[C.Correct],Field);}
                    else Dialog(Screen.Field,C.Recipient,["まだ思い出せない……。立札の言葉を確かめて、もう一度話そう。"],Field);
                }));break;
            case 'B' when !P.Address:
                Dialog(Screen.Field,"封印",["宛先が欠けている。先に案内人に会いに行こう。"],Field);break;
            case 'E':case 'B':
                Enemy=new Fight(P.Chapter,ch=='B',y*16+x);Cursor=0;Mode=Screen.Battle;break;
            case 'M':
                if(!P.BossClear){Dialog(Screen.Field,"星のポスト",["投函口には黒い封印。"+Objective()+"。"],Field);break;}
                Cue="stamp";
                Dialog(Screen.Field,"配達完了",[C.Delivered],()=>{
                    if(P.Chapter+1<Chapters.Length)EnterChapter(P.Chapter+1);
                    else Choice(Screen.Field,"七通目の追伸","最後の一行を、だれに書こう？",["父に書く","七つの町に書く"],i=>{
                        P.Ending=i;Save();Mode=Screen.Ending;Cursor=0;Cue="win";});});break;
        }
    }
    public void Act(int command)
    {
        if(Mode!=Screen.Battle || Enemy==null)return;
        var e=Enemy;Intent intent=e.Next;
        string message;
        int attack=P.Power;
        switch(command)
        {
            case 0:
                int hit=intent==Intent.Armor?Math.Max(1,attack/3):attack;
                e.Hp-=hit;message=$"ペン先の一撃！ {hit} ダメージ。";Cue="hit";break;
            case 1:
                if(P.Ink<2){Notice("インクが足りない。守ると2回復する。");return;}
                P.Ink-=2;int power=attack*2+3;e.Hp-=power;message=$"星印が光る！ {power} ダメージ。";Cue="stamp";break;
            case 2:
                P.Ink=Math.Min(5,P.Ink+2);message="手紙をかばった。インクが戻る。";Cue="guard";break;
            case 3:
                if(P.Sweets==0){Notice("星あめを持っていない。");return;}
                P.Sweets--;int before=P.Hp;P.Hp=Math.Min(P.MaxHp,P.Hp+P.MaxHp/2+4);
                message=$"星あめで体力が {P.Hp-before} 回復。";Cue="heal";break;
            default:return;
        }
        if(e.Hp<=0){
            int level=P.Level;int xp=e.Boss?12:5;P.Xp+=xp;
            if(e.Boss)P.BossClear=true;else P.Used.Add(e.Id);
            if(level!=P.Level)P.Rest();
            Save();Cue="win";
            Dialog(Screen.Battle,"勝利",[message,$"経験値 {xp} を獲得。"+(level!=P.Level?$"レベル {P.Level}！ 体力とインクが全快。":"影は消えた。")],Field);
            return;
        }
        int damage=intent switch {Intent.Heavy=>e.Attack*2,Intent.Armor=>Math.Max(1,e.Attack/2),_=>e.Attack};
        if(command==2)damage=Math.Max(1,damage/4);
        P.Hp=Math.Max(0,P.Hp-damage);
        if(intent==Intent.Drain)e.Hp=Math.Min(e.MaxHp,e.Hp+damage/2);
        e.Turn++;
        string result=$"{IntentName(intent)}！ {damage} ダメージ。"+(intent==Intent.Drain?"敵が体力を吸収。":"");
        if(P.Hp<=0){
            Dialog(Screen.Battle,"配達は続く",[message+" "+result,"局の明かりが見える。見つけた宛先は、かばんに残っていた。"],()=>{
                AtPost();P.Rest();P.Sweets=Math.Max(1,P.Sweets);Save();Field();});return;
        }
        Notice(message+" "+result);
    }
    public string IntentName(Intent intent)=>intent switch{Intent.Heavy=>Enemy?.Boss==true?C.Blow:"大振り",
        Intent.Armor=>"硬い構え",Intent.Drain=>"吸い取り",_=>"体当たり"};
    void Notice(string text)=>Dialog(Screen.Battle,"交戦",[text],()=>{Mode=Screen.Battle;});
}
