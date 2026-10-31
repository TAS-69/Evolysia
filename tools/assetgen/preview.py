
import json
from PIL import Image, ImageDraw

def build_preview(root):
    data=json.loads((root/'assets/palette/palette.json').read_text())
    out=root/'assets/_preview'; out.mkdir(exist_ok=True)
    ramps=data['ramps']
    sheet=Image.new('RGB',(880,len(ramps)*24+48),'#1e1813')
    draw=ImageDraw.Draw(sheet)
    draw.text((8,8),'Evolysia master ramps | 4x pixels | dark to light',fill='#f1e6d9')
    for y,(name,colours) in enumerate(ramps.items()):
        yy=36+y*24
        draw.text((8,yy+3),name,fill='#f1e6d9')
        for i,c in enumerate(colours): draw.rectangle((188+i*40,yy,223+i*40,yy+19),fill=tuple(c))
    sheet.save(out/'palette_ramps.png')
    for name,meta in data['luts'].items():
        lut=Image.open(root/f'assets/palette/{name}.png')
        w=1320; h=42+len(meta['rows'])*36
        sheet=Image.new('RGB',(w,h),'#1e1813'); draw=ImageDraw.Draw(sheet)
        groups={'lut_nature':['grass','foliage','bark','flower'], 'lut_skin':['skin'],
                'lut_hair':['hair'],'lut_cloth':['cloth_a','cloth_b','fruit'],'lut_culture':['culture']}[name]
        focus=[s['index'] for s in data['slots'] if any(s['name'].startswith(g+'_') for g in groups)]
        draw.text((8,8),f'{name} | 4x variable slots: {", ".join(groups)} | full 1x rows on grass / dark',fill='#f1e6d9')
        used=len(data['slots'])
        for y,label in enumerate(meta['rows']):
            yy=34+y*36
            draw.text((8,yy),label,fill='#f1e6d9')
            strip=lut.crop((0,y,used,y+1))
            focused=Image.new('RGBA',(len(focus),1))
            for x,index in enumerate(focus): focused.putpixel((x,0),lut.getpixel((index,y)))
            sheet.paste(focused.resize((len(focus)*4,12),Image.Resampling.NEAREST),(260,yy))
            for offset,bg in [(16,'#72864d'),(24,'#1e1813')]:
                draw.rectangle((260,yy+offset,260+used,yy+offset+5),fill=bg)
                sheet.paste(strip,(260,yy+offset+2),strip)
        sheet.save(out/f'{name}.png')
