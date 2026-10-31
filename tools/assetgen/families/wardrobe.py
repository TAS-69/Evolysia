
import json
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
from .people import ANIMS, profiles, pose
from .civilisation import Pixels, DENSITIES
from .terrain import stable

ROOT=Path(__file__).resolve().parents[3]
TOPS=['hide_wrap','woven_tunic','linen_shirt','wrap_shirt','long_tunic','work_shirt','farmer_smock','fisher_coat','hunter_vest','artisan_apron','smith_apron','cook_apron','healer_coat','scholar_robe','traveller_coat','merchant_coat','guard_tabard','ceremonial_robe','royal_robe','court_coat','winter_parka','fur_coat','desert_robe','raincoat','sailor_shirt','mechanic_coverall','engineer_jacket','lab_coat','formal_jacket','flight_suit','environment_suit','mage_robe']
LEGS=['shorts','trousers','loose_trousers','skirt','long_skirt','wrap_skirt','leggings','fur_leggings','work_trousers','formal_trousers','padded_trousers','environment_trousers']
FEET=['sandals','soft_shoes','leather_boots','fur_boots','work_boots','metal_boots','cloth_wraps','ceremonial_shoes','environment_boots','water_shoes']
OUTER=['short_cape','long_cape','hooded_cloak','fur_mantle','rain_cape','desert_shawl','traveller_mantle','ceremonial_mantle','royal_mantle','officer_coat','artisan_overcoat','insulated_overcoat']
ARMOUR=['padded','hide','bone','wood_lamellar','woven_guard','leather_scale','copper_scale','iron_chain','iron_plate','ceramic_plate','composite','powered']
HEAD=['cloth_cap','woven_hat','wide_brim_hat','fur_hat','hood','headwrap','veil','feather_band','beaded_band','ceremonial_headdress','royal_circlet','royal_crown','leather_helmet','bone_helmet','copper_helmet','iron_helmet','guard_helmet','visor_helmet','work_helmet','cook_hat','scholar_cap','respirator_hood','environment_helmet','mage_hat']
ACCESS=['belt_pouch','necklace','beads','scarf','spectacles','goggles','face_wrap','respirator','backpack_straps','tool_belt','ceremonial_sash','royal_chain']
HELD=['club','spear','stone_axe','axe','pick','hoe','shovel','hammer','chisel','knife','sword','mace','bow','crossbow','shield','net','fishing_rod','basket','torch','staff','orb_focus','wand','medicine_bag','book','measuring_rod','wrench','tongs','rifle','pistol','energy_tool','energy_blade','construction_plan']
HAIR=['short','cropped','long','braided','topknot','mane','tied_back','locks','child_tuft','side_swept','curly','twin_braids','braid_crown','high_tail','bob','coils']
PARTS={'hair_back':HAIR,'body':['base'],'legwear':LEGS,'footwear':FEET,'top':TOPS,'outerwear':OUTER,'armour':ARMOUR,'body_paint':['cheek_dots','brow_band','arm_bands','leg_marks','face_streak','shoulder_dabs'],'hair_front':HAIR,'headwear':HEAD,'accessory':ACCESS,'held_item':HELD}
ORDER=list(PARTS)

def form(p):
    hx,hy=p['head']; l,t,r,hip=p['torso']
    if p['lying']: return (hx*4,hy*4),(hx*4+8,56-(r-l+1)*4,40,55)
    return (hx*4,hy*4),(l*4-1,t*4,(r+1)*4-1,hip*4+2)

