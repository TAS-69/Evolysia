
import numpy as np
from PIL import Image, ImageDraw
from .terrain import stable

WALLS={'bamboo':'wood','reed':'thatch','hide':'hide','rammed_earth':'soil','adobe':'mudbrick','sandstone':'sand','limestone':'stone','granite':'rock','basalt':'basalt','ice':'ice','snow':'snow','concrete':'rock','ceramic':'clay','composite':'metal_iron','salvage':'metal_copper'}
FLOORS={'bamboo':'wood','packed_clay':'clay','sandstone':'sand','granite':'rock','basalt':'basalt','ceramic_tile':'clay','ice':'ice','composite':'metal_iron'}
ROOFS={'hide':'hide','bark':'bark','reed':'thatch','wood_shingle':'wood','slate':'rock','copper':'metal_copper','glass':'ice','composite':'metal_iron','solar':'ice'}
FOODS={'water':'water','snow':'snow','ice':'ice','sand':'sand','wood':'wood','bamboo':'wood','brick':'clay','stone_blocks':'stone',
    'berries':'fruit','nuts':'wood','roots':'soil','meat_raw':'hide','meat_cooked':'wood','fish':'water','fruit':'fruit','honey':'thatch','bread':'thatch','dried_meat':'wood','milk':'snow','eggs':'hide',
    'apple':'fruit','pear':'foliage','peach':'clay','plum':'flower','orange':'fire','lemon':'thatch','grapes':'flower','melon':'grass','banana':'thatch','coconut':'wood','dates':'wood','fig':'flower',
    'carrot':'clay','potato':'soil','beet':'fruit','onion':'hide','cabbage':'foliage','lettuce':'grass','tomato':'fruit','pepper':'fruit','pumpkin':'clay','squash':'thatch','peas':'foliage','beans':'wood','mushrooms':'hide',
    'wheat':'thatch','barley':'thatch','oats':'thatch','rice':'snow','maize':'thatch','millet':'sand','rye':'thatch','lentils':'clay',
    'freshwater_fish':'water','saltwater_fish':'ice','dried_fish':'wood','shellfish':'hide','crab':'clay','shrimp':'clay','squid':'hide','poultry':'hide','cheese':'thatch','butter':'thatch','flour':'snow','oil':'thatch','tea':'foliage','coffee':'wood','herbs':'foliage','spices':'clay',
    'flint':'rock','copper_ore':'metal_copper','tin_ore':'stone','iron_ore':'metal_iron','gold_ore':'thatch','silver_ore':'metal_iron','coal':'basalt','sulfur':'blight','quartz':'ice','copper_ingot':'metal_copper','bronze_ingot':'metal_copper','salt':'snow','ochre':'clay','charcoal':'basalt','fiber':'thatch','reed':'foliage','hide':'hide','fur':'hide','bone':'snow','antler':'hide','rope':'wood','leather':'hide','steel':'metal_iron','glass':'ice','ceramic':'clay','concrete':'rock','electronics':'metal_copper','battery':'metal_iron','advanced_alloy':'metal_iron'}
OBJECTS=['throne','lectern','ceremonial_screen','altar','brazier','weapons_rack','armour_stand','scaffold','sawbench','fish_drying_rack','feed_trough','market_stall','alchemy_table','telescope','water_pump','construction_crane','windlass','press','spinning_wheel','dye_vat','brew_vat','beehive','compost_bin','tool_rack','cooking_pot','stone_quern','medical_table','bookcase','communication_terminal','battery_bank','hydroponic_rack','fabricator']
TREES=['oak','birch','willow','cedar','spruce','fir','pine','juniper','palm','acacia','jungle','mangrove','baobab','fruit_tree']
BUSHES=['berry_shrub','thorn_shrub','flowering_shrub','dry_shrub','heath','fern','succulent','cactus','reeds','sedge','meadow_grass','dry_grass','weeds','clover','moss','wildflowers','mushroom_patch','fallen_log','tree_stump','scree','sandstone_outcrop','basalt_outcrop','granite_outcrop','clay_deposit','ore_outcrop']
ANIMALS=['rabbit','goat','sheep','deer','elk','bison','cattle','horse','camel','wolf','dog','bear','big_cat','pig','fowl','duck','goose','crow','hawk','parrot','fish','snake','crocodile']
NATURE={**{'tree_'+k:([64,128],[16,32]) for k in TREES},**{'nature_'+k:([64,64],[16,16]) for k in BUSHES},**{'animal_'+k:([64,64],[16,16]) for k in ANIMALS}}
VEHICLES={'raft':([64,64],[16,16]),'canoe':([64,64],[16,16]),'rowing_boat':([64,96],[16,24]),'sailboat':([128,128],[32,32]),'cargo_ship':([192,128],[48,32]),'wagon':([96,64],[24,16]),'sled':([96,64],[24,16]),'bicycle':([64,64],[16,16]),'truck':([96,64],[24,16]),'rover':([96,64],[24,16]),'hover_carrier':([96,64],[24,16])}

