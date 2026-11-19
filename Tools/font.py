"""Bake only the game's characters from the official 8-pixel Misaki Gothic TTF."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import sys
root=Path(__file__).resolve().parents[1]
letters=set(chr(c) for c in range(32,127))
for p in [*root.glob('Game/*.cs'),root/'Assets/chapters.json']:
    letters.update(c for c in p.read_text() if ord(c)>127 and not c.isspace())
chars=''.join(sorted(letters));font=ImageFont.truetype(sys.argv[1],8)
alpha=Image.new('L',(256,8*((len(chars)+31)//32)))
painter=ImageDraw.Draw(alpha)
for i,c in enumerate(chars):
    if c=='　':continue
    painter.text((i%32*8,i//32*8),c,font=font,fill=255,stroke_width=0)
im=Image.new('RGBA',alpha.size,'white');im.putalpha(alpha)
im.save(root/'Assets/Graphics/font.png')
(root/'Assets/Graphics/glyphs.txt').write_text(chars+'\n')