def part(layer,style,p,anim,f,seed,slots):
    q=Pixels(slots); (hx,hy),(l,t,r,b)=form(p); cx=(l+r)//2; side=p['direction']=='side'; up=p['direction']=='up'; lying=p['lying']
    hands=[tuple(n*4 for n in v) for v in p['hands']]; feet=[tuple(n*4 for n in v) for v in p['feet']]
    def line(points,ramp,shade,width=1): q.line(points,ramp,shade,width)
    def rect(box,ramp,shade):
        x0,y0,x1,y1=map(int,box)
        if x0<=x1 and y0<=y1: q.rect((x0,y0,x1,y1),ramp,shade)
    def clothbox(box):
        rect(box,'cloth_a',2); x0,y0,x1,y1=box
        line([(x0,y1),(x1,y1),(x1,y0)],'cloth_a',0)
        line([(x0,y1-1),(x0,y0),(x1-1,y0)],'cloth_a',3)
    if layer=='body':
        q.ellipse((hx-6,hy,hx+6,hy+11),'skin',2)
        q.ellipse((hx-5,hy+1,hx,hy+8),'skin',3)
        if side: q.poly([(hx+5,hy+5),(hx+9,hy+7),(hx+5,hy+9)],'skin',2)
        rect((l,t,r,b),'skin',2); line([(l,t),(l,b)],'skin',3,2)
        if not lying: line([(hx,hy+9),(cx,t+2)],'skin',2,4)
        if anim!='swim':
            for i,(x,y) in enumerate(feet):
                origin=(r,b-2) if lying else (l+2+i*max(1,r-l-4),b-1)
                line([origin,(x,y)],'skin',2 if i==0 else 0,4)
                rect((x-2,y-1,x+2,y+2),'skin',2)
        for i,(x,y) in enumerate(hands):
            origin=(l+2,t+2) if lying else (l if i==0 else r,t+2)
            line([origin,(x,y)],'skin',2,4); line([(origin[0]-1,origin[1]),(x-1,y-1)],'skin',3)
            rect((x-2,y-1,x+2,y+2),'skin',2)
        if not up:
            for x in ([hx+3] if side else [hx-3,hx+3]): rect((x,hy+5,x+1,hy+6),'skin',0)
            line([(hx,hy+9),(hx+3,hy+9)],'skin',0)
    elif layer in {'hair_back','hair_front'}:
        number=HAIR.index(style)
        if layer=='hair_back':
            if style in {'long','braided','mane','locks','twin_braids','high_tail','bob','coils'}:
                length=16 if style in {'long','locks','braided','twin_braids'} else 10
                for x in [hx-8,hx+6]:
                    rect((x,hy+3,x+3,hy+length),'hair',1)
                    if style in {'braided','locks','twin_braids','coils'}:
                        for y in range(hy+5,hy+length,4): rect((x-1,y,x+2,y+1),'hair',3)
            elif style in {'tied_back','high_tail','topknot'}: q.ellipse((hx+5,hy-2,hx+11,hy+7),'hair',1)
        else:
            q.ellipse((hx-7,hy-3,hx+6,hy+4),'hair',1)
            line([(hx-5,hy),(hx-1,hy-2),(hx+3,hy-1)],'hair',3,2)
            if style in {'long','bob','mane','locks'}: rect((hx-7,hy+1,hx-4,hy+8),'hair',1)
            if style in {'topknot','high_tail','braid_crown'}: q.ellipse((hx-2,hy-8,hx+5,hy-2),'hair',1)
            if style in {'curly','coils','mane'}:
                for dx,dy in [(-7,0),(-3,-4),(3,-3),(6,1)]: q.ellipse((hx+dx-2,hy+dy-2,hx+dx+3,hy+dy+3),'hair',1)
            if style in {'side_swept','child_tuft','tied_back'}: q.poly([(hx-5,hy),(hx+7,hy-2),(hx+4,hy+5)],'hair',3)
            if up: q.ellipse((hx-6,hy,hx+6,hy+10),'hair',1)
            if number%3==1: line([(hx-3,hy),(hx+1,hy+2)],'hair',3)
    elif layer=='body_paint':
        points={'cheek_dots':[(hx-4,hy+8),(hx+4,hy+8)],'brow_band':[(hx-3,hy+3),(hx,hy+3),(hx+3,hy+3)],'arm_bands':hands,'leg_marks':feet,'face_streak':[(hx,hy+5),(hx,hy+8)],'shoulder_dabs':[(l,t+3),(r,t+3)]}[style]
        for x,y in points: rect((x-1,y,x+1,y+1),'culture',2)
        body=np.array(part('body','base',p,anim,f,seed,slots)); a=np.array(q.im); a[body==255]=255; q.im=Image.fromarray(a)
    elif layer=='legwear' and anim!='swim':
        if 'skirt' in style:
            bottom=min(58,max(y for x,y in feet)+(1 if style=='long_skirt' else -3 if style=='wrap_skirt' else -1))
            rect((l-1,b-4,r+1,bottom),'cloth_a',2); line([(l,b-3),(l,bottom)],'cloth_a',3)
            for x in range(l+3,r+1,4): line([(x,b-2),(x+1,bottom)],'cloth_a',0)
            line([(l,bottom),(r,bottom)],'cloth_b',3)
            if style=='wrap_skirt': line([(l,t+5),(r,bottom-1)],'cloth_b',3,2)
        else:
            for i,(x,y) in enumerate(feet):
                start=(r-2,b-3) if lying else (l+2+i*max(1,r-l-4),b-3)
                end=(x-(2 if lying else 0),y-(5 if style=='shorts' else 1))
                if style=='shorts': end=((start[0]+x)//2,(start[1]+y)//2)
                width=8 if style in {'loose_trousers','fur_leggings'} else 4 if style=='leggings' else 6
                line([start,end],'cloth_a',0,width); line([(start[0]-1,start[1]),(end[0]-1,end[1])],'cloth_a',2,max(2,width-2))
                if style in {'work_trousers','environment_trousers'}: rect((end[0]-2,end[1]-5,end[0]+2,end[1]-2),'cloth_b',3)
                if style=='formal_trousers': line([(start[0],start[1]),(end[0],end[1])],'cloth_b',1)
                if style=='padded_trousers':
                    for dy in [3,7]: line([(end[0]-2,end[1]-dy),(end[0]+2,end[1]-dy)],'cloth_b',1)
    elif layer=='footwear' and anim!='swim':
        for x,y in feet:
            height=7 if 'boot' in style else 3
            rect((x-3,y-height,x+3,y+2),'cloth_a',0)
            rect((x-2,y-height,x+2,y),'cloth_a',2)
            line([(x-2,y-height),(x+1,y-height)],'cloth_b',3)
            if 'sandal' in style or style=='cloth_wraps':
                for dy in [0,3]: line([(x-2,y-dy),(x+2,y-dy)],'cloth_b',3)
            if style=='fur_boots':
                for dx in [-2,0,2]: rect((x+dx,y-height-1,x+dx+1,y-height+2),'cloth_b',3)
            if style in {'metal_boots','environment_boots'}: rect((x-2,y-5,x+2,y-3),'cloth_b',3)
    elif layer=='top':
        isrobe='robe' in style or style in {'long_tunic','lab_coat','raincoat','winter_parka','fur_coat'}
        bottom=min(56,b+(5 if isrobe and not lying else 0))
        clothbox((l-1,t-1,r+1,bottom))
        if style=='hide_wrap':
            q.poly([(l-1,t-1),(l+4,t-1),(r+1,bottom),(l,bottom)],'cloth_b',1)
        elif not up:
            line([(cx-2,t),(cx,t+4),(cx+2,t)],'cloth_b',3)
        if style not in {'hunter_vest','guard_tabard','hide_wrap','artisan_apron','smith_apron','cook_apron'}:
            for i,(x,y) in enumerate(hands):
                start=(l,t+1) if i==0 else (r,t+1); end=((start[0]+x*2)//3,(start[1]+y*2)//3)
                line([start,end],'cloth_a',0,5); line([(start[0]-1,start[1]-1),(end[0]-1,end[1]-1)],'cloth_a',2,3)
                line([(end[0]-1,end[1]),(end[0]+1,end[1])],'cloth_b',3)
        if 'apron' in style:
            rect((l+2,t+4,r-2,bottom+2),'cloth_b',1)
            rect((l+3,t+8,r-3,t+10),'cloth_b',3)
            line([(l,t+3),(r,b-2)],'cloth_b',3)
        elif style in {'sailor_shirt','work_shirt','farmer_smock','woven_tunic'}:
            for y in range(t+6,bottom,5): line([(l+1,y),(r-1,y)],'cloth_b',1)
        elif style in {'ceremonial_robe','royal_robe','mage_robe','scholar_robe'}:
            line([(l,t),(l,bottom),(r,bottom),(r,t)],'cloth_b',3,2)
            q.poly([(cx,t+7),(cx+2,t+10),(cx,t+13),(cx-2,t+10)],'cloth_b',3)
        elif style in {'mechanic_coverall','engineer_jacket','environment_suit','flight_suit','lab_coat'}:
            line([(cx,t+3),(cx,bottom-1)],'cloth_b',1)
            rect((l+2,t+6,l+5,t+9),'cloth_b',3)
            rect((r-5,t+7,r-2,t+9),'cloth_b',1)
        else:
            line([(l,b-2),(r,b-2)],'cloth_b',1)
            for y in range(t+6,bottom-2,4): rect((cx,y,cx+1,y+1),'cloth_b',3)
        if style in {'fur_coat','winter_parka'}:
            for x in range(l,r+1,3): rect((x,t-1,x+1,t+2),'cloth_b',3)
        a=np.array(q.im)
        for y,x in np.argwhere(a==q.ink('cloth_a',2)):
            if (x+stable(style)+seed)%7==0 and y%9 in {3,4}: a[y,x]=q.ink('cloth_a',3)
        q.im=Image.fromarray(a)
    elif layer=='outerwear' and anim!='swim':
        bottom=min(58,b+(8 if style in {'long_cape','hooded_cloak','royal_mantle','officer_coat'} else 2))
        q.poly([(l-3,t),(r+3,t),(r+5,bottom),(l-5,bottom)],'cloth_a',0)
        q.poly([(l-3,t),(cx,t+2),(l-1,bottom),(l-4,bottom)],'cloth_a',2)
        q.poly([(cx+2,t+2),(r+3,t),(r+4,bottom),(r,bottom)],'cloth_a',2)
        if up: rect((l-1,t+2,r+1,bottom-1),'cloth_a',2)
        line([(l-3,t),(cx,t+2),(r+3,t)],'cloth_b',3,2)
        line([(l-4,bottom),(r+4,bottom)],'cloth_b',1)
        if 'fur' in style:
            for x in range(l-3,r+4,3): rect((x,t,x+1,t+4),'cloth_b',3)
    elif layer=='armour' and anim!='swim':
        rmp='metal_copper' if style=='copper_scale' else 'metal_iron' if style in {'iron_chain','iron_plate','ceramic_plate','composite','powered'} else 'cloth_a'
        rect((l,t+2,r,b-2),rmp,1 if rmp.startswith('metal') else 2)
        line([(l,t+2),(r-1,t+2)],rmp,3,2); line([(l,t+3),(l,b-2)],rmp,3)
        rect((l-3,t,l+2,t+4),rmp,3); rect((r-2,t,r+3,t+4),rmp,1)
        if style in {'bone','wood_lamellar','woven_guard','leather_scale','copper_scale','iron_chain'}:
            for y in range(t+6,b-2,4):
                for x in range(l+2,r,4): rect((x,y,x+1,y+1),rmp,3)
        else: line([(cx,t+4),(cx,b-3)],rmp,3)
        line([(l,b-1),(r,b-1)],'cloth_b',1,2)
        if style=='powered': rect((cx-1,t+7,cx+1,t+10),'cloth_b',3)
    elif layer=='headwear':
        rmp='metal_copper' if style=='copper_helmet' else 'metal_iron' if style in {'iron_helmet','guard_helmet','visor_helmet','environment_helmet'} else 'cloth_a'
        if style in {'royal_circlet','beaded_band','feather_band'}:
            line([(hx-7,hy+1),(hx+7,hy+1)],'cloth_a',2,3)
            q.poly([(hx,hy-3),(hx+3,hy),(hx,hy+3),(hx-3,hy)],'cloth_b',3)
            if style=='feather_band': q.poly([(hx-6,hy),(hx-9,hy-9),(hx-5,hy-6),(hx-3,hy)],'cloth_b',3)
        elif style in {'royal_crown','ceremonial_headdress'}:
            q.poly([(hx-8,hy+2),(hx-9,hy-6),(hx-4,hy-3),(hx,hy-8),(hx+4,hy-3),(hx+8,hy-6),(hx+7,hy+2)],'cloth_a',2)
            line([(hx-7,hy+2),(hx+6,hy+2)],'cloth_b',3,2)
            if style=='ceremonial_headdress':
                q.poly([(hx-8,hy),(hx-12,hy-5),(hx-10,hy+6),(hx-6,hy+8)],'cloth_b',3)
                q.poly([(hx+8,hy),(hx+12,hy-5),(hx+10,hy+6),(hx+6,hy+8)],'cloth_b',1)
        elif style=='cook_hat':
            rect((hx-6,hy-2,hx+6,hy+2),'cloth_a',2)
            for dx in [-5,0,5]: q.ellipse((hx+dx-4,hy-8,hx+dx+4,hy-1),'cloth_a',3)
            line([(hx-6,hy+2),(hx+6,hy+2)],'cloth_b',1)
        elif style=='scholar_cap':
            q.poly([(hx-9,hy-1),(hx-5,hy-5),(hx+6,hy-4),(hx+10,hy),(hx+4,hy+2),(hx-5,hy+2)],'cloth_a',2)
            line([(hx-6,hy-3),(hx+4,hy-3)],'cloth_a',3)
        elif style in {'wide_brim_hat','woven_hat','mage_hat'}:
            q.ellipse((hx-11,hy-1,hx+11,hy+4),'cloth_a',0)
            q.poly([(hx-6,hy),(hx-3,hy-7),(hx+5,hy-7),(hx+7,hy)],'cloth_a',2)
            if style=='mage_hat': q.poly([(hx-6,hy),(hx+2,hy-8),(hx+8,hy-6),(hx+5,hy)],'cloth_a',2)
            line([(hx-6,hy),(hx+6,hy)],'cloth_b',3,2)
        else:
            q.ellipse((hx-8,hy-5,hx+8,hy+6),rmp,1 if rmp.startswith('metal') else 2)
            q.ellipse((hx-5,hy+2,hx+5,hy+10),'skin',2)
            if up: rect((hx-6,hy,hx+6,hy+8),rmp,1)
            elif style in {'visor_helmet','environment_helmet','respirator_hood'}: rect((hx-5,hy+3,hx+6,hy+6),'cloth_b',1)
            else:
                for x in ([hx+3] if side else [hx-3,hx+3]): rect((x,hy+5,x+1,hy+6),'skin',0)
            line([(hx-6,hy-2),(hx+3,hy-3)],rmp,3,2)
            if style in {'hood','headwrap','veil','fur_hat'}: rect((hx-8,hy+3,hx-5,hy+12),'cloth_a',2)
            if style in {'headwrap','veil'}:
                for dy in [-2,1]: line([(hx-6,hy+dy),(hx+5,hy+dy+2)],'cloth_b',3)
            if style=='veil': rect((hx+5,hy+3,hx+8,hy+14),'cloth_a',2)
            if style=='work_helmet': rect((hx-1,hy-5,hx+2,hy+1),'cloth_b',3); line([(hx-9,hy+2),(hx+9,hy+2)],'cloth_a',0,2)
            if style=='fur_hat':
                for dx in range(-6,7,3): rect((hx+dx,hy+1,hx+dx+1,hy+4),'cloth_b',3)
            if style=='bone_helmet':
                line([(hx-4,hy-3),(hx-5,hy+3)],'cloth_b',3,3); line([(hx+4,hy-3),(hx+5,hy+3)],'cloth_b',3,3)
    elif layer=='accessory':
        if style in {'spectacles','goggles','face_wrap','respirator'} and not up:
            rect((hx-5,hy+5,hx+5,hy+7),'cloth_b',1)
            if style in {'spectacles','goggles'}: rect((hx-3,hy+5,hx-1,hy+6),'cloth_b',3); rect((hx+2,hy+5,hx+4,hy+6),'cloth_b',3)
        elif style in {'scarf','necklace','beads','royal_chain'}:
            line([(l,t),(cx,t+6),(r,t)],'cloth_b',3,2)
        elif style in {'ceremonial_sash','backpack_straps'}:
            line([(l,t),(r,b)],'cloth_b',3,2)
            if style=='backpack_straps': line([(r,t),(l,b)],'cloth_b',1,2)
        else:
            line([(l,b-2),(r,b-2)],'cloth_b',1,3); rect((r-3,b-3,r+3,b+3),'cloth_b',3)
    elif layer=='held_item':
        q=Pixels(slots,128); x=y=64
        wooden={'club','spear','stone_axe','axe','pick','hoe','shovel','hammer','chisel','mace','fishing_rod','torch','staff','wand','measuring_rod'}
        if style in wooden:
            line([(x-3,y+6),(x+4,y-13)],'wood',1,3); line([(x-2,y+4),(x+4,y-12)],'wood',3)
            if style in {'stone_axe','axe','pick','hoe','shovel','hammer','chisel','mace'}:
                if style in {'stone_axe','axe'}: q.poly([(x+1,y-11),(x+9,y-14),(x+10,y-7),(x+3,y-6)],'metal_iron' if style!='stone_axe' else 'stone',3)
                elif style=='pick': line([(x-5,y-10),(x+4,y-15),(x+12,y-11)],'metal_iron',3,2)
                elif style=='hoe': rect((x+2,y-14,x+12,y-10),'metal_iron',3)
                elif style=='shovel': q.poly([(x+1,y-13),(x+8,y-15),(x+11,y-10),(x+7,y-5),(x+3,y-6)],'metal_iron',3)
                elif style=='hammer': rect((x-1,y-15,x+10,y-9),'metal_iron',3)
                elif style=='chisel': line([(x+3,y-11),(x+6,y-16)],'metal_iron',3,2)
                else: q.ellipse((x,y-17,x+10,y-8),'metal_iron',3)
            elif style=='spear': q.poly([(x+3,y-12),(x+6,y-20),(x+8,y-12)],'stone',3)
            elif style in {'staff','wand'}: q.ellipse((x,y-20,x+8,y-12),'cloth_b',3); q.ellipse((x+2,y-18,x+5,y-15),'cloth_a',2)
            elif style=='torch': q.poly([(x+1,y-12),(x,y-17),(x+4,y-22),(x+7,y-14)],'fire',3)
            elif style=='fishing_rod': line([(x+4,y-13),(x+14,y-17),(x+14,y+4)],'cloth_b',3)
        elif style in {'sword','energy_blade','knife'}:
            line([(x-2,y+4),(x+8,y-(8 if style=='knife' else 14))],'metal_iron',3,3); line([(x-2,y-3),(x+5,y+1)],'cloth_b',1,3)
            if style=='energy_blade': line([(x+1,y-3),(x+8,y-14)],'cloth_b',3,3)
        elif style in {'bow','crossbow'}:
            line([(x-2,y-13),(x+5,y-8),(x+6,y),(x+1,y+7)],'wood',3,2)
            line([(x-2,y-13),(x+1,y+7)],'cloth_b',3)
            if style=='crossbow': line([(x-5,y+4),(x+9,y-7)],'wood',1,4)
        elif style=='shield':
            q.poly([(x-6,y-10),(x+7,y-10),(x+8,y+1),(x,y+9),(x-7,y+1)],'cloth_a',0)
            q.poly([(x-4,y-8),(x+5,y-8),(x+5,y),(x,y+6),(x-4,y)],'cloth_a',2)
            q.ellipse((x-2,y-3,x+2,y+1),'cloth_b',3)
        elif style in {'rifle','pistol','energy_tool','wrench','tongs'}:
            line([(x-4,y+4),(x+10,y-7)],'metal_iron',1,4); line([(x-1,y),(x+9,y-7)],'metal_iron',3,2)
            if style in {'rifle','energy_tool'}: line([(x+5,y-6),(x+15,y-8)],'metal_iron',1,3)
            if style=='rifle': rect((x-6,y+2,x-1,y+6),'wood',1); rect((x+2,y-5,x+7,y-1),'metal_iron',1)
            elif style=='pistol': line([(x+5,y-5),(x+9,y-7)],'metal_iron',3,3); line([(x,y),(x+2,y+5)],'metal_iron',1,3)
            elif style=='energy_tool': rect((x-1,y-5,x+6,y+2),'cloth_a',2); rect((x+12,y-10,x+15,y-6),'cloth_b',3)
            elif style=='wrench': line([(x+7,y-6),(x+6,y-12)],'metal_iron',3,3); line([(x+9,y-6),(x+14,y-8)],'metal_iron',3,3)
            else: line([(x-5,y+5),(x+7,y-8)],'metal_iron',3,2); line([(x-1,y+6),(x+11,y-5)],'metal_iron',1,2)
        elif style=='orb_focus': q.ellipse((x-5,y-8,x+7,y+3),'cloth_b',1); q.ellipse((x-3,y-7,x+3,y-1),'cloth_b',3)
        elif style=='net':
            line([(x,y+5),(x+8,y-8)],'wood',3,2); q.ellipse((x+3,y-18,x+15,y-5),'cloth_b',1)
            for yy in range(y-15,y-5,3): line([(x+5,yy),(x+12,yy)],'cloth_b',3)
        elif style=='basket':
            q.ellipse((x-6,y-5,x+8,y+5),'wood',1)
            for yy in range(y-2,y+4,3): line([(x-4,yy),(x+6,yy)],'wood',3)
            line([(x-4,y-5),(x-2,y-9),(x+4,y-9),(x+7,y-4)],'wood',3,2)
        elif style=='medicine_bag':
            q.ellipse((x-5,y-5,x+7,y+5),'cloth_a',2); line([(x-3,y-5),(x+4,y-5)],'cloth_b',3,2)
            line([(x-4,y-3),(x-4,y-8),(x+5,y-8),(x+6,y-3)],'cloth_b',1)
        else:
            clothbox((x-4,y-6,x+8,y+4))
            line([(x-2,y-3),(x+5,y-3)],'cloth_b',3)
            if style=='book': line([(x-2,y-5),(x-2,y+3)],'cloth_b',3,2)
            elif style=='construction_plan': rect((x,y-1,x+5,y+2),'cloth_b',1)
    a=np.array(q.im)
    if layer=='held_item':
        yy,xx=np.where(a!=255); values=a[yy,xx]; dx=xx-64; dy=yy-64; ax,ay=hands[1]
        result=np.full((64,64),255,np.uint8)
        candidates=[(dx,dy),(-dy,dx),(-dx,-dy),(dy,-dx)]
        for rx,ry in candidates:
            tx=rx+ax; ty=ry+ay
            if np.all((tx>=4)&(tx<=59)&(ty>=4)&(ty<=59)):
                result[ty,tx]=values; break
        else: raise ValueError(f'Cannot fit held item {style} at {hands[1]}')
        a=result
    a[:4]=255; a[-4:]=255; a[:,:4]=255; a[:,-4:]=255
    return Image.fromarray(a)

def build(seed=0):
    palette=json.loads((ROOT/'assets/palette/palette.json').read_text()); slots={s['name']:s['index'] for s in palette['slots']}
    lookup=np.zeros((256,4),np.uint8)
    for s in palette['slots']: lookup[s['index']]=[*s['reference'],255]
    out=ROOT/'assets/wardrobe'; out.mkdir(parents=True,exist_ok=True); entries=[]
    for anim,(frames,fps,actions) in ANIMS.items():
        prof=profiles(anim); poses=[[pose(*p,anim,f) for f in range(frames)] for p in prof]
        for layer,styles in PARTS.items():
            strips=[]; hashes={}; rows=[]; mapping=[]
            for style in styles:
                indices=[]
                for pi,p in enumerate(prof):
                    strip=np.concatenate([np.array(part(layer,style,poses[pi][f],anim,f,seed,slots)) for f in range(frames)],axis=1)
                    key=strip.tobytes()
                    if key not in hashes:
                        hashes[key]=len(strips); strips.append(strip); rows.append(style+'/'+('/'.join(p)))
                    indices.append(hashes[key])
                mapping.append(indices)
            for density in DENSITIES:
                low=[]; low_labels=[]; low_map=[]; low_hashes={}
                for row,strip in enumerate(strips):
                    resized=np.concatenate([np.array(Image.fromarray(strip[:,f*64:(f+1)*64]).resize((density,density),Image.Resampling.NEAREST)) for f in range(frames)],axis=1)
                    key=resized.tobytes()
                    if key not in low_hashes:
                        low_hashes[key]=len(low); low.append(resized); low_labels.append(rows[row])
                    low_map.append(low_hashes[key])
                w=density; cols=min(8,len(low)); height=math.ceil(len(low)/cols)*w
                idx=np.zeros((height,w*frames*cols,4),np.uint8)
                for row,a in enumerate(low):
                    x=(row%cols)*w*frames; y=(row//cols)*w
                    idx[y:y+w,x:x+w*frames,0]=np.where(a==255,0,a); idx[y:y+w,x:x+w*frames,3]=(a!=255)*255
                ref=lookup[idx[:,:,0]].copy(); ref[:,:,3]=idx[:,:,3]; ref[idx[:,:,3]==0]=0
                stem=f'{layer}_{anim}_lod{density}'; used=set(idx[idx[:,:,3]>0,0].tolist())
                Image.fromarray(idx).save(out/f'{stem}_idx.png'); Image.fromarray(ref).save(out/f'{stem}.png')
                e={'id':'wardrobe.'+stem,'family':'wardrobe','asset_id':layer,'animation':anim,'files':[f'wardrobe/{stem}.png',f'wardrobe/{stem}_idx.png'],'sidecar':f'wardrobe/{stem}.json',
                   'frame':[w,w],'frames':frames,'fps':fps,'directions':list(dict.fromkeys(p[2] for p in prof)),'pivot':[w//2,w*14//16],
                   'density':density,'logical_frame':[16,16],'strip_columns':cols,'strip_count':len(low),'sheet_rows':low_labels,'profiles':prof,'variants':styles,'row_lookup':[[low_map[row] for row in indices] for indices in mapping],
                   'sim_actions':actions,'hand_anchors':[[[v*density//16 for v in pose['hands'][1]] for pose in pose_frames] for pose_frames in poses],
                   'row_pivots':[[v*density//16 for v in pose_frames[0]['pivot']] for pose_frames in poses],
                   'anims':{anim:{'frames':frames,'fps':fps}},'seed':seed,'tags':['modular_outfit',layer],'shadow':'engine',
                   'recolor_slots':[s['name'] for s in palette['slots'] if s['index'] in used],
                   'dye_channels':{'main':'cloth_a','trim':'cloth_b','culture':'culture'},'per_piece_dyes':True}
                (out/f'{stem}.json').write_text(json.dumps(e,indent=2)+'\n',encoding='utf-8')
                entries.append({key:value for key,value in e.items() if key not in {'sheet_rows','profiles','variants','row_lookup','hand_anchors','row_pivots'}})
    from .wardrobe_data import build_data
    build_data(out,seed)
    return entries