def food(kind,p):
    r=FOODS[kind]
    if kind in {'wheat','barley','oats','rice','maize','millet','rye','lentils','flour','coffee','salt','ochre','spices','sulfur'}:
        if kind=='maize':
            p.poly([(4,12),(6,2),(10,1),(13,4),(11,13)],r,2)
            for y in [4,7,10]: p.line([(7,y),(10,y)],r,3,2)
            p.poly([(4,13),(2,6),(6,10),(10,14)],'foliage',2)
        elif kind in {'wheat','barley','rye'}:
            for x in [4,8,11]:
                p.line([(x,14),(x+1,3)],r,1)
                p.poly([(x-1,5),(x+1,1),(x+3,5),(x+2,10),(x,11)],r,3)
                if kind=='barley': p.line([(x-2,4),(x+1,8),(x+3,3)],r,2)
                if kind=='rye': p.rect((x,6,x+1,8),r,1)
        elif kind=='flour':
            p.poly([(3,5),(5,2),(11,2),(13,5),(12,14),(3,14)],'hide',2)
            p.rect((5,5,10,10),'snow',3); p.line([(5,3),(10,3)],'wood',1)
        elif kind in {'salt','sulfur'}:
            for x,y in [(3,9),(7,5),(11,10)]: p.poly([(x,y),(x+1,y-4),(x+4,y-2),(x+4,y+3),(x,y+3)],r,3)
        elif kind in {'rice','oats','lentils','coffee'}:
            for x,y in [(4,5),(9,4),(6,10),(11,11)]:
                p.ellipse((x-2,y-2,x+2,y+2),r,2)
                if kind=='rice': p.rect((x,y-2,x,y+2),r,3)
                else: p.line([(x-1,y),(x+1,y+1)],r,1)
        else:
            p.poly([(2,10),(5,5),(10,3),(14,10),(12,14),(3,14)],r,1)
            for x,y in [(4,10),(7,7),(10,6),(11,11),(7,12)]: p.rect((x,y-1,x+1,y+1),r,3)
    elif kind in {'wood','bamboo','brick','stone_blocks','bone','antler','hide','fur','leather','charcoal','quartz','battery','electronics','glass'}:
        if kind in {'wood','bamboo','charcoal'}:
            for x,y in [(3,5),(7,3),(10,7)]:
                p.line([(x,y),(x+2,y+8)],r,1,3); p.line([(x,y),(x+1,y+6)],r,3)
                if kind=='bamboo': p.line([(x-1,y+3),(x+2,y+3)],r,0)
        elif kind in {'bone','antler'}:
            p.line([(4,12),(10,4)],r,3,3)
            if kind=='bone': p.ellipse((2,10,6,14),r,2); p.ellipse((8,2,13,6),r,3)
            else: p.line([(7,9),(5,3)],r,3,2); p.line([(9,7),(13,7),(14,4)],r,3,2)
        elif kind in {'hide','fur','leather'}:
            p.poly([(2,3),(7,4),(13,2),(12,7),(14,13),(8,12),(3,14),(4,8)],r,2)
            p.line([(4,5),(7,6),(10,4)],r,3)
            if kind=='fur':
                for x,y in [(3,4),(11,4),(12,11),(5,11)]: p.line([(x,y),(x-1,y+2)],r,1)
            if kind=='leather': p.line([(5,6),(6,11),(11,10)],r,0)
        elif kind=='quartz':
            for x,y in [(3,7),(7,3),(10,8)]: p.poly([(x,y+5),(x,y),(x+2,y-2),(x+4,y),(x+3,y+6)],r,2); p.line([(x+1,y),(x+1,y+4)],r,4)
        elif kind=='battery': p.box(4,4,9,11,'metal_iron'); p.rect((6,1,9,3),'metal_iron',3); p.rect((6,7,10,11),'blight',2)
        elif kind=='electronics':
            p.rect((2,3,13,13),'foliage',1); p.rect((5,6,10,10),'metal_iron',0)
            for x in [3,7,11]: p.line([(x,4),(x,6)],'metal_copper',3)
        else:
            p.poly([(2,6),(9,3),(14,6),(13,12),(6,14),(2,11)],r,1)
            p.poly([(2,6),(9,3),(14,6),(6,10)],r,3)
            if kind=='glass': p.line([(4,6),(9,5)],r,4)
    elif kind in {'bread','cheese','butter','meat_raw','meat_cooked','poultry','dried_meat'}:
        if kind=='bread':
            p.ellipse((2,4,14,13),r,2)
            for x in [5,9]: p.line([(x,6),(x+2,9)],r,1)
            p.line([(4,5),(10,5)],r,3)
        elif kind=='cheese':
            p.poly([(2,12),(5,3),(14,7),(14,13)],r,2); p.poly([(2,11),(5,3),(14,7)],r,3)
            for x,y in [(5,9),(10,10),(11,6)]: p.rect((x,y,x+1,y+1),r,1)
        elif kind=='butter': p.box(3,5,11,8,r); p.line([(3,7),(1,9),(3,12)],'hide',3)
        elif kind=='poultry': p.ellipse((2,3,11,11),r,2); p.line([(9,10),(13,14)],'snow',3,3)
        elif kind=='dried_meat':
            for x in [3,7,11]: p.poly([(x,3),(x+2,2),(x+3,12),(x+1,14)],r,2)
        elif kind=='meat_raw': p.box(3,4,11,10,r); p.line([(4,5),(12,12)],'thatch',3)
        else: p.ellipse((2,4,14,13),r,2); p.line([(4,7),(11,10)],r,0,2); p.line([(6,5),(13,8)],r,1)
    elif kind in {'crab','shrimp','shellfish','squid'}:
        if kind=='crab':
            p.ellipse((4,5,12,11),r,2)
            for y in [7,10]: p.line([(5,y),(1,y+3)],r,1); p.line([(11,y),(15,y+3)],r,1)
            p.line([(5,6),(2,2),(1,5)],r,3,2); p.line([(11,6),(14,2),(15,5)],r,3,2)
        elif kind=='shrimp':
            p.poly([(3,5),(5,2),(11,3),(14,8),(11,13),(6,12),(9,9),(9,6)],r,2)
            for x,y in [(5,4),(9,5),(11,9)]: p.line([(x,y),(x+2,y+2)],r,3)
        elif kind=='squid':
            p.poly([(5,9),(4,5),(8,1),(12,5),(11,9)],r,2)
            for x in [5,8,11]: p.line([(x,8),(x-1,12),(x+1,14)],r,3)
        else:
            p.poly([(7,14),(2,6),(3,3),(7,1),(12,3),(14,7),(9,14)],r,2)
            for x in [4,7,10]: p.line([(8,12),(x,4)],r,3)
    elif kind in {'grapes','melon','banana','coconut','pumpkin','squash','cabbage','lettuce','peas','beans','mushrooms','tomato','pepper'}:
        if kind=='grapes':
            for x,y in [(5,4),(10,4),(4,8),(8,8),(11,8),(8,12)]: p.ellipse((x-2,y-2,x+2,y+2),r,2); p.rect((x-1,y-1,x,y),r,3)
        elif kind in {'melon','pumpkin','cabbage'}:
            p.ellipse((2,3,14,14),r,2)
            for x in [5,8,11]: p.line([(x,5),(x-1,9),(x,12)],r,1)
            p.line([(4,5),(9,4)],r,3)
        elif kind=='banana':
            p.line([(3,3),(5,9),(9,12),(13,10)],r,1,4); p.line([(4,3),(6,8),(10,11),(13,9)],r,3,2)
        elif kind=='coconut': p.ellipse((2,3,14,14),r,1); p.ellipse((4,5,12,12),'snow',3); p.ellipse((7,7,10,10),r,0)
        elif kind=='squash': p.ellipse((3,7,12,14),r,2); p.line([(9,9),(11,3),(8,2)],r,3,4)
        elif kind=='lettuce':
            p.poly([(7,14),(2,9),(3,5),(5,6),(5,2),(8,5),(12,2),(13,8),(10,13)],r,2); p.line([(7,12),(8,5)],r,3)
        elif kind=='peas':
            p.poly([(2,11),(3,6),(10,3),(14,4),(12,10),(6,13)],r,1)
            for x,y in [(5,9),(8,7),(11,5)]: p.ellipse((x-1,y-1,x+1,y+1),r,3)
        elif kind=='beans': p.ellipse((2,4,8,13),r,2); p.ellipse((8,3,13,11),r,3); p.rect((6,5,8,7),r,0)
        elif kind=='mushrooms':
            for x,y in [(5,9),(11,7)]: p.rect((x,y,x+1,y+5),'wood',3); p.ellipse((x-3,y-4,x+3,y),r,2)
        elif kind=='tomato': p.ellipse((2,5,14,14),r,2); p.line([(4,5),(9,7),(12,4)],'foliage',2,2)
        else: p.poly([(4,3),(9,2),(13,5),(12,11),(7,14),(5,11)],r,2); p.line([(7,3),(8,1)],'foliage',2)
    elif kind in {'copper_ore','iron_ore','gold_ore','silver_ore'}:
        p.poly([(2,8),(5,3),(10,2),(14,8),(11,13),(4,13)],'rock',1)
        vein='thatch' if kind=='gold_ore' else 'metal_iron' if kind=='silver_ore' else 'metal_copper' if kind=='copper_ore' else 'clay'
        p.line([(4,7),(7,5),(8,9),(12,10)],vein,3,2)
    elif kind in {'fish','freshwater_fish','saltwater_fish','dried_fish'}:
        p.poly([(2,6),(0,3),(0,12),(3,10),(6,13),(12,11),(15,7),(11,3),(6,3)],r,2)
        p.line([(5,5),(10,4),(13,6)],r,3); p.rect((11,6,12,7),r,0)
        p.line([(7,8),(9,10)],r,1)
    elif kind in {'apple','pear','peach','plum','orange','lemon','tomato','pepper','berries','fruit'}:
        if kind in {'berries','fruit'}:
            for x,y in [(5,9),(10,9),(8,5)]: p.ellipse((x-3,y-2,x+2,y+3),r,2); p.rect((x-1,y-1,x,y),r,3)
        else:
            p.ellipse((3,4,13,14),r,2); p.ellipse((4,5,7,8),r,3)
            if kind=='pear': p.ellipse((6,1,11,9),r,2)
        p.line([(8,4),(9,1)],'wood',1)
    elif kind in {'eggs','potato','nuts','onion','coconut','dates','fig'}:
        for x,y in [(5,7),(10,9)]: p.ellipse((x-3,y-4,x+3,y+4),r,2); p.line([(x-1,y-2),(x,y-3)],r,3)
    elif kind in {'carrot','roots','beet','banana'}:
        p.poly([(4,4),(9,3),(12,7),(10,12),(5,14),(7,8)],r,2); p.line([(6,5),(9,6)],r,3)
        p.line([(6,4),(3,1)],'foliage',2); p.line([(7,3),(9,1)],'foliage',3)
    elif kind in {'cabbage','lettuce','herbs','tea','peas','beans','grapes','mushrooms'}:
        for x,y in [(5,6),(10,6),(7,10)]:
            p.ellipse((x-3,y-3,x+3,y+3),r,2); p.line([(x-1,y-2),(x,y)],r,3)
        if kind=='mushrooms': p.rect((6,10,8,14),'wood',3)
    elif kind in {'wheat','barley','oats','rice','maize','millet','rye','lentils','flour','spices','salt','ochre','coffee','sulfur'}:
        p.poly([(2,10),(5,5),(10,3),(14,10),(12,14),(3,14)],r,1)
        for x,y in [(4,10),(7,7),(10,6),(11,11),(7,12)]: p.rect((x,y-1,x+1,y+1),r,3)
    elif kind in {'milk','oil','honey','water'}:
        p.ellipse((3,5,13,14),'clay',1); p.rect((6,1,10,6),'clay',2); p.ellipse((5,1,11,4),r,3)
        p.line([(4,7),(4,11)],'clay',3)
    elif kind in {'bread','cheese','butter','meat_raw','meat_cooked','poultry','dried_meat'}:
        p.poly([(2,7),(6,3),(12,4),(14,10),(11,14),(3,13)],r,2); p.line([(4,6),(10,5)],r,3)
        for x in [5,9]: p.line([(x,7),(x+2,10)],r,1)
    elif kind in {'crab','shrimp','shellfish','squid'}:
        p.ellipse((4,4,12,12),r,2)
        for x in [3,7,11]: p.line([(8,9),(x,14)],r,1)
        p.line([(5,6),(2,3)],r,3); p.line([(11,6),(14,3)],r,3)
    elif 'ingot' in kind or kind in {'steel','battery','electronics','advanced_alloy','glass','ceramic'}:
        p.poly([(2,6),(10,3),(14,6),(13,11),(5,14),(2,11)],r,1)
        p.poly([(2,6),(10,3),(14,6),(5,10)],r,3)
        if kind=='electronics': p.rect((6,6,10,10),'metal_iron',1)
    else:
        p.poly([(2,8),(5,3),(10,2),(14,8),(11,13),(4,13)],r,2); p.line([(4,6),(7,4),(10,4)],r,3)
        if kind in {'rope','fiber','reed'}:
            for y in [6,9,12]: p.line([(3,y),(12,y-2)],r,1)
    return p.im

