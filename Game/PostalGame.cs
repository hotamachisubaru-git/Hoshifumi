using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hoshifumi;

public sealed partial class PostalGame : Microsoft.Xna.Framework.Game
{
    const int W=256,H=240;
    readonly GraphicsDeviceManager graphics;
    readonly Adventure game;
    readonly Jukebox audio;
    readonly string? capture;
    readonly Dictionary<string,Texture2D> textures=[];
    readonly Dictionary<char,int> glyphs=[];
    SpriteBatch batch=null!;
    Texture2D dot=null!;
    RenderTarget2D canvas=null!;
    KeyboardState old;
    GamePadState padOld;
    double cooldown,clock,foot;
    static readonly Color Ink=new(16,16,36), Night=new(25,32,57), Snow=new(255,241,207),
        Amber=new(255,206,115), Sea=new(115,202,190), Rose=new(238,112,96), Shade=new(63,79,111);
    public PostalGame(string? capture)
    {
        this.capture=capture;
        graphics=new GraphicsDeviceManager(this){PreferredBackBufferWidth=W*3,PreferredBackBufferHeight=H*3,
            SynchronizeWithVerticalRetrace=true,HardwareModeSwitch=false};
        IsMouseVisible=false;
        string path=capture!=null?Path.Combine(capture,"capture-save.json"):
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Hoshifumi","save.json");
        game=new(ContentFiles.Read<Chapter[]>("chapters.json"),new SaveSlot(path));
        audio=new(ContentFiles.Read<Score[]>("Music/score.json"));
    }
    protected override void LoadContent()
    {
        // DesktopGL's post-creation title setter sends Japanese text as UTF-8 on Windows.
        Window.Title="星ふみ郵便局 | HOSHIFUMI";
        batch=new(GraphicsDevice);dot=new(GraphicsDevice,1,1);dot.SetData(new[]{Color.White});
        canvas=new(GraphicsDevice,W,H,false,SurfaceFormat.Color,DepthFormat.None,0,RenderTargetUsage.PreserveContents);
        string dir=Path.Combine(ContentFiles.Root,"Graphics");
        foreach(var file in Directory.EnumerateFiles(dir,"*.png"))
        {
            using var stream=File.OpenRead(file);textures[Path.GetFileNameWithoutExtension(file)]=Texture2D.FromStream(GraphicsDevice,stream);
        }
        string chars=File.ReadAllText(Path.Combine(dir,"glyphs.txt")).TrimEnd('\r','\n');
        for(int i=0;i<chars.Length;i++)glyphs[chars[i]]=i;
        audio.Music(game.Music);
    }
    protected override void UnloadContent()
    {
        foreach(var t in textures.Values)t.Dispose();
        canvas.Dispose();dot.Dispose();batch.Dispose();audio.Dispose();
    }
    protected override void Update(GameTime time)
    {
        clock+=time.ElapsedGameTime.TotalSeconds;
        cooldown-=time.ElapsedGameTime.TotalSeconds;
        foot=Math.Max(0,foot-time.ElapsedGameTime.TotalSeconds);
        var keys=Keyboard.GetState();var pad=GamePad.GetState(PlayerIndex.One);
        bool Key(Keys k)=>keys.IsKeyDown(k)&&old.IsKeyUp(k);
        bool Btn(Buttons b)=>pad.IsButtonDown(b)&&padOld.IsButtonUp(b);
        if(IsActive && capture==null)
        {
            if(Key(Keys.F11)){
                bool full=!graphics.IsFullScreen;
                graphics.PreferredBackBufferWidth=full?GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width:W*3;
                graphics.PreferredBackBufferHeight=full?GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height:H*3;
                graphics.IsFullScreen=full;graphics.ApplyChanges();
            }
            if(Key(Keys.M))audio.Toggle();
            int dx=0,dy=0;
            bool confirm=Key(Keys.Z)||Key(Keys.Enter)||Key(Keys.Space)||Btn(Buttons.A);
            bool cancel=Key(Keys.X)||Key(Keys.Escape)||Btn(Buttons.B)||Btn(Buttons.Start);
            if(confirm)game.Confirm();
            else if(cancel)game.Cancel();
            else if(game.Mode==Screen.Field)
            {
                if(keys.IsKeyDown(Keys.Left)||keys.IsKeyDown(Keys.A)||pad.IsButtonDown(Buttons.DPadLeft))dx=-1;
                else if(keys.IsKeyDown(Keys.Right)||keys.IsKeyDown(Keys.D)||pad.IsButtonDown(Buttons.DPadRight))dx=1;
                else if(keys.IsKeyDown(Keys.Up)||keys.IsKeyDown(Keys.W)||pad.IsButtonDown(Buttons.DPadUp))dy=-1;
                else if(keys.IsKeyDown(Keys.Down)||keys.IsKeyDown(Keys.S)||pad.IsButtonDown(Buttons.DPadDown))dy=1;
                if(dx==0&&dy==0)cooldown=0;
                else if(cooldown<=0){
                    int x=game.P.X,y=game.P.Y;game.Move(dx,dy);cooldown=0.14;
                    if(x!=game.P.X||y!=game.P.Y)foot=0.08;
                }
            }
            else{
                int vertical=game.Mode==Screen.Battle?2:1;
                if(Key(Keys.Up)||Key(Keys.W)||Btn(Buttons.DPadUp))game.Navigate(-vertical);
                else if(Key(Keys.Down)||Key(Keys.S)||Btn(Buttons.DPadDown))game.Navigate(vertical);
                else if(Key(Keys.Left)||Key(Keys.A)||Btn(Buttons.DPadLeft))game.Navigate(-1);
                else if(Key(Keys.Right)||Key(Keys.D)||Btn(Buttons.DPadRight))game.Navigate(1);
            }
        }
        old=keys;padOld=pad;
        string cue=game.TakeCue();audio.Music(game.Music);if(cue.Length>0)audio.Cue(cue);
        base.Update(time);
    }
    void Rect(int x,int y,int w,int h,Color color)
    {if(w>0 && h>0)batch.Draw(dot,new Rectangle(x,y,w,h),color);}
    void Frame(int x,int y,int w,int h)
    {
        Rect(x,y,w,h,Ink);Rect(x+1,y+1,w-2,h-2,Shade);Rect(x+2,y+2,w-4,h-4,Night);
        foreach(int xx in new[]{x+1,x+w-3})foreach(int yy in new[]{y+1,y+h-3})Rect(xx,yy,2,2,Amber);
    }
    void Pic(string key,Rectangle src,Rectangle dest,SpriteEffects flip=SpriteEffects.None)=>
        batch.Draw(textures[key],dest,src,Color.White,0,Vector2.Zero,flip,0);
    void Scene(int n,int y)=>Pic("scene"+n,new Rectangle(0,0,256,128),new Rectangle(0,y,256,128));
    static int Step(char ch)=>ch<128?5:8;
    static int Width(string s,int scale=1)=>s.Sum(Step)*scale;
    void Text(string s,int x,int y,Color color,int scale=1)
    {
        foreach(char ch in s){
            if(ch!=' '&&ch!='　'){
                int i=glyphs.GetValueOrDefault(ch,glyphs['?']);
                batch.Draw(textures["font"],new Rectangle(x,y,8*scale,8*scale),
                    new Rectangle((i%32)*8,(i/32)*8,8,8),color);
            }
            x+=Step(ch)*scale;
        }
    }
    void Center(string s,int y,Color c,int size=1)=>Text(s,(W-Width(s,size))/2,y,c,size);
    void Lines(string s,int x,int y,int maxWidth)
    {
        int left=x;
        foreach(char ch in s){
            if(ch=='\n'||x+Step(ch)>left+maxWidth){x=left;y+=12;if(ch=='\n')continue;}
            Text(ch.ToString(),x,y,Snow);x+=Step(ch);
        }
    }
    void Arrow(int x,int y){for(int i=0;i<4;i++)Rect(x+i,y+i,1,7-2*i,Amber);}
    void Option(string label,int x,int y,int number)
    {
        if(game.Cursor==number)Arrow(x-8,y);
        Text(label,x,y,game.Cursor==number?Amber:Snow);
    }
    void Icon(int i,int x,int y,bool step=false,bool flip=false)
    {
        Pic("actors",new Rectangle(i*16,step?16:0,16,16),new Rectangle(x,y,16,16),flip?SpriteEffects.FlipHorizontally:SpriteEffects.None);
    }
    void Envelope(int x,int y)
    {
        Rect(x,y,18,12,Ink);Rect(x+1,y+1,16,10,Amber);Rect(x+2,y+2,14,8,Snow);
        for(int n=0;n<7;n++){Rect(x+2+n,y+2+n/2,1,1,Shade);Rect(x+15-n,y+2+n/2,1,1,Shade);}
        Rect(x+8,y+5,3,3,Rose);
    }
    protected override void Draw(GameTime time)
    {
        if(capture!=null){Snapshots();Exit();return;}
        Paint();GraphicsDevice.Clear(Color.Black);
        var vp=GraphicsDevice.Viewport;
        int scale=Math.Max(1,Math.Min(vp.Width/W,vp.Height/H));
        batch.Begin(samplerState:SamplerState.PointClamp);
        batch.Draw(canvas,new Rectangle((vp.Width-W*scale)/2,(vp.Height-H*scale)/2,W*scale,H*scale),Color.White);
        batch.End();base.Draw(time);
    }
    void Paint()
    {
        GraphicsDevice.SetRenderTarget(canvas);GraphicsDevice.Clear(Ink);
        batch.Begin(samplerState:SamplerState.PointClamp);
        Screen bg=game.Mode is Screen.Text or Screen.Choice?game.Backdrop:game.Mode;
        switch(bg){
            case Screen.Title:TitleScreen();break;
            case Screen.Battle:BattleScreen();break;
            case Screen.Ending:EndingScreen();break;
            default:FieldScreen();break;
        }
        if(game.Mode==Screen.Text)TextBox();
        else if(game.Mode==Screen.Choice)ChoiceBox();
        else if(game.Mode==Screen.Pause){
            Frame(45,59,166,117);Text("配達手帳",58,70,Amber,2);
            Option("旅をつづける",72,105,0);Option("配達の記録",72,125,1);Option("記録してタイトルへ",72,145,2);
        }
        batch.End();GraphicsDevice.SetRenderTarget(null);
    }
    void PostageStamp(int x,int y)
    {
        Rect(x,y,34,38,Snow);
        for(int i=2;i<33;i+=5){Rect(x+i,y,2,2,Ink);Rect(x+i,y+36,2,2,Ink);}
        for(int j=3;j<36;j+=5){Rect(x,y+j,2,2,Ink);Rect(x+32,y+j,2,2,Ink);}
        Rect(x+4,y+4,26,30,Shade);Rect(x+5,y+5,24,28,Night);
        for(int j=0;j<12;j++)for(int i=0;i<12;i++){
            int a=i-6,b=j-6,k=i-8,m=j-3;
            if(a*a+b*b<33 && k*k+m*m>=23)Rect(x+8+i,y+8+j,1,1,Amber);
        }
        Rect(x+23,y+10,1,3,Snow);Rect(x+22,y+11,3,1,Snow);
        Rect(x+8,y+25,19,1,Sea);Rect(x+10,y+28,12,1,Sea);
    }
    void TitleScreen()
    {
        Scene(0,0);Rect(0,125,W,3,Ink);Rect(0,128,W,1,Amber);
        Envelope(20,140);Text("H O S H I F U M I",48,143,Sea);
        PostageStamp(199,142);
        Text("星ふみ郵便局",26,160,Shade,3);Text("星ふみ郵便局",24,158,Amber,3);
        // Airmail perforations mark the edge of the title panel.
        for(int x=0;x<W;x+=8)Rect(x,188,4,1,Shade);
        Option(game.HasSave?"つづきから":"はじめから",52,199,0);
        if(game.HasSave)Option("はじめから",154,199,1);
        Rect(0,218,W,22,Night);Center("Z / ENTER  決定     M  音",225,Snow);
    }
    static readonly Dictionary<char,int> Symbols=new(){['P']=7,['N']=1,['M']=2,['T']=3,['R']=4,['E']=5,['B']=6};
    void FieldScreen()
    {
        var p=game.P;Text($"第{p.Chapter+1}章",6,3,Amber);Text(game.C.Place,50,3,Snow);
        string hp=$"HP {p.Hp}/{p.MaxHp}";Text(hp,249-Width(hp),3,Sea);
        for(int y=0;y<11;y++)for(int x=0;x<16;x++){
            char ch=game.Tile(x,y);int variant=(x*19+y*37+x*y*3+p.Chapter)%4;
            int tile=ch=='#'?variant:4+variant;
            Pic("terrain",new Rectangle(tile*16,p.Chapter*16,16,16),new Rectangle(x*16,16+y*16,16,16));
            if(ch!='#'){
                if(game.Tile(x,y-1)=='#')Rect(x*16,16+y*16,16,1,Shade);
                if(Symbols.TryGetValue(ch,out int icon))Icon(icon,x*16,16+y*16);
            }
        }
        Icon(0,p.X*16,16+p.Y*16,foot>0,game.FaceX<0);
        Rect(0,192,W,1,Amber);
        if(game.Mode==Screen.Field||game.Mode==Screen.Pause){
            string near=Nearby();
            Text(near.Length>0?"Z  "+near:game.Objective(),7,199,near.Length>0?Amber:Snow);
            Text($"LV {p.Level}    インク {p.Ink}/5    星あめ {p.Sweets}",7,214,Sea);
            Text(game.SaveError?"記録の書き込みに失敗":"矢印  移動    Z  調べる    X  手帳",7,228,game.SaveError?Rose:Snow);
        }
    }
    string Nearby()
    {
        string Name(char c)=>c switch{'P'=>"局舎で休む",'T'=>"補給箱を開ける",'N'=>game.C.Recipient+"と話す",
            'R'=>"立札を読む",'M'=>"手紙を投函する",_=>""};
        var p=game.P;string own=Name(game.Tile(p.X,p.Y));
        return own.Length>0?own:Name(game.Tile(p.X+game.FaceX,p.Y+game.FaceY));
    }
    void Bar(int x,int y,int w,int value,int max,Color color)
    {Rect(x,y,w,3,Shade);Rect(x,y,w*Math.Clamp(value,0,max)/max,3,color);}
    void BattleScreen()
    {
        var e=game.Enemy!;var p=game.P;
        Text(e.Boss?"守り手":"街道の影",6,4,Amber);Text(e.Boss?game.C.Boss:game.C.Foe,79,4,Snow);
        Scene(p.Chapter+1,16);
        int size=e.Boss?96:64, y=144-size+(int)Math.Round(Math.Sin(clock*3)*2);
        Pic("bestiary",new Rectangle(p.Chapter*48,e.Boss?48:0,48,48),new Rectangle((256-size)/2,y-5,size,size));
        Rect(0,143,256,1,Amber);
        Text($"相手 {Math.Max(0,e.Hp)}/{e.MaxHp}",6,150,Snow);Bar(8,162,83,e.Hp,e.MaxHp,Rose);
        Text("次："+game.IntentName(e.Next),109,150,e.Next==Intent.Heavy?Rose:Amber);
        Text($"ナギ  HP {p.Hp}/{p.MaxHp}   インク {p.Ink}/5",7,172,Sea);
        Frame(3,185,250,53);
        if(game.Mode==Screen.Battle){
            Option("たたかう",21,194,0);Option("星印  2インク",131,194,1);
            Option("まもる",21,209,2);Option($"星あめ  x{p.Sweets}",131,209,3);
            Text("矢印  選択    Z  決定",14,225,Snow);
        }
    }
    void TextBox()
    {
        Frame(3,159,250,79);Text(game.Caption,12,166,Amber);
        Lines(game.Page,12,181,232);
        if((int)(clock*3)%2==0)Arrow(240,225);
    }
    void ChoiceBox()
    {
        Frame(3,150,250,88);Text(game.Caption,12,157,Sea);Text(game.Page,12,172,Snow);
        for(int i=0;i<game.Options.Length;i++)Option(game.Options[i],23,194+16*i,i);
    }
    void EndingScreen()
    {
        Scene(game.P.Ending==0?0:7,0);Rect(0,127,W,2,Amber);
        Text("配達完了　七通目の追伸",10,137,Sea);
        Text(game.P.Ending==0?"父の帰る港":"七つの朝",9,154,Amber,2);
        Lines(game.P.Ending==0?"父とナギは朝一番の船に乗った。灯台から白い灯が二度またたく。新しい宛先は、ふたりの家。":
            "七つの町から返事が届いた。明日も道は続く。ナギは局の扉に「配達に出ています」と貼った。",10,182,236);
        Text("おしまい     Z  タイトルへ",9,226,Snow);
    }
    void Snap(string name)
    {
        Paint();using var f=File.Create(Path.Combine(capture!,name+".png"));canvas.SaveAsPng(f,W,H);
    }
    void Snapshots()
    {
        Directory.CreateDirectory(capture!);Snap("title");game.NewGame();
        while(game.Mode==Screen.Text)game.Confirm();
        Snap("field");Verification.Visit(game,'N');game.Confirm();game.Confirm();
        if(game.C.Key==1)game.Navigate(1);game.Confirm();while(game.Mode==Screen.Text)game.Confirm();
        Verification.Visit(game,'B',true);Snap("battle");
    }
}
