from pathlib import Path
from PIL import Image, ImageDraw
import random, json, argparse, colorsys
ROOT=Path(__file__).resolve().parent
SKINS={
"porcelain":["f2ceb1","ffe2c5","dba88b","b77e62","925741"],
"golden":["d9a36c","efbc82","b98252","98613e","784736"],
"warm_brown":["a76543","c5865b","895032","693b29","5e332a"],
"dark_brown":["71432f","976243","5e3527","43251f","40231e"],
"peach":["f6ad6b","ffc080","df9457","bf7848","a45d3d"],
"fair":["e5b78e","f4cda5","ca936d","a76e51","89533f"],
"olive":["c99665","e1ae7a","b07d50","8e593b","754434"],
"tan":["b98155","d49b69","a46b48","805039","754435"],
"brown":["ad704b","ca9165","935737","73452f","69392e"],
"deep":["885137","ad7450","70412e","553025","512a25"]}
HAIRS={
"ash_blond":["50463a","8e8268","c6b89a"],
"auburn":["482b26","824b35","b87651"],
"white":["77716e","b9b5ab","e8dfcf"],
"salt_pepper":["39373b","77757a","b8b1aa"],
"brown":["34241f","69432f","96603d"],
"black":["292326","48332e","705048"],
"blond":["58402c","a47b42","d2aa67"],
"copper":["4b2e29","985538","c17a4b"],
"silver":["4d4949","96938b","ccc5b6"],
"dark_brown":["302523","50392f","78513d"]}
POOLS={'temperate': {'clothing': ['civil', 'merchant', 'purple', 'military', 'field_steward', 'court_scholar', 'frontier_leather', 'ceremonial_ivory']}, 'cold': {'clothing': ['green_fur', 'civil', 'military', 'northern_fur', 'northern_wool', 'frontier_leather']}, 'hot': {'clothing': ['arid', 'desert_indigo', 'desert_ochre', 'linen_steward', 'ceremonial_ivory']}, 'mediterranean': {'clothing': ['mediterranean_linen', 'coastal_administrator', 'merchant', 'ceremonial_ivory', 'linen_steward']}, 'steppe': {'clothing': ['steppe_riding_coat', 'steppe_leather', 'lamellar_commander', 'frontier_leather']}, 'coastal': {'clothing': ['coastal_administrator', 'mediterranean_linen', 'merchant', 'court_scholar']}}
def asset(kind,name):
    return Image.open(ROOT/"assets"/kind/(name+".png")).convert("RGBA")
def recolor(im,old,new):
    mapping={tuple(bytes.fromhex(a)):tuple(bytes.fromhex(b)) for a,b in zip(old,new)}
    return Image.frombytes("RGBA",im.size,bytes(c for r,g,b,a in im.getdata() for c in (*mapping.get((r,g,b),(r,g,b)),a)))