def fixture(kind,p,frame=0):
    if kind in {'throne','lectern','ceremonial_screen','bookcase','weapons_rack','tool_rack','armour_stand','scaffold','fish_drying_rack','hydroponic_rack'}:
        for x in [10,49]: p.box(x,9,5,48,'wood' if kind not in {'armour_stand','hydroponic_rack'} else 'metal_iron')
        p.box(10,9,44,6,'wood'); p.box(10,48,44,6,'wood')
        if kind=='throne':
            p.boards((16,10,47,43)); p.rect((19,16,44,39),'cloth_a',2); p.box(13,40,39,8,'wood')
            p.box(9,32,8,11,'wood'); p.box(47,32,8,11,'wood'); p.poly([(20,11),(32,3),(44,11)],'wood',3)
        elif kind in {'ceremonial_screen','lectern'}:
            p.rect((16,17,47,45),'cloth_a',2)
            for x in [20,43]: p.line([(x,19),(x,43)],'cloth_b',3,2)
            if kind=='lectern': p.poly([(14,31),(43,21),(50,35),(20,44)],'wood',2)
        elif kind=='armour_stand':
            p.line([(32,15),(32,48)],'wood',1,5); p.box(20,21,25,19,'metal_iron'); p.ellipse((23,6,41,22),'metal_iron',2)
        elif kind=='scaffold':
            p.line([(12,46),(50,17)],'wood',1,3); p.line([(12,17),(50,46)],'wood',2,3)
            for y in [24,38]: p.box(8,y,49,4,'wood')
        elif kind=='hydroponic_rack':
            for y in [22,39]:
                p.box(14,y,37,7,'metal_iron')
                for x in [20,32,44]: p.poly([(x,y),(x-5,y-9),(x,y-5),(x+5,y-11),(x+2,y)],'foliage',3)
        elif kind=='bookcase':
            for y in [25,44]:
                p.box(14,y,36,4,'wood')
                for x in range(17,47,6): p.rect((x,y-12,x+3,y-1),'cloth_a' if x%3 else 'cloth_b',2)
        else:
            for x in [20,32,44]:
                p.line([(x,19),(x-3,47)],'wood',1,3)
                if kind=='weapons_rack': p.poly([(x-3,18),(x+1,9),(x+4,18)],'metal_iron',3)
                elif kind=='fish_drying_rack': p.ellipse((x-4,24,x+4,38),'hide',2)
                else: p.box(x-4,19,11,7,'metal_iron')
    elif kind in {'altar','medical_table','alchemy_table','sawbench','press','market_stall','stone_quern'}:
        for x in [11,47]: p.rect((x,36,x+5,58),'wood',0)
        p.boards((7,25,56,45),'stone' if kind in {'altar','stone_quern'} else 'wood',vertical=False)
        if kind=='market_stall':
            for x in [8,53]: p.line([(x,14),(x,43)],'wood',1,3)
            p.poly([(6,15),(14,6),(49,6),(58,16),(55,23),(8,23)],'cloth_a',2)
            for x in range(12,55,10): p.line([(x,11),(x-3,21)],'cloth_b',3,3)
            for x in [16,29,42]: p.ellipse((x,30,x+8,38),'fruit',2)
        elif kind=='alchemy_table':
            for x in [17,32,45]: p.ellipse((x-4,21,x+4,34),'clay',2); p.rect((x-2,15,x+2,23),'ice',3)
            p.ellipse((28,12,40,25),'cloth_b',3)
        elif kind=='medical_table': p.rect((13,27,50,40),'cloth_a',3); p.ellipse((15,28,27,36),'hide',3)
        elif kind=='sawbench': p.line([(16,31),(46,36)],'metal_iron',3,5)
        elif kind=='press': p.box(14,7,39,8,'wood'); p.line([(32,10),(32,32)],'metal_iron',1,5); p.box(17,30,30,7,'wood')
        elif kind=='altar': p.ellipse((17,28,29,36),'clay',2); p.ellipse((39,25,48,33),'clay',2)
        else: p.ellipse((16,22,46,39),'stone',2); p.line([(32,29),(45,18)],'wood',1,4)
    elif kind in {'brazier','cooking_pot','beehive','compost_bin','feed_trough','dye_vat','brew_vat'}:
        p.ellipse((10,20,53,56),'wood' if kind!='cooking_pot' else 'metal_iron',1)
        p.ellipse((11,16,52,37),'wood',3); p.ellipse((16,20,47,33),'clay',0)
        if kind=='brazier': p.flame(32,35,frame,18)
        elif kind=='cooking_pot': p.ellipse((17,22,46,31),'clay',2)
        elif kind=='beehive':
            for y in [24,32,40,48]: p.line([(13,y),(50,y)],'thatch',3,3)
            p.rect((28,42,38,47),'wood',0)
        elif kind=='compost_bin':
            for y in [28,37,46]: p.line([(13,y),(50,y)],'wood',0,3)
            p.ellipse((18,22,45,32),'soil',2)
        elif kind=='feed_trough': p.ellipse((17,21,46,31),'thatch',2)
        else: p.ellipse((17,21,46,31),'cloth_a' if kind=='dye_vat' else 'water',2)
    elif kind in {'telescope','construction_crane','windlass','spinning_wheel','water_pump'}:
        p.box(15,47,36,8,'wood'); p.line([(31,48),(31,18)],'wood',1,5)
        if kind=='telescope': p.line([(15,25),(48,10)],'metal_copper',1,9); p.line([(15,22),(46,8)],'metal_copper',3,3)
        elif kind=='construction_crane': p.line([(8,18),(52,10)],'wood',2,6); p.line([(49,12),(49,43)],'wood',0,2); p.line([(49,43),(45,48),(42,44)],'metal_iron',1,3)
        elif kind in {'spinning_wheel','windlass'}:
            p.ellipse((20,16,51,46),'wood',1); p.ellipse((24,20,47,42),'wood',3)
            for x,y in [(35,20),(25,30),(44,30),(35,41)]: p.line([(35,31),(x,y)],'wood',0,2)
        else: p.box(24,23,13,24,'metal_iron'); p.line([(31,24),(48,16)],'metal_iron',3,4); p.line([(36,36),(44,36),(44,43)],'metal_iron',1,4)
    else:
        p.box(8,40,49,17,'metal_iron'); p.box(13,12,39,30,'metal_iron')
        if kind=='battery_bank':
            for x in [17,30,43]: p.box(x,16,8,22,'metal_iron'); p.rect((x+2,19,x+4,23),'cloth_b',3)
        elif kind=='fabricator':
            p.rect((18,17,47,36),'metal_iron',0); p.line([(22,18),(43,18)],'metal_iron',3,3); p.line([(32,18),(32,29)],'metal_copper',3,3)
            p.poly([(26,34),(31,29),(39,32),(39,36),(27,36)],'clay',3)
        else:
            p.rect((18,16,46,35),'ice',0)
            for y in [21,27,32]: p.line([(22,y),(39,y)],'ice',3,2)
    return p.im

