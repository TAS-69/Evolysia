
import json
import math
import numpy as np
from PIL import Image, ImageDraw
from families.civilisation import FLOORS, WALLS, ROOFS, OBJECTS, CONTAINERS, ANIMATED, OUTDOOR, VEHICLES, RESOURCES

GRASS='#72864d'; DARK='#1e1813'; BONE='#f1e6d9'

class Composer:
    def __init__(self,root):
        self.root=root; self.base=root/'assets/civilisation'; self.cache={}
        self.storage=json.loads((self.base/'storage_profiles.json').read_text())['profiles']
        self.vehicles=json.loads((self.base/'vehicle_profiles.json').read_text())['profiles'] if (self.base/'vehicle_profiles.json').exists() else {}
        self.wardrobe=None
        if (root/'assets/wardrobe/customisation.json').exists():
            from wardrobe_preview import Wardrobe
            self.wardrobe=Wardrobe(root)

    def art(self,name,density=64,frame=0):
        key=(name,density,frame)
        if key not in self.cache:
            stem=f'{name}_lod{density}'; im=Image.open(self.base/f'{stem}.png')
            w,h=json.loads((self.base/f'{stem}.json').read_text())['frame']
            self.cache[key]=im.crop((frame*w,0,(frame+1)*w,h))
        return self.cache[key]

    def container(self,kind,resource='grain',count=0,density=64,closed=False,inventory=None):
        profile=self.storage[kind]; result=self.art(profile['layers']['back'],density).copy()
        inventory=[{'resource_id':resource} for _ in range(count)] if inventory is None else inventory
        for (x,y),stored in zip(profile['slot_anchors_px64'],inventory):
            item=self.art('stored_'+stored['resource_id'],density)
            result.alpha_composite(item,(x*density//64,y*density//64))
        if closed: result.alpha_composite(self.art(profile['layers']['lid'],density))
        result.alpha_composite(self.art(profile['layers']['front'],density))
        return result

    def vehicle(self,kind,density=64,loaded=False):
        result=self.art(kind,density).copy(); source=result.copy(); profile=self.vehicles.get(kind,{})
        if loaded:
            for slot in profile.get('attachment_slots',[]):
                container=slot['container']; tile=self.container(container,count=len(self.storage[container]['slot_anchors_px64']),density=density)
                x,y=slot['position']; result.alpha_composite(tile,(x*density//64,y*density//64))
            polygon=profile.get('foreground_polygon')
            if polygon:
                mask=Image.new('L',result.size); ImageDraw.Draw(mask).polygon([(x*density//64,y*density//64) for x,y in polygon],fill=255)
                source.putalpha(Image.fromarray(np.minimum(np.array(source.getchannel('A')),np.array(mask))))
                result.alpha_composite(source)
        return result

    def world(self,density=64):
        result=Image.new('RGBA',(12*density,10*density))
        for y in range(10):
            for x in range(12):
                material='packed_path' if y==8 or x==7 else 'grass'
                result.alpha_composite(self.art('floor_'+material,density),(x*density,y*density))
        plans=json.loads((self.base/'blueprints.json').read_text())['blueprints']
        granary=next(p for p in plans if p['id']=='granary')
        result.alpha_composite(self.room(granary,density),(0,0))
        for y in [2,3]:
            for x in [8,9,10,11]: result.alpha_composite(self.art('crop_grain_detail',density),(x*density,y*density))
        for kind,x,y in [('tree_conifer',9,0),('tree_broadleaf',10,4),('boulder_detail',8,6),('person_detail',7,7)]:
            tile=self.wardrobe.compose('traveller',density=density,pieces={'held_item':None}) if kind=='person_detail' and self.wardrobe else self.art(kind,density)
            result.alpha_composite(tile,(x*density,y*density))
        for kind,x,y,resource,count in [('handcart',7,8,'grain',5),('backpack',8,9,'food',2),('crate',9,8,'log',3)]:
            result.alpha_composite(self.container(kind,resource,count,density),(x*density,y*density))
        return result

    def room(self,plan,density=64,roof=False,frame=0):
        w,h=plan['size_cells']; result=Image.new('RGBA',(w*density,h*density))
        for y in range(h):
            for x in range(w): result.alpha_composite(self.art('floor_'+plan['floor'],density),(x*density,y*density))
        perimeter={(x,y) for y in range(h) for x in range(w) if x in {0,w-1} or y in {0,h-1}}
        perimeter.discard(tuple(plan['entrance_cell'])); layers=[]
        if plan['wall']:
            for x,y in perimeter:
                if not roof and y==h-1: continue
                mask=sum(bit for dx,dy,bit in [(0,-1,1),(1,0,2),(0,1,4),(-1,0,8)] if (x+dx,y+dy) in perimeter)
                layers.append((y,x,self.art(f'wall_{plan["wall"]}_{mask:02d}',density)))
        for item in plan['objects']+plan['actors']:
            kind=item['asset_id']; x,y=item['cell']
            if kind=='person_detail' and self.wardrobe:
                tile=self.wardrobe.compose(plan.get('actor_outfit','farmer'),density=density,pieces={'held_item':None})
            else: tile=self.container(kind,item.get('resource','grain'),item.get('count',0),density,item.get('closed',False)) if kind in self.storage else self.art(kind,density,frame%4 if kind in ANIMATED else 0)
            layers.append((y,x,tile))
        if plan.get('entrance_visual'):
            x,y=plan['entrance_cell']; layers.append((y,x,self.art(plan['entrance_visual'],density)))
        for y,x,tile in sorted(layers,key=lambda value:(value[0],value[1])):
            result.alpha_composite(tile,(x*density,y*density))
        if roof and plan['roof']:
            for y in range(h):
                for x in range(w): result.alpha_composite(self.art('roof_'+plan['roof'],density),(x*density,y*density))
        return result

def native(sheet,tile,x,y):
    for col,bg in enumerate([GRASS,DARK,BONE]):
        sample=Image.new('RGBA',tile.size,bg)
        if col<2: sample.alpha_composite(tile)
        else:
            sample=Image.new('RGBA',tile.size,DARK); silhouette=Image.new('RGBA',tile.size,BONE); silhouette.putalpha(tile.getchannel('A')); sample.alpha_composite(silhouette)
        sheet.paste(sample.convert('RGB'),(x+col*(tile.width+5),y))

def build_civilisation_preview(root):
    c=Composer(root); out=root/'assets/_preview'
    plans=json.loads((c.base/'blueprints.json').read_text())['blueprints']
    for plan in plans:
        scene=c.room(plan); w,h=scene.size
        sheet=Image.new('RGB',(w*4+290,h*4+150),DARK); d=ImageDraw.Draw(sheet)
        d.text((10,10),plan['id']+' | furnished cutaway, 4x native close detail | separate objects / contents',fill=BONE)
        sheet.paste(scene.resize((w*4,h*4),Image.Resampling.NEAREST),(8,36))
        sx=w*4+24
        d.text((sx,36),'Same logical footprint: 7 x 6 cells',fill=BONE)
        d.text((sx,58),'Distant artwork density: 16',fill=BONE)
        sheet.paste(c.room(plan,16).convert('RGB'),(sx,78))
        d.text((sx,190),'Middle artwork density: 32',fill=BONE)
        sheet.paste(c.room(plan,32).convert('RGB'),(sx,210))
        d.text((sx,425),'Roof shown / room hidden',fill=BONE)
        sheet.paste(c.room(plan,32,roof=True).convert('RGB'),(sx,445))
        d.text((10,h*4+48),'Capabilities: '+', '.join(plan['capabilities']),fill=BONE)
        d.text((10,h*4+70),'Layout sample only. Roof visibility, inventories, navigation and specialist assignments need engine integration.',fill=BONE)
        sheet.save(out/f'civilisation_room_{plan["id"]}.png')
    chosen=['camp','granary','smithy','future_fabricator']
    overview=Image.new('RGB',(1840,1660),DARK); d=ImageDraw.Draw(overview)
    d.text((12,10),'Evolysia | detailed civilisation samples | each object is separate | 2x close artwork',fill=BONE)
    for i,name in enumerate(chosen):
        plan=next(p for p in plans if p['id']==name); im=c.room(plan); x=12+(i%2)*916; y=40+(i//2)*810
        d.text((x,y),name,fill=BONE); overview.paste(im.resize((896,768),Image.Resampling.NEAREST).convert('RGB'),(x,y+22))
    overview.save(out/'civilisation_overview.png')
    kinds=OBJECTS+list(CONTAINERS)+['person_detail']
    for page in range(math.ceil(len(kinds)/8)):
        sheet=Image.new('RGB',(1120,770),DARK); d=ImageDraw.Draw(sheet)
        d.text((10,10),'Detailed objects | 4x | native grass / dark / silhouette underneath',fill=BONE)
        for i,kind in enumerate(kinds[page*8:(page+1)*8]):
            tile=c.container(kind,count=len(CONTAINERS[kind])) if kind in CONTAINERS else c.art(kind)
            x=10+(i%4)*278; y=38+(i//4)*360
            d.text((x,y),kind,fill=BONE); sheet.paste(tile.resize((256,256),Image.Resampling.NEAREST),(x,y+20),tile.resize((256,256),Image.Resampling.NEAREST))
            native(sheet,tile,x,y+284)
        sheet.save(out/f'civilisation_objects_{page+1}.png')
    sheet=Image.new('RGB',(1450,45+len(CONTAINERS)*345),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Visible inventories | separate back + occupied slots + lid + front | 4x, native grass/dark/silhouette',fill=BONE)
    for row,kind in enumerate(CONTAINERS):
        n=len(CONTAINERS[kind]); y=40+row*345
        for col,(count,closed) in enumerate([(0,False),(max(1,n//3),False),(max(1,n*2//3),False),(n,False),(n,True)]):
            tile=c.container(kind,count=count,closed=closed); x=10+col*286
            d.text((x,y),f'{kind}: {count}'+(' covered' if closed else ' visible'),fill=BONE)
            sheet.paste(tile.resize((256,256),Image.Resampling.NEAREST),(x,y+18),tile.resize((256,256),Image.Resampling.NEAREST))
            native(sheet,tile,x,y+277)
    sheet.save(out/'civilisation_storage.png')
    example=json.loads((c.base/'inventory_example.json').read_text())
    sheet=Image.new('RGB',(1450,1490),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Inventory handoff example | six persistent stacks, 60 grain total in every row | 4x + native strips',fill=BONE)
    for row,snapshot in enumerate(example['snapshots']):
        y=38+row*360
        for col,(container_id,kind) in enumerate(example['containers'].items()):
            ids=snapshot[container_id]; inventory=[example['items'][i] for i in ids]; x=10+col*286
            tile=c.container(kind,inventory=inventory); large=tile.resize((256,256),Image.Resampling.NEAREST)
            d.text((x,y),f'{example["transfer_labels"][row]}: {container_id} ({len(ids)})',fill=BONE)
            sheet.paste(large,(x,y+20),large); native(sheet,tile,x,y+280)
        x=1154; d.text((x,y),'Backpack equipped, covered',fill=BONE)
        tile=c.container('backpack',inventory=[example['items'][i] for i in snapshot['backpack']],closed=True)
        tile.alpha_composite(c.art('person_detail')); large=tile.resize((256,256),Image.Resampling.NEAREST)
        sheet.paste(large,(x,y+20),large); native(sheet,tile,x,y+280)
    sheet.save(out/'civilisation_inventory_handoff.png')
    sheet=Image.new('RGB',(1140,55+len(ANIMATED)*350),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Active equipment | four frames, 6 fps | 4x and native background / silhouette strips',fill=BONE)
    for row,kind in enumerate(sorted(ANIMATED)):
        y=40+row*350
        for f in range(4):
            x=10+f*280; tile=c.art(kind,frame=f); large=tile.resize((256,256),Image.Resampling.NEAREST)
            d.text((x,y),f'{kind} frame {f}',fill=BONE); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
    sheet.save(out/'civilisation_animations.png')
    for material in WALLS:
        sheet=Image.new('RGB',(2160,750),DARK); d=ImageDraw.Draw(sheet)
        d.text((10,10),f'{material} walls | all 16 N/E/S/W masks | 4x + native strips',fill=BONE)
        for mask in range(16):
            x=8+(mask%8)*268; y=36+(mask//8)*350; tile=c.art(f'wall_{material}_{mask:02d}'); large=tile.resize((256,256),Image.Resampling.NEAREST)
            d.text((x,y),str(mask),fill=BONE); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
        sheet.save(out/f'civilisation_walls_{material}.png')
    surfaces=['floor_'+m for m in FLOORS]+['roof_'+m for m in ROOFS]
    sheet=Image.new('RGB',(1120,45+math.ceil(len(surfaces)/4)*350),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Repeatable floor and roof modules | 4x + native strips',fill=BONE)
    for i,name in enumerate(surfaces):
        x=10+(i%4)*278; y=36+(i//4)*350; tile=c.art(name); large=tile.resize((256,256),Image.Resampling.NEAREST)
        d.text((x,y),name,fill=BONE); sheet.paste(large,(x,y+18),large); native(sheet,tile,x,y+278)
    sheet.save(out/'civilisation_surfaces.png')
    for page in range(math.ceil(len(OUTDOOR)/4)):
        sheet=Image.new('RGB',(1120,780),DARK); d=ImageDraw.Draw(sheet)
        d.text((10,10),'Outdoor detail samples | 4x | native grass/dark/silhouette | static references',fill=BONE)
        for i,name in enumerate(list(OUTDOOR)[page*4:(page+1)*4]):
            tile=c.art(name); x=10+i*278; y=36; large=tile.resize((tile.width*4,tile.height*4),Image.Resampling.NEAREST)
            d.text((x,y),name,fill=BONE); sheet.paste(large,(x,y+20),large); native(sheet,tile,x,580)
        sheet.save(out/('civilisation_outdoors.png' if page==0 else f'civilisation_outdoors_{page+1}.png'))
    for page in range(math.ceil(len(VEHICLES)/3)):
        sheet=Image.new('RGB',(2400,760),DARK); d=ImageDraw.Draw(sheet)
        d.text((10,10),'Watercraft and land transport | 4x + native strips | separate container attachments supported',fill=BONE)
        for i,name in enumerate(list(VEHICLES)[page*3:(page+1)*3]):
            tile=c.art(name); x=10+i*795; large=tile.resize((tile.width*4,tile.height*4),Image.Resampling.NEAREST)
            d.text((x,38),name,fill=BONE); sheet.paste(large,(x,58),large); native(sheet,tile,x,590)
        sheet.save(out/f'civilisation_transport_{page+1}.png')
    sheet=Image.new('RGB',(2400,1510),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Separate cargo attachments | empty / loaded / foreground hull occlusion | actual payload is outside the base sprite',fill=BONE)
    for row,kind in enumerate(['cargo_ship','truck']):
        for col,loaded in enumerate([False,True]):
            tile=c.vehicle(kind,loaded=loaded); x=10+col*800; y=38+row*735; large=tile.resize((tile.width*4,tile.height*4),Image.Resampling.NEAREST)
            d.text((x,y),kind+(' loaded' if loaded else ' empty'),fill=BONE); sheet.paste(large,(x,y+20),large); native(sheet,tile,x,y+540)
    sheet.save(out/'civilisation_transport_cargo.png')
    sheet=Image.new('RGB',(1450,45+math.ceil(len(RESOURCES)/12)*125),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Foods and stored resources | 4x source / native grass-dark-silhouette | independently placed stacks',fill=BONE)
    for i,name in enumerate(RESOURCES):
        tile=c.art('stored_'+name); large=tile.resize((64,64),Image.Resampling.NEAREST); x=8+i%12*120; y=36+i//12*125
        d.text((x,y),name,fill=BONE); sheet.paste(large,(x,y+17),large); native(sheet,tile,x,y+85)
    sheet.save(out/'civilisation_resources.png')
    regions=json.loads((c.base/'regional_styles.json').read_text()); granary=next(p for p in plans if p['id']=='granary')
    sheet=Image.new('RGB',(1410,1360),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Regional granary material variants | same function / locally available construction resources | native close artwork',fill=BONE)
    for i,(name,style) in enumerate(regions['styles'].items()):
        plan={**granary,'wall':style['wall'],'floor':style['floor'],'roof':style['roof']}; tile=c.room(plan)
        x=10+i%3*466; y=36+i//3*440; d.text((x,y),name,fill=BONE); sheet.paste(tile.convert('RGB'),(x,y+18))
        d.text((x,y+407),'/'.join([style['wall'],style['roof'],style['floor']]),fill=BONE)
    sheet.save(out/'civilisation_regions.png')
    names=['temperate_forest.spring','temperate_forest.summer','temperate_forest.autumn','temperate_forest.winter','savanna.wet','savanna.dry']
    palette=json.loads((root/'assets/palette/palette.json').read_text()); lut=np.array(Image.open(root/'assets/palette/lut_nature.png'))
    sheet=Image.new('RGB',(1700,1500),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'Nature LUT samples | 4x | detail tree seasonal silhouette variants remain pending',fill=BONE)
    for row,kind in enumerate(['tree_broadleaf','tree_conifer','floor_grass']):
        idx=np.array(Image.open(c.base/f'{kind}_lod64_idx.png')); y=[42,600,1160][row]
        for col,name in enumerate(names):
            n=palette['luts']['lut_nature']['rows'].index(name); a=lut[n][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]
            tile=Image.fromarray(a); large=tile.resize((tile.width*4,tile.height*4),Image.Resampling.NEAREST); x=10+col*280
            d.text((x,y),kind+' / '+name,fill=BONE); sheet.paste(large,(x,y+20),large)
    sheet.save(out/'civilisation_nature.png')
    sheet=Image.new('RGB',(1580,1430),DARK); d=ImageDraw.Draw(sheet)
    d.text((10,10),'One world, three authored densities | same logical coordinates | final panel magnifies existing close pixels',fill=BONE)
    for i,density in enumerate([16,32,64]):
        x=10+(i%2)*785; y=40+(i//2)*690
        d.text((x,y),f'{density} px per tile, shown at equal scene extent',fill=BONE)
        im=c.world(density).resize((768,640),Image.Resampling.NEAREST); sheet.paste(im.convert('RGB'),(x,y+22))
    im=c.world(64).crop((64,64,256,256)).resize((640,640),Image.Resampling.NEAREST)
    d.text((795,730),'Close crop: individual grain piles and furniture',fill=BONE)
    sheet.paste(im.convert('RGB'),(795,752)); sheet.save(out/'civilisation_zoom.png')
    palette=json.loads((root/'assets/palette/palette.json').read_text()); idx=np.array(Image.open(c.base/'person_detail_lod64_idx.png'))
    recolours=Image.new('RGB',(1120,1200),DARK); d=ImageDraw.Draw(recolours)
    d.text((10,10),'Static scale reference | skin / hair / cloth LUT rows, 4x | culture console below',fill=BONE)
    yy=34
    for group,count in [('skin',8),('hair',8),('cloth',13)]:
        lut=np.array(Image.open(root/f'assets/palette/lut_{group}.png'))
        for row in range(count):
            a=lut[row][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]; im=Image.fromarray(a)
            x=8+row*82
            recolours.paste(im,(x,yy+18),im); d.text((x,yy),f'{group} {row}',fill=BONE)
        yy+=110
    idx=np.array(Image.open(c.base/'console_lod64_idx.png')); lut=np.array(Image.open(root/'assets/palette/lut_culture.png'))
    for row in range(12):
        a=lut[row][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]; im=Image.fromarray(a)
        x=8+row*90; recolours.paste(im,(x,yy+18),im); d.text((x,yy),f'culture {row}',fill=BONE)
    yy+=110
    for i,(group,row) in enumerate([('skin',0),('skin',7),('cloth',6),('cloth',9)]):
        idx=np.array(Image.open(c.base/'person_detail_lod64_idx.png')); lut=np.array(Image.open(root/f'assets/palette/lut_{group}.png'))
        a=lut[row][idx[:,:,0]].copy(); a[:,:,3]=idx[:,:,3]; im=Image.fromarray(a); large=im.resize((256,256),Image.Resampling.NEAREST)
        x=8+i*278; d.text((x,yy),f'{group} row {row}, 4x',fill=BONE); recolours.paste(large,(x,yy+20),large); native(recolours,im,x,yy+284)
    recolours.save(out/'civilisation_recolours.png')
