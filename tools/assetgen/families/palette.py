"""Master colour definitions and shader lookup tables."""
import colorsys
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
BASES = {
    'soil':'795747', 'sand':'b6a17a', 'rock':'818184', 'basalt':'514958',
    'snow':'d7dfe0', 'ice':'91bdc6', 'water':'417d98', 'grass':'72864d',
    'foliage':'52704b', 'bark':'705344', 'wood':'9c7955', 'thatch':'ad945e',
    'hide':'a68b70', 'mudbrick':'99725e', 'stone':'929087', 'clay':'af795e',
    'metal_copper':'b67851', 'metal_iron':'85929a', 'fire':'e79840',
    'lava':'d8653c', 'ash':'928b91', 'divine':'e2cf99', 'blight':'9caa55',
    'shadow':'4b4154', 'flower':'bd7d94',
}
UI = dict(bg='#1e1813', panel='#2a211a', raised='#3a2e24', border='#5a4636',
          bone='#f1e6d9', parchment='#d4c3b1', ochre='#c8913a', moss='#6f8f4e',
          terracotta='#b5553c', river_blue='#4f86a8', divine_gold='#f2d27a')
BIOMES = ['ice_sheet','tundra','boreal_forest','cold_desert','temperate_grassland',
          'temperate_forest','temperate_rainforest','shrubland','hot_desert','savanna',
          'tropical_seasonal_forest','tropical_rainforest','wetland','alpine',
          'volcanic','ocean','deep_ocean','lake','coast']
TROPICAL = {'savanna','tropical_seasonal_forest','tropical_rainforest'}

def rgb(value):
    return tuple(bytes.fromhex(value.lstrip('#')))

def ramp(value, count):
    h,s,v = colorsys.rgb_to_hsv(*(c/255 for c in rgb(value)))
    result = []
    for i in range(count):
        t = i/(count-1)
        target = .76 if t < .5 else .14
        shift = ((target-h+.5)%1-.5) * abs(t-.5)*.26
        result.append(tuple(round(c*255) for c in colorsys.hsv_to_rgb(
            (h+shift)%1, min(.85,s*(1.12-.32*t)), max(.035,min(.97,v*(.48+.82*t))))))
    return result