def nature(kind,p,seed):
    rng=np.random.default_rng(stable(kind)+seed)
    if kind.startswith('animal_'):
        species=kind[7:]; bird=species in {'fowl','duck','goose','crow','hawk','parrot'}; aquatic=species in {'fish','snake','crocodile'}
        ramp='basalt' if species in {'wolf','bear','big_cat','crow'} else 'hide'
        if species=='fish':
            p.ellipse((10,20,52,43),'water',2); p.poly([(13,29),(3,18),(3,47),(13,36)],'water',1); p.rect((45,27,48,30),'water',0); p.line([(18,24),(38,22)],'water',3,2)
        elif species=='snake':
            p.line([(8,49),(17,35),(31,44),(43,30),(54,32)],'hide',0,7); p.line([(9,47),(18,33),(31,42),(43,28),(54,30)],'hide',3,3)
        elif species=='crocodile':
            p.poly([(4,45),(16,33),(44,31),(59,36),(60,42),(37,45),(24,42)],'foliage',1)
            for x in [22,39]: p.line([(x,40),(x+4,51)],'foliage',0,5)
            for x in range(20,44,6): p.poly([(x,33),(x+2,27),(x+5,33)],'foliage',2)
            p.rect((53,34,55,36),'foliage',0)
        elif bird:
            p.ellipse((16,28,44,48),ramp,2); p.ellipse((29,19 if species=='goose' else 24,43,35),ramp,3)
            p.poly([(40,26),(53,29),(41,33)],'thatch',3)
            p.poly([(18,34),(7,25),(10,42),(22,42)],ramp,1)
            p.ellipse((21,33,35,44),ramp,1); p.rect((36,25,38,27),ramp,0)
            for x in [26,36]: p.line([(x,46),(x-2,56),(x+4,56)],'thatch',1,2)
            if species=='fowl': p.poly([(31,23),(33,16),(36,20),(39,17),(41,25)],'fruit',2)
            if species=='hawk': p.poly([(21,30),(28,22),(40,40),(29,45)],'basalt',1)
            if species=='parrot': p.ellipse((21,28,36,43),'foliage',3)
        else:
            heavy=species in {'cattle','bison','bear','pig'}; small=species=='rabbit'
            by=34 if heavy else 30; p.ellipse((10,by,45,51),ramp,2)
            if species in {'horse','camel','deer','elk'}: p.poly([(36,39),(36,20),(42,17),(48,31),(43,43)],ramp,2)
            p.ellipse((38,23 if not small else 34,56,39 if not small else 47),ramp,2)
            p.rect((51,29 if not small else 39,53,31 if not small else 41),ramp,0)
            for x in [17,37]:
                p.line([(x,45),(x-2,57)],ramp,1,4); p.line([(x+7,45),(x+9,56)],ramp,2,3)
            p.line([(12,40),(5,34),(4,40)],ramp,1,3)
            if small: p.ellipse((13,34,31,55),ramp,3); p.ellipse((43,18,48,39),ramp,2); p.ellipse((51,21,56,40),ramp,2)
            elif species in {'goat','deer','elk','cattle','bison'}:
                for x in [43,49]:
                    p.line([(x,25),(x-4,15),(x-2,11)],'wood',3,2)
                    if species in {'deer','elk'}: p.line([(x-3,18),(x+2,14)],'wood',3,2)
            elif species=='camel': p.ellipse((17,21,32,40),ramp,2); p.ellipse((28,21,41,39),ramp,2)
            elif species in {'wolf','dog','horse'}: p.poly([(41,25),(40,17),(46,24)],ramp,1)
            if species=='sheep':
                for x,y in [(14,34),(24,31),(34,33),(19,44),(31,45)]: p.ellipse((x-5,y-4,x+6,y+6),'hide',3)
            if species=='big_cat':
                for x,y in [(19,37),(26,41),(33,36)]: p.ellipse((x,y,x+3,y+2),'basalt',0)
            if species=='bison': p.ellipse((25,24,45,45),ramp,1)
            if species=='elk':
                p.ellipse((28,23,46,46),ramp,2)
                for x in [41,49]:
                    p.line([(x,24),(x-4,12),(x-2,5)],'wood',3,2)
                    for dx in [-5,1,5]: p.line([(x-2,14),(x+dx,7)],'wood',3,2)
                p.ellipse((38,22,55,38),ramp,2); p.rect((51,29,53,31),ramp,0)
            if species=='cattle':
                p.poly([(11,32),(41,30),(44,45),(18,50)],ramp,2)
                p.line([(43,25),(36,21),(36,16)],'wood',3,2)
                p.line([(48,25),(57,20),(57,16)],'wood',3,2)
            if species=='pig':
                p.ellipse((48,32,60,42),'clay',2); p.rect((55,36,56,37),'clay',0)
            if species in {'wolf','bear','big_cat'}: p.poly([(53,35),(56,35),(54,39)],'snow',3)
    elif kind.startswith('tree_'):
        species=kind[5:]; conifer=species in {'cedar','spruce','fir','pine','juniper'}
        p.poly([(29,48),(36,48),(36,116),(43,124),(33,122),(24,124),(29,114)],'bark',1)
        p.line([(30,72),(30,116)],'bark',3,3)
        if species=='palm':
            p.line([(31,118),(37,83),(29,43)],'bark',2,7)
            for dx,dy in [(-25,8),(-22,-14),(-5,-23),(21,-17),(29,3),(18,24)]: p.poly([(29,43),(29+dx//2,43+dy//2-5),(29+dx,43+dy),(29+dx//2,43+dy//2+4)],'foliage',2)
        elif conifer:
            for y,r in [(88,27),(69,23),(51,18),(34,12)]:
                shift=int(rng.integers(-2,3)); p.poly([(32+shift,y-25),(32+r,y+12),(35,y+9),(26,y+14),(32-r,y+10)],'foliage',1)
                p.poly([(32+shift,y-23),(30,y+8),(32-r+3,y+7)],'foliage',3)
        else:
            if species=='baobab': p.poly([(23,62),(40,62),(43,124),(21,124)],'bark',2)
            count=7 if species not in {'acacia','willow'} else 9
            for _ in range(count):
                x=int(rng.integers(14,49)); y=int(rng.integers(28,79)); radius=int(rng.integers(10,16))
                if species=='acacia': y=32+int(rng.integers(0,20)); radius=15
                if species=='willow': y+=12
                p.line([(32,88),(x,y+7)],'bark',1,4)
                p.ellipse((max(3,x-radius),y-9,min(60,x+radius),y+radius),'foliage',1)
                p.ellipse((max(4,x-radius),y-8,min(58,x+radius-3),y+radius-4),'foliage',2)
                p.ellipse((max(5,x-radius+3),y-7,x+2,y+3),'foliage',3)
                if species=='fruit_tree': p.ellipse((x,y+3,x+3,y+6),'fruit',2)
            if species=='birch':
                p.line([(30,82),(30,118)],'hide',3,4)
                for y in range(85,118,8): p.line([(29,y),(33,y+1)],'bark',0,2)
    else:
        name=kind.removeprefix('nature_')
        if name in {'scree','sandstone_outcrop','basalt_outcrop','granite_outcrop','clay_deposit','ore_outcrop'}:
            ramp='basalt' if name=='basalt_outcrop' else 'sand' if name=='sandstone_outcrop' else 'clay' if name=='clay_deposit' else 'rock'
            for x,y,r in [(19,43,15),(39,29,17),(46,48,11)]:
                p.poly([(x-r,y),(x-5,y-r),(x+7,y-r+3),(x+r,y),(x+6,y+9),(x-r+2,y+6)],ramp,2)
                p.line([(x-r+3,y-2),(x-5,y-r+2),(x+4,y-r+4)],ramp,3,2)
            if name=='ore_outcrop':
                for x,y in [(22,35),(36,25),(42,42)]: p.ellipse((x,y,x+5,y+4),'metal_copper',3)
        elif name in {'fallen_log','tree_stump'}:
            p.poly([(9,36),(43,25),(55,38),(52,49),(20,58),(8,48)],'bark',1)
            p.ellipse((8,36,25,57),'wood',3); p.ellipse((12,40,21,53),'wood',1)
            if name=='tree_stump': p.ellipse((21,23,45,49),'bark',2); p.ellipse((21,20,45,35),'wood',3)
        elif name in {'cactus','succulent'}:
            p.line([(32,56),(32,13)],'foliage',1,12); p.line([(29,52),(29,16)],'foliage',3,3)
            p.line([(29,33),(17,33),(17,24)],'foliage',2,7); p.line([(35,43),(47,43),(47,27)],'foliage',2,7)
        elif 'shrub' in name or name=='heath':
            r='thatch' if name=='dry_shrub' else 'foliage'
            for x,y in [(17,39),(31,26),(46,38),(31,45)]:
                p.ellipse((x-12,y-10,x+12,y+12),r,1); p.ellipse((x-10,y-9,x+7,y+7),r,3)
            for x,y in [(19,37),(32,26),(43,42)]:
                if name=='berry_shrub': p.ellipse((x,y,x+4,y+4),'fruit',2)
                if name=='flowering_shrub': p.ellipse((x,y,x+4,y+4),'flower',3)
        elif name=='fern':
            for tip in [(12,15),(32,7),(52,18)]:
                tx,ty=tip; p.line([(32,58),(tx,ty)],'foliage',1,2)
                for f in [.3,.5,.7,.85]:
                    x=int(32+(tx-32)*f); y=int(58+(ty-58)*f)
                    p.line([(x,y),(x-7,y-3)],'foliage',3,2); p.line([(x,y),(x+7,y-4)],'foliage',2,2)
        elif name=='moss':
            for x,y in [(16,44),(33,38),(48,47),(31,51)]:
                p.ellipse((x-11,y-5,x+11,y+5),'foliage',1); p.ellipse((x-9,y-5,x+7,y+2),'foliage',3)
        else:
            for _ in range(10):
                x=int(rng.integers(10,54)); y=int(rng.integers(35,57)); height=int(rng.integers(8,30))
                r='thatch' if name=='dry_grass' else 'foliage'
                p.line([(x,y),(x-3,y-height)],r,2,2); p.line([(x,y-3),(x+6,y-height//2)],r,3,2)
                if name in {'wildflowers','clover'}: p.ellipse((x-5,y-height-3,x+1,y-height+3),'flower',3)
                if name=='mushroom_patch': p.rect((x,y-8,x+2,y),'wood',3); p.ellipse((x-4,y-12,x+7,y-5),'hide',2)
    return p.im

def vehicle(kind,p,seed):
    w,h=p.im.size
    if kind in {'raft','canoe','rowing_boat'}:
        if kind=='raft':
            for x in range(8,58,9): p.boards((x,12,x+7,54)); p.ellipse((x,48,x+7,58),'wood',3)
            for y in [24,44]: p.line([(8,y),(57,y)],'bark',0,3)
        else:
            p.poly([(31,6),(47,24),(52,h-24),(32,h-5),(12,h-23),(16,24)],'wood',1)
            p.poly([(31,13),(41,27),(44,h-27),(32,h-14),(20,h-26),(22,27)],'wood',0)
            for y in range(30,h-22,17): p.box(20,y,25,5,'wood')
            p.line([(8,h-27),(53,24)],'wood',3,3)
    elif kind in {'sailboat','cargo_ship'}:
        p.poly([(6,h-42),(w-16,h-47),(w-4,h-30),(w-21,h-10),(25,h-7)],'wood' if kind=='sailboat' else 'metal_iron',1)
        p.poly([(10,h-43),(w-17,h-49),(w-10,h-32),(28,h-20)],'wood',3)
        if kind=='sailboat':
            p.line([(64,h-30),(64,8)],'wood',1,4)
            p.poly([(61,13),(17,75),(60,67)],'cloth_a',2); p.line([(61,16),(22,72),(59,66)],'cloth_a',3,2)
            p.poly([(69,22),(105,69),(69,64)],'cloth_b',2)
        else:
            p.box(w-58,42,35,44,'metal_iron'); p.rect((w-53,48,w-28,58),'ice',1)
            p.box(w-52,26,12,19,'metal_iron')
    elif kind=='bicycle':
        for x in [15,48]:
            p.ellipse((x-10,35,x+10,56),'metal_iron',0); p.ellipse((x-7,38,x+7,53),'metal_iron',3)
        p.line([(15,46),(28,29),(38,46),(15,46),(35,33),(48,46)],'metal_iron',1,3)
        p.line([(44,25),(40,25),(48,46)],'metal_iron',1,3); p.box(24,25,14,4,'hide')
    else:
        metal=kind in {'truck','rover','hover_carrier'}; r='metal_iron' if metal else 'wood'
        if kind!='hover_carrier':
            for x in [18,w-21]:
                p.ellipse((x-9,38,x+9,59),r,0); p.ellipse((x-5,42,x+5,55),r,3)
        else:
            for x in [19,w-22]: p.ellipse((x-11,45,x+11,57),'metal_iron',1); p.line([(x-6,54),(x+6,54)],'ice',3,2)
        p.boards((8,22,w-8,45),r,vertical=False)
        if kind in {'truck','rover'}:
            p.box(w-36,10,25,30,r); p.rect((w-32,14,w-15,25),'ice',1); p.line([(w-31,15),(w-18,15)],'ice',3)
            if kind=='rover': p.line([(w-25,11),(w-29,4)],'metal_iron',1,2)
        elif kind=='wagon':
            p.line([(13,26),(18,10),(w-18,10),(w-13,26)],'wood',1,3)
            p.poly([(18,11),(w-18,11),(w-12,29),(12,29)],'cloth_a',2)
        elif kind=='sled':
            p.line([(10,40),(10,57),(w-10,57),(w-5,51)],'wood',3,4)
        else: p.box(16,15,w-32,20,'metal_iron')
    return p.im
