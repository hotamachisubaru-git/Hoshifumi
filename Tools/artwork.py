"""Build the in-game pixel art. Pass the optional 2 x 4 location sheet to rebuild panoramas."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps
import random, sys, math
import numpy as np
OUT=Path(__file__).resolve().parents[1]/'Assets'/'Graphics'
OUT.mkdir(parents=True,exist_ok=True)
# RGB swatches drawn from an NTSC-style NES palette.
ink='#101024'; ivory='#fff1cf'; gold='#ffce73'; red='#e4533e'; navy='#23365e'
themes=[
 ('#142849','#167388','#47b59b','#ffd684'),('#142633','#235349','#50a65b','#d3db87'),
 ('#381e32','#95432d','#dd8950','#ffe2a5'),('#1b2248','#355e9b','#79c8dc','#e6f3e8'),
 ('#29204a','#674486','#b782cb','#f7dea2'),('#231a32','#77333c','#d36142','#ffbc65'),
 ('#101a3b','#344282','#7589bd','#f7dea2')]
def rgb(s): return tuple(bytes.fromhex(s[1:]))
def panoramas(path):
    source=Image.open(path).convert('RGB')
    w,h=source.size
    for n in range(8):
        col=n%2; row=n//2
        pic=source.crop((int(w*col/2)+3,int(h*row/4)+3,int(w*(col+1)/2)-3,int(h*(row+1)/4)-3))
        pic=ImageOps.fit(pic,(256,128),method=Image.Resampling.BOX)
        base=themes[max(0,n-1)]
        hues=[base, (base[0],navy,'#4977a9','#a6d7d8'),
              (base[0],'#734237','#cc8156',gold),(base[0],base[1],base[2],ivory)]
        if n==0:hues=[('#10152f','#243963','#577da4','#b7d7d7'),('#10152f','#683e44','#dd8d52','#ffdc91'),('#10152f','#254554','#728d77',ivory)]
        arr=np.asarray(pic,dtype=float); out=np.empty_like(arr)
        for y in range(0,128,8):
            for x in range(0,256,8):
                tile=arr[y:y+8,x:x+8]; best=None
                for group in hues:
                    pal=np.array([rgb(c) for c in group],dtype=float)
                    dist=(((tile[:,:,None,:]-pal[None,None,:,:])**2)*[.9,1.4,.7]).sum(3)
                    err=dist.min(2).sum()
                    if best is None or err<best[0]: best=(err,pal[dist.argmin(2)])
                out[y:y+8,x:x+8]=best[1]
        Image.fromarray(out.astype('uint8')).save(OUT/f'scene{n}.png',optimize=True)

def terrain():
    sheet=Image.new('RGB',(16*8,16*7))
    for c,(dark,mid,light,shine) in enumerate(themes):
        for tile in range(8):
            im=Image.new('RGB',(16,16),dark if tile<4 else mid)
            d=ImageDraw.Draw(im); rnd=random.Random(255*c+tile)
            if tile<4:
                if c in (0,6):
                    for y in [3,9,14]:
                        x=rnd.randrange(-2,8);d.line((x,y,x+5,y),fill=mid);d.point((x+6,y-1),light)
                    if c==6:
                        x,y=5+tile,4+tile;d.point((x,y),shine);d.point((14,11),light)
                elif c==1:
                    d.rectangle((6,9,9,15),fill=mid)
                    for x,y in [(7,0),(5,3),(9,3),(2,6),(12,6),(0,9),(5,7),(9,8)]:
                        d.rectangle((x,y,x+3,y+3),fill=mid);d.line((x+1,y,x+3,y),fill=light)
                    d.point((7,3),shine)
                elif c in (2,3):
                    k=tile%3;d.polygon([(0,15),(3,8),(8+k,1),(15,12),(15,15)],fill=mid)
                    d.polygon([(3,9),(8+k,1),(11+k,6),(8,4)],fill=light)
                    d.line((6,5,8+k,2),fill=shine);d.line((10,9,13,13),fill=dark)
                elif c==4:
                    for x,y in [(1,9),(7,4),(12,10)]:
                        d.line((x,y+2,x,y+5),fill=mid);d.rectangle((x-2,y,x+2,y+1),fill=light)
                        d.rectangle((x,y-2,x+1,y+2),fill=mid);d.point((x,y),shine)
                else:
                    for y in [1,6,11]:
                        x=(y%2)*5;d.rectangle((x-7,y,x+5,y+3),fill=mid);d.rectangle((x+7,y,x+14,y+3),fill=mid)
                        d.line((x-2,y+2,x+2,y+2),fill=light)
            else:
                floor,edge,mark=[
                    ('#b48a56','#725336','#ead49a'),('#659856','#386547','#a7bd74'),
                    ('#d89b61','#955c45','#f0c684'),('#92c8d3','#638eaf','#d4ede1'),
                    ('#ae84a2','#805076','#edbcac'),('#9c6760','#653d46','#d78862'),
                    ('#959bc3','#626990','#d8d1e0')][c]
                d.rectangle((0,0,15,15),fill=floor)
                if c in (0,5):
                    for yy in [0,8]:
                        d.line((0,yy,15,yy),fill=edge)
                        for xx in [2,13]:d.point((xx,yy+2),edge)
                    d.line((4+tile%3,5,10,5),fill=mark)
                elif c in (3,4,6):
                    d.line((0,15,15,15),fill=edge);d.line((0,7,15,7),fill=edge)
                    d.line((4,0,4,6),fill=edge);d.line((12,8,12,14),fill=edge)
                    d.line((6,1,11,1),fill=mark)
                else:
                    for _ in range(5):
                        x,y=rnd.randrange(15),rnd.randrange(15);d.line((x,y,x+rnd.randrange(1,3),y),fill=edge)
                    d.point((3+tile,10),mark)
            sheet.paste(im,(tile*16,c*16))
    sheet.save(OUT/'terrain.png')

def token(rows,colors):
    im=Image.new('RGBA',(16,16))
    for y,row in enumerate(rows.strip().splitlines()):
        for x,ch in enumerate(row):
            if ch!='.':im.putpixel((x,y),rgb(colors[ch])+(255,))
    return im
hero="""
......0000......
....00111100....
...0111111110...
..001111111100..
..000022220000..
....02202020....
....02222220....
.....022220.....
...001111100....
..01201111020...
..02201100220...
...001100110....
....01111110....
....02200220....
...0000..0000...
................"""
icons=[
hero,
"""
......000.......
....0011100.....
...011111110....
...001111100....
....0220220.....
....0222220.....
.....02220......
...000111000....
..01221112210...
..01201110210...
...001111100....
....0111110.....
....0111110.....
....0220220.....
...0000.0000....
................""",
"""
................
...0000000000...
..022222222210..
..021111111110..
..021000000110..
..021111111110..
..022222222110..
..011111111110..
...0000110000...
......0110......
......0110......
......0110......
......0110......
.....001100.....
....00000000....
................""",
"""
................
................
....00000000....
...0222222220...
..022211112210..
..011111111110..
..000000000000..
..022222222210..
..011100001110..
..011102201110..
..011100001110..
..011111111110..
...0000000000...
................""",
"""
................
................
...0000000000...
..022222222210..
..021111111110..
..022122212210..
..021111111110..
...0000000000...
......0110......
......0110......
......0110......
......0110......
.....001100.....
................""",
"""
................
......00........
.....0220.......
....021120......
...02111120.....
..0211111120....
..0112112110....
..0110110110....
...01111110.....
....010010......
...00....00.....
................""",
"""
.......00.......
....00.22.00....
....02122120....
....01111110....
.....022220.....
....01111110....
...0112112110...
..011100001110..
..011122221110..
...0111111110...
....00000000....
...00......00...
................""",
"""
.......00.......
......0220......
.....022220.....
....02011020....
...0222222220...
..000000000000..
....01111110....
....01222210....
....01200210....
....01200210....
....01111110....
...0000000000...
................"""]
def markers():
    sheet=Image.new('RGBA',(16*8,32))
    colors=[(red,ivory),('#439289',gold),(red,ivory),('#9d5834',gold),('#9a583e',ivory),('#7767ab',ivory),('#b14446',gold),('#477f93',ivory)]
    for i,(pattern,(a,b)) in enumerate(zip(icons,colors)):
        im=token(pattern,{'0':ink,'1':a,'2':b});sheet.paste(im,(i*16,0))
        if i==0:
            stride=im.copy();stride.paste((0,0,0,0),(0,13,16,16))
            stride.paste(im.crop((2,13,8,15)),(3,12));stride.paste(im.crop((8,13,14,15)),(7,14))
            im=stride
        sheet.paste(im,(i*16,16))
    sheet.save(OUT/'actors.png')
def bestiary():
    from PIL import ImageFilter
    sheet=Image.new('RGBA',(48*7,48*2))
    for i,(_,secondary,bright,glint) in enumerate(themes):
        for boss in [False,True]:
            im=Image.new('RGBA',(48,48)); d=ImageDraw.Draw(im)
            a,b,h=secondary,bright,glint
            def poly(v,c):d.polygon(v,fill=c)
            def orb(box,c):d.ellipse(box,fill=c)
            def bar(box,c):d.rectangle(box,fill=c)
            def line(points,c,width=1):d.line(points,fill=c,width=width)
            def eyes(x,y,spacing=8):
                for xx in [x,x+spacing]:
                    bar((xx,y,xx+3,y+3),ink);bar((xx,y,xx+1,y+1),h)
            if i==0: # shore crab, with a grandfather anchor for the guardian
                for side in [-1,1]:
                    for k in range(3):
                        x=24+side*(9+k)
                        line([(x,29+k),(x+side*5,28+k*2),(x+side*8,33+k*2)],b,2)
                    x=24+side*16;orb((x-5,18,x+5,26),a)
                    poly([(x-4,20),(x-4,12),(x,17),(x+4,13),(x+4,22)],b)
                orb((10,22,38,34),a);orb((13,20,35,29),b)
                bar((16,19,18,24),b);bar((29,19,31,24),b);eyes(16,18,13)
                for x in (18,23,28):bar((x,29,x+1,30),h)
                if boss:
                    orb((20,3,27,10),h);orb((22,5,25,8),ink);bar((23,9,25,22),h)
                    line([(16,15),(18,20),(24,23),(31,20),(33,15)],h,2);bar((19,11,29,12),h)
            elif i==1 and not boss: # bat
                for side in [-1,1]:
                    poly([(24,21),(24+side*12,12),(24+side*20,16),(24+side*21,30),(24+side*15,25),(24+side*10,32),(24+side*6,27),(24,31)],b)
                    line([(24+side*5,22),(24+side*13,16),(24+side*18,17)],h)
                orb((17,19,30,34),a);poly([(18,22),(17,13),(23,19),(28,13),(29,23)],a);eyes(19,22,6)
                orb((22,29,26,34),h)
            elif i==1: # bell stag
                poly([(5,26),(11,21),(24,22),(29,14),(37,14),(37,20),(32,24),(29,33),(12,34),(6,31)],b)
                for x in (10,15,25,29):line([(x,29),(x-1,37),(x-3,41)],a,3)
                for dx in (0,7):
                    line([(29+dx,15),(27+dx,9),(23+dx,5),(22+dx,2)],h,2)
                    line([(26+dx,8),(28+dx,3)],h,2)
                poly([(33,16),(42,18),(40,22),(33,21)],b);bar((34,16,35,17),ink)
                orb((18,26,24,33),h);bar((17,32,25,33),h);bar((20,33,21,35),ink)
                for x in (12,16,20):d.point((x,24),h)
            elif i==2 and not boss: # sand lizard
                line([(20,28),(11,33),(5,28),(3,24)],a,4);orb((13,23,35,33),b);orb((27,17,41,28),b)
                for x in (16,23,30):poly([(x,24),(x+2,17),(x+4,24)],h)
                line([(19,30),(17,36),(12,37)],a,3);line([(32,28),(35,33),(40,33)],a,3)
                eyes(33,20,5)
            elif i==2: # sand engine beast
                for x in (10,21,33):
                    orb((x-5,29,x+6,40),a);orb((x-2,32,x+3,37),h);bar((x,33,x+1,35),ink)
                bar((5,17,18,31),a);bar((8,19,14,23),h);bar((3,15,20,17),b)
                orb((16,19,39,32),b);bar((28,13,33,24),a);bar((26,11,35,14),h)
                for x in (20,25):bar((x,21,x+1,30),a)
                poly([(33,26),(42,21),(40,28),(45,30),(36,35)],b);eyes(32,24,6)
                for x,y in [(26,7),(21,4),(16,3)]:orb((x,y,x+4,y+2),a)
            elif i==3 and not boss: # frost owl
                orb((11,15,36,37),a);poly([(11,21),(11,10),(19,16),(29,16),(36,10),(36,23)],b)
                for x in [14,25]:orb((x,18,x+9,27),h);orb((x+3,21,x+6,25),ink)
                poly([(21,25),(27,25),(24,30)],b);line([(13,29),(18,34),(21,35)],b,2)
                bar((15,37,20,38),h);bar((27,37,32,38),h)
            elif i==3: # ice wolf
                poly([(4,23),(4,15),(12,20),(18,22),(27,20),(32,12),(33,5),(37,11),(42,7),(41,18),(45,21),(40,25),(35,26),(29,32),(13,32)],b)
                poly([(5,20),(12,23),(17,25),(10,25)],h)
                for x in (12,18,28,33):poly([(x,28),(x+3,28),(x+1,38),(x-3,39)],a)
                poly([(31,18),(36,15),(41,17),(39,19),(34,21),(28,27)],h);bar((37,16,39,17),ink)
                for x,y in [(13,21),(19,21),(24,20)]:poly([(x,y),(x+3,y-7),(x+4,y)],h)
            elif i==4 and not boss: # cotton bee
                orb((12,10,24,25),h);orb((24,9,35,24),h)
                line([(16,14),(23,22)],b);line([(29,13),(25,21)],b)
                orb((10,22,36,33),b);bar((17,24,20,32),a);bar((23,22,25,32),a);eyes(29,24,5)
                poly([(11,24),(5,28),(12,29)],h)
            elif i==4: # royal moth
                for side in [-1,1]:
                    def wing(pts,col):poly([(24+side*x,y) for x,y in pts],col)
                    wing([(1,22),(10,8),(20,5),(21,14),(18,25),(9,28),(17,30),(14,38),(7,36),(2,29)],b)
                    wing([(5,22),(13,13),(18,11),(13,20),(9,24)],a)
                    x=24+side*13;orb((x-3,14,x+2,19),h);orb((x-1,15,x+1,17),a)
                orb((21,15,27,33),a);eyes(21,18,4);line([(23,16),(19,9),(16,8)],h);line([(25,16),(29,9),(32,8)],h)
                for x in [19,23,27]:orb((x,12,x+3,15),h)
            elif i==5 and not boss: # soot spirit
                poly([(10,31),(13,24),(18,22),(19,14),(23,18),(29,9),(31,20),(36,24),(39,32),(33,37),(16,37)],a)
                poly([(13,32),(20,28),(23,23),(27,29),(32,23),(35,32),(32,35),(17,35)],b);eyes(19,28,9)
                bar((16,37,20,39),h);bar((30,37,34,39),h)
            elif i==5: # type foundry golem
                for x in [5,31]:bar((x,18,x+10,34),a);bar((x+1,17,x+8,23),b);bar((x+1,29,x+8,31),h)
                for x in [14,26]:bar((x,30,x+6,42),a);bar((x-2,39,x+7,42),b)
                bar((13,15,34,32),b);bar((17,19,30,29),ink);poly([(19,27),(19,23),(22,25),(25,20),(28,25),(27,28)],h)
                bar((17,6,30,16),a);bar((16,5,31,8),b);bar((18,10,29,12),h);bar((23,10,24,12),ink)
            elif not boss: # night ray
                poly([(3,17),(13,18),(22,15),(32,18),(44,12),(40,26),(32,30),(23,29),(15,31),(9,25)],b)
                poly([(8,19),(17,22),(23,17),(32,21),(40,17),(34,25),(22,25)],a)
                line([(23,27),(24,33),(29,36),(31,41)],h,2);eyes(18,20,8)
            else: # black postmark dragon
                poly([(22,24),(15,10),(6,5),(9,14),(2,12),(6,23),(13,22),(11,28),(22,30)],b)
                poly([(25,24),(31,10),(41,3),(40,14),(46,11),(42,23),(34,23),(37,27),(26,31)],b)
                for x in (10,37):line([(x,12),(23,26)],h)
                orb((15,21,33,36),a);line([(20,33),(13,38),(6,36),(3,31)],b,3)
                poly([(24,28),(28,16),(25,10),(26,4),(31,9),(36,5),(36,13),(41,15),(39,21),(33,21),(30,31)],b)
                poly([(29,18),(34,19),(30,29),(24,34),(21,33)],h);bar((33,13,35,14),ink);d.point((34,13),h)
                for x in (18,27):bar((x,33,x+3,39),b);bar((x-2,38,x+4,40),h)
            outline=Image.new('RGBA',im.size,ink);outline.putalpha(im.getchannel('A').filter(ImageFilter.MaxFilter(3)))
            outline.alpha_composite(im);sheet.paste(outline,(48*i,48*int(boss)))
    sheet.save(OUT/'bestiary.png')
if __name__=='__main__':
    terrain();markers();bestiary()
    if len(sys.argv)>1:panoramas(sys.argv[1])
