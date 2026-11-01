
import json
import hashlib
import math
from pathlib import Path
import numpy as np
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]

def main():
    errors=[]
    def check(condition,message):
        if not condition: errors.append(message)
    data=json.loads((ROOT/'assets/palette/palette.json').read_text())
    manifest=json.loads((ROOT/'assets/manifest.json').read_text())
    font_directory=ROOT/'assets/fonts'
    font_registry=json.loads((font_directory/'sources.json').read_text(encoding='utf-8'))
    check(len(font_registry['fonts'])==4,'Expected four specified font files')
    check({font['file'] for font in font_registry['fonts']}=={'PixelifySans.ttf','AlegreyaSans-Regular.ttf','AlegreyaSans-Bold.ttf','AlegreyaSans-Italic.ttf'},'Unexpected font family or face')
    for font in font_registry['fonts']:
        path=font_directory/font['file']; licence=font_directory/font['license_file']
        check(path.is_file(),f'Missing font: {path.name}')
        if path.is_file(): check(hashlib.sha256(path.read_bytes()).hexdigest()==font['sha256'],f'Font checksum mismatch: {path.name}')
        check(licence.is_file() and 'SIL OPEN FONT LICENSE' in licence.read_text(encoding='utf-8'),f'Missing OFL notice: {font["file"]}')
    for licence in font_registry['licenses']:
        path=font_directory/licence['file']
        check(path.is_file() and hashlib.sha256(path.read_bytes()).hexdigest()==licence['sha256'],f'Font licence changed: {path.name}')
    colours={tuple(c) for c in data['colours']}
    slots=data['slots']; indices={s['index'] for s in slots}
    reference_lookup=np.zeros((256,3),dtype=np.uint8)
    valid_indices=np.zeros(256,dtype=bool)
    for slot in slots:
        reference_lookup[slot['index']]=slot['reference']; valid_indices[slot['index']]=True
    palette_codes={r*65536+g*256+b for r,g,b in colours}
    check(len(indices)==len(slots) and indices==set(range(len(slots))) and len(slots)<=256,'Invalid slot registry')
    check(len({s['name'] for s in slots})==len(slots),'Duplicate slot names')
    for name,ramp in data['ramps'].items():
        check(all(tuple(c) in colours for c in ramp),f'Ramp missing from palette: {name}')
    ids=set(); files=set()
    for entry in manifest['assets']:
        ident=entry['id']
        check(ident not in ids,f'Duplicate ID: {ident}'); ids.add(ident)
        w,h=entry['frame']; x,y=entry['pivot']
        check(0<=x<w and 0<=y<h,f'Pivot outside frame: {ident}')
        side=json.loads((ROOT/'assets'/entry['sidecar']).read_text())
        for key in ['frame','pivot','directions','frames','fps','files','sheet_rows','recolor_slots','anims','seasonal_states','wind_sway','composite_over','animation','mirror_left','coat_lut','form','source_species','row_lookup','hand_anchors','row_pivots','profiles','variants','sim_actions','clip_rule','atlas_columns','asset_id','state','lighting','materials','emissive_slots','glyph_anchor','fire_window','logical_frame','logical_pivot','density','detail_source','kind','connection_mask','connection_bits','container_id','layer','resource_id']:
            if key not in entry: continue
            check(side[key]==entry[key],f'Sidecar differs: {ident}.{key}')
        if 'placement_footprint' in entry: check(side['placement_footprint']==entry['placement_footprint'],f'Sidecar footprint differs: {ident}')
        for key in ['strip_columns','strip_count','dye_channels','per_piece_dyes','priority_anchor','layer_priority','priority_offset','mask_bits','masks','orientations','homogeneous_mask_15']:
            if key in entry: check(side[key]==entry[key],f'Sidecar differs: {ident}.{key}')
        for file in entry['files']:
            check(file not in files,f'Duplicate output: {file}'); files.add(file)
            image=Image.open(ROOT/'assets'/file)
            check(image.mode=='RGBA',f'Not PNG RGBA: {file}')
            a=np.asarray(image)
            row_count=entry['strip_count'] if 'strip_count' in entry else len(entry.get('sheet_rows',entry['directions']))
            if 'strip_columns' in entry: expected_size=(w*entry['frames']*entry['strip_columns'],h*math.ceil(row_count/entry['strip_columns']))
            elif 'atlas_columns' in entry: expected_size=(w*entry['atlas_columns'],h*math.ceil(row_count/entry['atlas_columns']))
            else: expected_size=(w*entry['frames'],h*row_count)
            check(image.size==expected_size,f'Sheet dimensions: {file}')
            check(bool(np.isin(a[:,:,3],[0,255]).all()),f'Partial alpha: {file}')
            visible=a[a[:,:,3]>0]
            if file.endswith('_idx.png'):
                check(bool(valid_indices[visible[:,0]].all() and (visible[:,1:3]==0).all()),f'Invalid index: {file}')
                ref=np.asarray(Image.open(ROOT/'assets'/file.replace('_idx.png','.png')))
                check(bool(np.array_equal(a[:,:,3],ref[:,:,3])),f'Index alpha mismatch: {file}')
                check(bool(np.array_equal(ref[a[:,:,3]>0,:3],reference_lookup[visible[:,0]])),f'Index/reference colour mismatch: {file}')
            else:
                rgb=visible[:,:3].astype(np.uint32); codes=np.unique(rgb[:,0]*65536+rgb[:,1]*256+rgb[:,2])
                check(all(int(c) in palette_codes for c in codes),f'Off-palette: {file}')
    for png in (ROOT/'assets').rglob('*.png'):
        relative=png.relative_to(ROOT/'assets').as_posix()
        if not relative.startswith('_preview/'):
            check(relative in files,f'PNG missing from manifest: {relative}')
    if any(e['family']=='terrain' for e in manifest['assets']):
        from terrain_checks import validate_terrain
        validate_terrain(ROOT,manifest,check)
    if any(e['family']=='overlays' for e in manifest['assets']):
        from overlay_checks import validate_overlays
        validate_overlays(ROOT,manifest,check)
    if any(e['family']=='flora' for e in manifest['assets']):
        from flora_checks import validate_flora
        validate_flora(ROOT,manifest,check)
    if any(e['family']=='fauna' for e in manifest['assets']):
        from fauna_checks import validate_fauna
        validate_fauna(ROOT,manifest,check)
    if any(e['family']=='people' for e in manifest['assets']):
        from people_checks import validate_people
        validate_people(ROOT,manifest,check)
    if any(e['family']=='portraits' for e in manifest['assets']):
        from portrait_checks import validate_portraits
        validate_portraits(ROOT,manifest,check)
    if any(e['family']=='structures' for e in manifest['assets']):
        from structure_checks import validate_structures
        validate_structures(ROOT,manifest,check)
    if any(e['family']=='civilisation' for e in manifest['assets']):
        from civilisation_checks import validate_civilisation
        validate_civilisation(ROOT,manifest,check)
    if any(e['family']=='wardrobe' for e in manifest['assets']):
        from wardrobe_checks import validate_wardrobe
        validate_wardrobe(ROOT,manifest,check)
    if any(e['family']=='landscape' for e in manifest['assets']):
        from landscape_checks import validate_landscape
        validate_landscape(ROOT,manifest,check)
    for name,meta in data['luts'].items():
        a=np.asarray(Image.open(ROOT/f'assets/palette/{name}.png'))
        check(len(set(meta['rows']))==len(meta['rows']),f'Duplicate LUT labels: {name}')
        check(bool((a[:,:len(slots),3]==255).all()),f'Missing LUT slots: {name}')
        check(bool((a[:,len(slots):,3]==0).all()),f'Unreserved slots: {name}')
        for slot in slots:
            if slot['name'].startswith('fixed_'):
                check(bool((a[:,slot['index'],:3]==slot['reference']).all()),f'Fixed colour changed: {name}')
        preview=ROOT/f'assets/_preview/{name}.png'
        check(preview.exists(),f'Missing preview: {name}')
    nature=data['luts']['lut_nature']['rows']
    check(len(nature)==70,'Expected 70 biome-season rows')
    check(set(data['luts'])=={'lut_nature','lut_skin','lut_hair','lut_cloth','lut_culture'},'Missing required LUT')
    for kind,count in [('skin',8),('hair',8),('cloth',13),('culture',12)]:
        check(len(data['luts'][f'lut_{kind}']['rows'])==count,f'Wrong {kind} row count')
    def luminance(value):
        c=np.array(list(bytes.fromhex(value.lstrip('#'))))/255
        c=np.where(c<=.04045,c/12.92,((c+.055)/1.055)**2.4)
        return float(c @ np.array([.2126,.7152,.0722]))
    for colour in ['bone','parchment']:
        ratio=(luminance(data['ui'][colour])+.05)/(luminance(data['ui']['panel'])+.05)
        check(ratio>=4.5,f'UI contrast below AA: {colour}')
    gpl=(ROOT/'assets/palette/evolysia.gpl').read_text().splitlines()[4:]
    check([tuple(map(int,line.split()[:3])) for line in gpl]==data_colours(data),'GPL differs from JSON')
    for file in (ROOT/'assets/_preview').glob('*.png'):
        check(file.stat().st_size<=8*1024*1024,f'Preview exceeds 8 MB: {file.name}')
    if errors:
        print('\n'.join('ERROR: '+e for e in errors)); raise SystemExit(1)
    print(f'PASS: zero errors; {len(ids)} assets, {len(colours)} colours, {len(slots)} slots, 70 nature rows')

def data_colours(data): return [tuple(c) for c in data['colours']]

if __name__=='__main__': main()
