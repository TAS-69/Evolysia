
import numpy as np
from families.portraits import PARTS, PROFILES, ORDER, EXPRESSIONS
from portrait_preview import PortraitAssembly

def validate_portraits(root,manifest,check):
    a=PortraitAssembly(root); entries=a.entries
    check(len(entries)==14 and set(entries)==set(PARTS),'Portrait layer inventory')
    check(a.recipe['layer_order']==ORDER,'Portrait layer order')
    check(set(a.recipe['expressions'])==set(EXPRESSIONS) and len(EXPRESSIONS)==11,'Portrait emotion coverage')
    check(len(a.recipe['hair_styles'])==10,'Portrait hairstyle coverage')
    for emotion,parts in EXPRESSIONS.items():
        check(a.recipe['expressions'][emotion]==dict(zip(['brows','eyes','mouth'],parts)),f'Portrait expression mapping: {emotion}')
    for layer,e in entries.items():
        expected_profiles=[p for p in PROFILES if layer!='facial_hair' or p[0]!='child']
        check(e['frame']==[48,48] and e['frames']==1 and e['fps']==0,f'Portrait frame contract: {layer}')
        check(e['directions']==['three_quarter_left'],f'Portrait facing: {layer}')
        check(e['variants']==PARTS[layer] and e['profiles']==[list(p) for p in expected_profiles],f'Portrait variant/profile inventory: {layer}')
        check(len(e['row_lookup'])==len(e['variants']) and all(len(row)==len(expected_profiles) for row in e['row_lookup']),f'Portrait atlas lookup shape: {layer}')
        check({i for row in e['row_lookup'] for i in row}==set(range(len(e['sheet_rows']))),f'Unused portrait cells: {layer}')
        cells=[]
        for cell in range(len(e['sheet_rows'])):
            x=cell%e['atlas_columns']*48; y=cell//e['atlas_columns']*48
            tile=a.images[layer][y:y+48,x:x+48]; alpha=tile[:,:,3]>0
            if layer!='paint': check(np.any(alpha),f'Empty portrait part: {layer}:{cell}')
            check(not (alpha[0].any() or alpha[-1].any() or alpha[:,0].any() or alpha[:,-1].any()),f'Clipped portrait part: {layer}:{cell}')
            check(len(np.unique(tile[alpha,0]))<=16,f'Portrait part colour budget: {layer}:{cell}')
            cells.append(tile.tobytes())
        check(len(set(cells))==len(cells),f'Duplicate portrait atlas cells: {layer}')
    for profile in PROFILES:
        assembled=[]
        for emotion in EXPRESSIONS:
            idx=a.indices(profile,emotion); tile=np.array(a.reference(idx)); alpha=tile[:,:,3]>0
            check(np.count_nonzero(alpha)>900,f'Portrait silhouette too small: {profile}:{emotion}')
            check(len(np.unique(tile[alpha,:3],axis=0))<=16,f'Portrait assembled colour budget: {profile}:{emotion}')
            check(not np.any(np.all(tile[alpha,:3]==0,axis=1)),f'Black portrait pixel: {profile}:{emotion}')
            assembled.append(tile.tobytes())
        check(len(set(assembled))==11,f'Indistinguishable portrait expressions: {profile}')
        for layer,variants in PARTS.items():
            if layer in {'bg','head','ears','mouth','hair_back','hair_front','brows','eyes'}: continue
            if layer=='facial_hair' and profile[0]=='child': continue
            for variant in variants:
                choices={layer:variant}
                if layer=='paint': choices['shoulders']='none'
                composed=a.indices(profile,choices=choices)
                ref=np.array(a.reference(composed)); active=ref[:,:,3]>0
                check(len(np.unique(ref[active,:3],axis=0))<=16,f'Portrait part assembly colour budget: {profile}:{layer}:{variant}')
        for style in a.recipe['hair_styles']:
            composed=a.indices(profile,choices={'hair':style})
            ref=np.array(a.reference(composed)); active=ref[:,:,3]>0
            check(len(np.unique(ref[active,:3],axis=0))<=16,f'Portrait hair colour budget: {profile}:{style}')
        for shape in ['almond','rounded','hooded','deep_set','upturned','downturned']:
            for iris in ['brown','amber','green','blue','grey']:
                check(np.any(a.indices(profile,choices={'eye_shape':shape,'iris':iris})[:,:,3]),f'Invalid eye assembly: {profile}:{shape}:{iris}')
        plain=a.indices(profile,choices={'hair':'shaved'})
        scar=a.indices(profile,choices={'hair':'shaved','scars':'missing_ear'})
        check(not np.array_equal(plain,scar),f'Missing-ear scar invisible: {profile}')
        before=a.tile('ears','base',profile)[:,:,3]>0
        polygon=a.recipe['scar_operations']['missing_ear']['polygon_by_profile']['.'.join(profile)]
        from PIL import Image, ImageDraw
        mask=Image.new('L',(48,48)); ImageDraw.Draw(mask).polygon(polygon,fill=255)
        check(np.count_nonzero(before & (np.array(mask)>0))>=3,f'Missing-ear cutout misses ear: {profile}')
    for age in ['child','adult','elder']:
        check((root/f'assets/_preview/portraits_expressions_{age}.png').exists(),f'Missing portrait expression preview: {age}')
    for name in ['head_shapes','lut_skin','lut_hair','lut_cloth','lut_culture','shoulders','nose',
                 'facial_hair','scars','paint','accessory','hair_styles','eye_shapes','brow_shapes','overview']:
        check((root/f'assets/_preview/portraits_{name}.png').exists(),f'Missing portrait preview: {name}')