HAIR_STYLES=['bald', 'bowl_cut', 'braided_side', 'buzz_cut', 'cropped', 'curly', 'high_curls', 'long', 'receding', 'short', 'shoulder_length', 'side_part', 'slicked_back', 'swept', 'tonsure', 'wavy', 'widows_peak']
BEARD_STYLES=['chevron', 'chin_beard', 'chinstrap', 'full', 'goatee', 'handlebar', 'mustache', 'mutton_chops', 'none', 'pencil', 'pointed', 'short_beard', 'sideburns', 'soul_patch', 'stubble', 'van_dyke']
HAT_HAIR=["short","cropped","receding","bald","buzz_cut","side_part","tonsure"]
ARCHETYPES={'civil': ['civic_fur', 'civic_doublet', 'civic_mantle'], 'military': ['command_plate', 'command_mail', 'command_lamellar'], 'merchant': ['merchant_brocade', 'merchant_sash', 'merchant_velvet'], 'agricultural': ['estate_overalls', 'estate_linen', 'estate_wool'], 'diplomatic': ['envoy_silk', 'envoy_ivory', 'envoy_court'], 'religious': ['cleric_habit', 'cleric_vestment', 'cleric_ivory'], 'frontier': ['frontier_fur', 'frontier_rider', 'frontier_guard'], 'royal': ['royal_coronation', 'royal_ceremonial', 'royal_winter']}
OUTFIT_HATS={'civil': ['civil', 'none'], 'merchant': ['merchant', 'none'], 'purple': ['merchant', 'none'], 'military': ['military', 'none'], 'arid': ['arid', 'linen_hood', 'none'], 'green_fur': ['northern_fur', 'none', 'civil'], 'mediterranean_linen': ['coastal_cap', 'linen_hood', 'none'], 'desert_indigo': ['arid', 'linen_hood', 'none'], 'desert_ochre': ['arid', 'linen_hood', 'none'], 'steppe_riding_coat': ['steppe_felt', 'none'], 'steppe_leather': ['steppe_felt', 'none'], 'northern_fur': ['northern_fur', 'none'], 'northern_wool': ['northern_fur', 'linen_hood', 'none'], 'frontier_leather': ['steppe_felt', 'northern_fur', 'none'], 'field_steward': ['field_straw', 'none'], 'court_scholar': ['scholar_cap', 'none'], 'ceremonial_ivory': ['scholar_cap', 'linen_hood', 'none'], 'lamellar_commander': ['lamellar_helmet', 'none'], 'coastal_administrator': ['coastal_cap', 'none'], 'linen_steward': ['field_straw', 'linen_hood', 'none'], 'civic_fur': ['civil', 'none'], 'civic_doublet': ['civil', 'none'], 'civic_mantle': ['scholar_cap', 'none'], 'command_plate': ['military', 'none'], 'command_mail': ['military', 'none'], 'command_lamellar': ['lamellar_helmet', 'none'], 'merchant_brocade': ['merchant', 'none'], 'merchant_sash': ['coastal_cap', 'none'], 'merchant_velvet': ['merchant', 'none'], 'estate_overalls': ['field_straw', 'none'], 'estate_linen': ['field_straw', 'none'], 'estate_wool': ['northern_fur', 'none'], 'envoy_silk': ['coastal_cap', 'none'], 'envoy_ivory': ['scholar_cap', 'none'], 'envoy_court': ['civil', 'none'], 'cleric_habit': ['linen_hood', 'none'], 'cleric_vestment': ['scholar_cap', 'none'], 'cleric_ivory': ['linen_hood', 'none'], 'frontier_fur': ['northern_fur', 'none'], 'frontier_rider': ['steppe_felt', 'none'], 'frontier_guard': ['steppe_felt', 'none'], 'royal_coronation': ['civil', 'none'], 'royal_ceremonial': ['scholar_cap', 'none'], 'royal_winter': ['northern_fur', 'none']}
ARCHETYPE_HATS={'civil': ['civil', 'civic_hood', 'civic_bonnet'], 'military': ['military', 'lamellar_helmet', 'command_crest'], 'merchant': ['merchant', 'merchant_tall', 'merchant_wrap'], 'agricultural': ['field_straw', 'estate_cap', 'estate_hood'], 'diplomatic': ['coastal_cap', 'envoy_feather', 'envoy_turban'], 'religious': ['linen_hood', 'cleric_mitre', 'cleric_skullcap'], 'frontier': ['northern_fur', 'steppe_felt', 'frontier_leather_cap'], 'royal': ['royal_crown', 'royal_circlet', 'royal_cap']}
for role, outfits in ARCHETYPES.items():
    for outfit in outfits:
        OUTFIT_HATS[outfit]=ARCHETYPE_HATS[role]+["none"]
PALETTE_LIBRARY=json.loads((ROOT/"palette_library.json").read_text())

HAIR_OCCLUSION=json.loads((ROOT/"hair_occlusion.json").read_text())
def occlude_hair(hair_im, hat_name):
    if hat_name=="none":
        return hair_im
    lines=HAIR_OCCLUSION.get(hat_name)
    if not lines:
        return hair_im
    out=hair_im.copy()
    px=out.load()
    for x,yline in enumerate(lines):
        if yline is None:
            continue
        ymax=min(out.height,yline+1)
        for y in range(ymax):
            if px[x,y][3]:
                px[x,y]=(0,0,0,0)
    return out