def build(seed=0):
    out = ROOT/'assets/palette'
    out.mkdir(parents=True, exist_ok=True)
    ramps = {k:ramp(v,5) for k,v in BASES.items()}
    skins = ['49302b','624035','795044','90604b','a87658','bf916c','d5ac88','e6c7a3']
    hairs = ['322b32','514038','77513c','9c704b','c2a06c','b66e45','aaa5a2','dedbd1']
    dyes = ['a75147','ba7946','bd9d53','818d4f','50775c','4e8c86',
            '507d9a','626d9a','86658f','ab738b','d1bda0','625858','a58b70']
    cultures = ['be6553','c28a4f','c6ac60','96a85e','639366','57938a',
                '5b91ab','727fa9','946fa5','b6779b','b69b83','8f9691']
    for name, values, count in [('skin',skins,5),('hair',hairs,4),('cloth',dyes,4),('culture',cultures,3)]:
        for i,value in enumerate(values):
            ramps[f'{name}_{i}'] = ramp(value,count)
    ramps['ui'] = list(map(rgb,UI.values()))
    slots = []
    def add(name, colour):
        slots.append({'index':len(slots),'name':name,'reference':colour})
    for name, colours in ramps.items():
        if name.startswith(('skin_','hair_','cloth_','culture_')) or name in {'grass','foliage','bark','flower'}:
            continue
        for i,c in enumerate(colours): add(f'fixed_{name}_{i}',c)
    variable = {'grass':ramps['grass'],'foliage':ramps['foliage'],'bark':ramps['bark'],
                'flower':ramps['flower'],'skin':ramps['skin_3'],'hair':ramps['hair_2'],
                'cloth_a':ramps['cloth_12'],'cloth_b':ramps['cloth_12'],'culture':ramps['culture_0']}
    for name,colours in variable.items():
        for i,c in enumerate(colours): add(f'{name}_{i}',c)
    add('culture_white',rgb(UI['bone']))
    for i,colour in enumerate(ramps['cloth_0']): add(f'fruit_{i}',colour)
    assert len(slots)<=256
    colours = list(dict.fromkeys(tuple(c) for values in ramps.values() for c in values))
    swatch = Image.new('RGBA',(16*16, ((len(colours)+15)//16)*16),(0,0,0,0))
    for i,c in enumerate(colours):
        swatch.paste((*c,255),((i%16)*16,(i//16)*16,(i%16+1)*16,(i//16+1)*16))
    swatch.save(out/'palette.png')
    (out/'evolysia.gpl').write_text('GIMP Palette\nName: Evolysia\nColumns: 16\n#\n'+
        ''.join(f'{r:3} {g:3} {b:3}\tcolour_{i:03}\n' for i,(r,g,b) in enumerate(colours)),encoding='utf-8')
    data = {'version':1,'seed':seed,'ramps':ramps,'ui':UI,'colours':colours,'slots':slots,
            'reference':{'nature':'temperate_forest.summer','skin':3,'hair':2,'cloth':12,'culture':0},
            'luts':{}}
    entries = []
    def emit(name, rows, labels):
        img = Image.new('RGBA',(256,len(rows)),(0,0,0,0))
        for y,row in enumerate(rows):
            for x,c in enumerate(row): img.putpixel((x,y),(*c,255))
        img.save(out/f'{name}.png')
        data['luts'][name] = {'rows':labels,'unused_slots':'transparent'}
        entries.append({'id':f'palette.{name}','family':'palette','files':[f'palette/{name}.png'],
            'frame':[256,1],'frames':1,'directions':labels,'fps':0,'pivot':[0,0],
            'tags':['lookup_texture'],'rows':labels})
    reference = [s['reference'] for s in slots]
    def replace(row, group, values):
        for s in slots:
            if s['name'].startswith(group+'_'):
                row[s['index']] = values[-1] if s['name']=='culture_white' else values[int(s['name'].rsplit('_',1)[1])]
    for kind,count in [('skin',8),('hair',8),('cloth',13),('culture',12)]:
        rows=[]
        for i in range(count):
            row=reference.copy()
            for group in (['cloth_a','cloth_b'] if kind=='cloth' else [kind]):
                replace(row,group,ramps[f'{kind}_{i}'])
            if kind=='cloth': replace(row,'fruit',ramps[f'cloth_{i}'])
            rows.append(row)
        emit(f'lut_{kind}',rows,[('undyed' if kind=='cloth' and i==12 else f'{kind}_{i}') for i in range(count)])
    rows=[]; labels=[]
    for biome in BIOMES:
        for season in (['wet','dry'] if biome in TROPICAL else ['spring','summer','autumn','winter']):
            row=reference.copy()
            cold=biome in {'ice_sheet','tundra','boreal_forest','cold_desert','alpine'}
            dry=biome in {'hot_desert','shrubland','cold_desert'} or season=='dry'
            grass='a39565' if dry else ('829770' if cold else '72864d')
            leaf='9d674c' if season=='autumn' else ('85918a' if season=='winter' else ('79964e' if season=='spring' else '52704b'))
            if biome=='volcanic': grass='7f7968'; leaf='716c60'
            if season=='wet': grass='648550'; leaf='477353'
            if season=='dry': leaf='92915d'
            if season=='autumn': grass='ac945a'
            if season=='winter': grass='92958a'
            for group,value in [('grass',grass),('foliage',leaf)]: replace(row,group,ramp(value,5))
            rows.append(row); labels.append(f'{biome}.{season}')
    emit('lut_nature',rows,labels)
    for row in rows:
        for c in row:
            if tuple(c) not in colours: colours.append(tuple(c))
    data['colours']=colours
    swatch=Image.new('RGBA',(256,((len(colours)+15)//16)*16),(0,0,0,0))
    for i,c in enumerate(colours): swatch.paste((*c,255),((i%16)*16,(i//16)*16,(i%16+1)*16,(i//16+1)*16))
    swatch.save(out/'palette.png')
    (out/'evolysia.gpl').write_text('GIMP Palette\nName: Evolysia\nColumns: 16\n#\n'+''.join(f'{r:3} {g:3} {b:3}\tcolour_{i:03}\n' for i,(r,g,b) in enumerate(colours)),encoding='utf-8')
    (out/'palette.json').write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
    entries.insert(0,{'id':'palette.master','family':'palette','files':['palette/palette.png'],
        'frame':list(swatch.size),'frames':1,'directions':['none'],'fps':0,'pivot':[0,0],
        'tags':['palette_swatches'],'sources':['palette/evolysia.gpl','palette/palette.json']})
    for entry in entries:
        sidecar=out/(Path(entry['files'][0]).stem+'.json')
        if sidecar.name=='palette.json': sidecar=out/'palette.meta.json'
        entry['sidecar']=str(sidecar.relative_to(ROOT/'assets')).replace('\\','/')
        sidecar.write_text(json.dumps({**entry,'anims':{},'recolor_slots':[]},indent=2)+'\n',encoding='utf-8')
    return entries
