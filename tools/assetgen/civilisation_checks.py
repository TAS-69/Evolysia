"""Detail-density, modular seams, storage composition and layout checks."""
import json
from collections import deque
import numpy as np
from PIL import Image
from families.civilisation import DENSITIES, FLOORS, WALLS, ROOFS, OBJECTS, CONTAINERS, RESOURCES, ANIMATED, OUTDOOR, VEHICLES
from civilisation_preview import Composer

def validate_civilisation(root,manifest,check):
    group=[e for e in manifest['assets'] if e['family']=='civilisation']; base=root/'assets/civilisation'
    entries={(e['asset_id'],e['density']):e for e in group}
    expected=set('floor_'+m for m in FLOORS)|{f'wall_{m}_{mask:02d}' for m in WALLS for mask in range(16)}|{'roof_'+m for m in ROOFS}|{k+'_'+layer for k in CONTAINERS for layer in ['back','front','lid']}|{'stored_'+k for k in RESOURCES}|set(OBJECTS)|{'person_detail'}|set(OUTDOOR)|set(VEHICLES)
    check(set(entries)=={(name,density) for name in expected for density in DENSITIES},'Civilisation detail inventory mismatch')
    check(len(group)==len(entries),'Duplicate civilisation detail pair')
    for e in group:
        name=e['asset_id']; density=e['density']; descriptor=OUTDOOR.get(name,VEHICLES.get(name)); canonical=([16,16] if e['kind']=='stored_item' else descriptor[0] if descriptor else [64,64])
        w,h=[n*density//64 for n in canonical]; frames=4 if name in ANIMATED else 1
        check(e['frame']==[w,h] and e['frames']==frames and e['directions']==['none'],f'Detail frame contract: {name}:{density}')
        logical=[4,4] if e['kind']=='stored_item' else descriptor[1] if descriptor else [16,16]
        check(e['logical_frame']==logical,f'Detail logical frame: {name}')
        check(e['pivot']==[int(v*density/16) for v in e['logical_pivot']],f'Detail pivot density drift: {name}:{density}')
        a=np.array(Image.open(root/'assets'/e['files'][0])); unique=[]
        for frame in range(frames):
            tile=a[:,frame*w:(frame+1)*w]; visible=tile[:,:,3]>0
            check(visible.any(),f'Invisible detail asset: {name}:{density}')
            check(len(np.unique(tile[visible,:3],axis=0))<=16,f'Detail colour budget: {name}:{density}')
            check(not np.any(np.all(tile[visible,:3]==0,axis=1)),f'Pure black detail pixel: {name}')
            unique.append(tile.tobytes())
            if e['kind'] in {'floor','roof'}:
                check(np.array_equal(tile[0],tile[-1]) and np.array_equal(tile[:,0],tile[:,-1]),f'Detail surface seam: {name}:{density}')
        check(len(set(unique))==frames,f'Duplicate active detail frames: {name}:{density}')
        if density!=64:
            source=Image.open(base/f'{name}_lod64_idx.png'); expected_idx=[]
            for f in range(frames):
                cw,ch=canonical; tile=np.array(source.crop((f*cw,0,(f+1)*cw,ch)).resize((w,h),Image.Resampling.NEAREST))
                if e['kind'] in {'floor','roof'}: tile[-1]=tile[0]; tile[:,-1]=tile[:,0]
                expected_idx.append(tile)
            check(np.array_equal(np.concatenate(expected_idx,axis=1),np.array(Image.open(root/'assets'/e['files'][1]))),f'Detail LOD mapping mismatch: {name}:{density}')
    for material in WALLS:
        for density in DENSITIES:
            tiles=[np.array(Image.open(base/f'wall_{material}_{mask:02d}_lod{density}_idx.png')) for mask in range(16)]
            check(len({tile.tobytes() for tile in tiles})==16,f'Duplicate wall masks: {material}:{density}')
            for a in range(16):
                for b in range(16):
                    if bool(a&2)==bool(b&8): check(np.array_equal(tiles[a][:,-1],tiles[b][:,0]),f'Wall E/W seam: {material}:{density}:{a}:{b}')
                    if bool(a&4)==bool(b&1): check(np.array_equal(tiles[a][-1],tiles[b][0]),f'Wall S/N seam: {material}:{density}:{a}:{b}')
            rng=np.random.default_rng(412); horizontal=rng.integers(0,2,(12,11)); vertical=rng.integers(0,2,(11,12))
            masks=np.zeros((12,12),dtype=int); masks[:,:-1]|=horizontal*2; masks[:,1:]|=horizontal*8
            masks[:-1]|=vertical*4; masks[1:]|=vertical
            for y in range(12):
                for x in range(12):
                    a=masks[y,x]
                    if x<11: check(np.array_equal(tiles[a][:,-1],tiles[masks[y,x+1]][:,0]),f'Random wall tiling E/W: {material}:{density}:{x}:{y}')
                    if y<11: check(np.array_equal(tiles[a][-1],tiles[masks[y+1,x]][0]),f'Random wall tiling S/N: {material}:{density}:{x}:{y}')
    c=Composer(root)
    for kind,anchors in CONTAINERS.items():
        p=c.storage[kind]; check(p['slot_anchors_px64']==[list(a) for a in anchors] and p['visible_slots']==len(anchors),f'Storage slot metadata: {kind}')
        check(len(set(anchors))==len(anchors),f'Duplicate storage anchors: {kind}')
        for x,y in anchors: check(0<=x<=48 and 0<=y<=48,f'Storage anchor outside cell: {kind}')
        states=[c.container(kind,count=count).tobytes() for count in range(len(anchors)+1)]
        check(len(set(states))==len(states),f'Hidden inventory count transition: {kind}')
        closed=c.container(kind,count=0,closed=True).tobytes()
        check(c.container(kind,count=len(anchors),closed=True).tobytes()==closed,f'Closed cover leaks contents: {kind}')
        for resource in RESOURCES:
            check(c.container(kind,resource,count=1).tobytes()!=c.container(kind,count=0).tobytes(),f'Invisible stored resource: {kind}:{resource}')
            check(c.container(kind,resource,count=len(anchors),closed=True).tobytes()==closed,f'Covered resource leaks: {kind}:{resource}')
        mixed=[{'resource_id':resource} for resource in list(RESOURCES)[:len(anchors)]]
        check(c.container(kind,inventory=mixed).tobytes()!=c.container(kind).tobytes(),f'Mixed inventory not rendered: {kind}')
    check(set(c.vehicles)==set(VEHICLES),'Vehicle attachment profile inventory')
    for kind,profile in c.vehicles.items():
        check(profile['payload_in_base_png'] is False and profile['frame']==VEHICLES[kind][0],f'Vehicle payload contract: {kind}')
        for slot in profile['attachment_slots']: check(slot['container'] in CONTAINERS,f'Unresolved vehicle container: {kind}')
        if profile['attachment_slots']:
            for density in DENSITIES: check(c.vehicle(kind,density,True).tobytes()!=c.vehicle(kind,density,False).tobytes(),f'Invisible separate vehicle payload: {kind}:{density}')
    example=json.loads((base/'inventory_example.json').read_text()); items=example['items']
    for snapshot in example['snapshots']:
        located=[item for inventory in snapshot.values() for item in inventory]
        check(len(located)==len(set(located)) and set(located)==set(items),'Inventory example loses or duplicates persistent items')
        check(sum(items[item]['quantity'] for item in located)==60,'Inventory example changes grain quantity')
        for container_id,inventory in snapshot.items():
            check(len(inventory)<=len(CONTAINERS[example['containers'][container_id]]),'Inventory example exceeds display slots')
    plans=json.loads((base/'blueprints.json').read_text())['blueprints']; names=set()
    for p in plans:
        check(p['id'] not in names,f'Duplicate room ID: {p["id"]}'); names.add(p['id'])
        w,h=p['size_cells']; occupied=set()
        for obj in p['objects']+p['actors']:
            kind=obj['asset_id']; x,y=obj['cell']; cell=(x,y)
            check(0<x<w-1 and 0<y<h-1 and cell not in occupied,f'Invalid/overlapping room placement: {p["id"]}:{kind}')
            occupied.add(cell)
            check(kind in CONTAINERS or (kind,64) in entries,f'Unresolved room object: {p["id"]}:{kind}')
            if kind in CONTAINERS:
                check(obj.get('resource') in RESOURCES and 0<=obj.get('count',0)<=len(CONTAINERS[kind]),f'Invalid room inventory: {p["id"]}:{kind}')
        walkable={tuple(cell) for cell in p['walkable_cells']}; start=tuple(p['entrance_cell']); queue=deque([start]); seen={start}
        while queue:
            x,y=queue.popleft()
            for dx,dy in [(0,1),(1,0),(0,-1),(-1,0)]:
                v=(x+dx,y+dy)
                if v in walkable and v not in seen: seen.add(v); queue.append(v)
        check(seen==walkable,f'Disconnected sample floor layout: {p["id"]}')
        for obj in p['objects']:
            x,y=obj['cell']
            check(any((x+dx,y+dy) in seen for dx,dy in [(0,1),(1,0),(0,-1),(-1,0)]),f'Unreachable sample equipment: {p["id"]}:{obj["asset_id"]}')
        check((root/f'assets/_preview/civilisation_room_{p["id"]}.png').exists(),f'Missing detail room preview: {p["id"]}')
        for density in DENSITIES:
            scene=c.room(p,density); check(scene.size==(w*density,h*density),f'Room density footprint drift: {p["id"]}:{density}')
    catalogue=json.loads((base/'catalogue.json').read_text())['entries']
    ready=set(OBJECTS)|set(CONTAINERS)|names|set(RESOURCES)|set(OUTDOOR)|set(VEHICLES)|{'wall_'+m for m in WALLS}|{'floor_'+m for m in FLOORS}|{'roof_'+m for m in ROOFS}
    for row in catalogue:
        if row.get('source_family')=='landscape':
            from families.landscape import MATERIALS as LAND_MATERIALS, CLIFFS
            selector=row['visual_selector']
            check(row['art_status']=='separate_family_ready' and selector['asset_id'] in LAND_MATERIALS and selector['kind'] in {'plain','cliff_corners'},f'Unresolved separate-family coverage: {row["id"]}')
            if selector['kind']=='cliff_corners': check(selector['asset_id'] in CLIFFS,f'Unresolved landscape cliff coverage: {row["id"]}')
        else: check(row['art_status']==('sample_ready' if row['id'] in ready else 'planned'),f'Coverage falsely claims completed art: {row["id"]}')
    contract=json.loads((base/'render_contract.json').read_text())
    check(contract['logical_tile_px']==16 and contract['authored_density']==64 and contract['densities']==[16,32,64],'Detail render density contract')
    check(contract['generated_visual_ids']==sorted(expected),'Detail render inventory contract')
    regions=json.loads((base/'regional_styles.json').read_text())
    for style in regions['styles'].values():
        check(style['floor'] in FLOORS and style['wall'] in WALLS and style['roof'] in ROOFS,'Unresolved regional building material')
    for variant in regions['variants']:
        check(variant['base_blueprint'] in names and variant['regional_style'] in regions['styles'],'Unresolved regional building variant')
    check(set(regions['wall_resource_categories'])==set(WALLS),'Wall construction resource categories incomplete')
    for name in ['overview','storage','animations','surfaces','recolours','outdoors','zoom','inventory_handoff','nature','regions','resources','transport_1','transport_cargo']:
        check((root/f'assets/_preview/civilisation_{name}.png').exists(),f'Missing detailed family review: {name}')