def available_colors(kind,name):
    mask=PALETTE_LIBRARY["masks"][kind][name]
    return ["original"]+list(PALETTE_LIBRARY["palettes"][mask["material"]]) if mask["colors"] else ["original"]
def dye(kind,name,variant):
    im=asset(kind,name)
    if variant=="original":return im
    entry=PALETTE_LIBRARY["masks"][kind][name]
    if not entry["colors"]:return im
    target=PALETTE_LIBRARY["palettes"][entry["material"]][variant]
    def rgb(s):return tuple(int(s.lstrip("#")[i:i+2],16)/255 for i in (0,2,4))
    hue,light,sat=colorsys.rgb_to_hls(*rgb(target))
    original=[colorsys.rgb_to_hls(*rgb(c)) for c in entry["colors"]]
    center=(min(v[1] for v in original)+max(v[1] for v in original))/2
    mapping=[]
    for _,l,s in original:
        new_l=max(.12,min(.91,light+(l-center)*.94))
        r,g,b=colorsys.hls_to_rgb(hue,new_l,sat)
        mapping.append("%02x%02x%02x"%(round(r*255),round(g*255),round(b*255)))
    return recolor(im,entry["colors"],mapping)
AGES=['young', 'adult', 'middle_aged', 'elderly', 'venerable']
def generate(seed, climate="temperate", archetype=None):
    rng=random.Random(seed)
    pick=rng.choice
    if archetype is not None:
        outfit=pick(ARCHETYPES[archetype])
    else:
        outfit=pick(POOLS[climate]["clothing"])
    hat=pick(OUTFIT_HATS[outfit])
    hair=pick(HAIR_STYLES)
    return dict(version=19,seed=seed,climate=climate,archetype=archetype,face="standard",skin=pick(list(SKINS)),hair=hair,hair_color=pick(list(HAIRS)),brows=pick(["straight","short"]),eyes="standard",beard=pick(BEARD_STYLES),clothing=outfit,headwear=hat,clothing_color=pick(available_colors("clothing",outfit)),headwear_color=pick(available_colors("headwear",hat)),age=rng.choices(AGES,weights=[15,35,30,16,4])[0])
def render(a):
    im=Image.new("RGBA",(48,48),"#4c516e")
    face=recolor(asset("faces",a["face"]),SKINS["peach"],SKINS[a["skin"]])
    im.alpha_composite(face)
    im.alpha_composite(recolor(asset("age",a.get("age","young")),SKINS["peach"],SKINS[a["skin"]]))
    im.alpha_composite(dye("clothing",a["clothing"],a.get("clothing_color","original")))
    hair=recolor(asset("hair",a["hair"]),HAIRS["brown"],HAIRS[a["hair_color"]])
    hair=occlude_hair(hair,a["headwear"])
    im.alpha_composite(hair)
    for kind,key in [("brows","brows"),("beards","beard")]:
        im.alpha_composite(recolor(asset(kind,a[key]),HAIRS["brown"],HAIRS[a["hair_color"]]))
    im.alpha_composite(asset("eyes",a["eyes"]))
    im.alpha_composite(dye("headwear",a["headwear"],a.get("headwear_color","original")))
    d=ImageDraw.Draw(im)
    d.rectangle((0,0,47,47),outline="#30242a")
    d.rectangle((1,1,46,46),outline="#c39a55")
    return im
if __name__=="__main__":
    p=argparse.ArgumentParser()
    p.add_argument("--seed",type=int,default=42)
    p.add_argument("--climate",choices=POOLS,default="temperate")
    p.add_argument("--archetype",choices=ARCHETYPES,default=None)
    p.add_argument("--output",default="portrait.png")
    args=p.parse_args()
    a=generate(args.seed,args.climate,args.archetype)
    render(a).save(args.output)
    Path(args.output).with_suffix(".json").write_text(json.dumps(a,indent=2))
