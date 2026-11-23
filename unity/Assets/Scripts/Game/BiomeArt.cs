// Coded biome art selected from strategic location; no saved terrain or resource state is changed.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CrownAndHearth.Sim;
using UnityEngine;
namespace CrownAndHearth.Game {
 public static class BiomeArt {
  sealed class Binding { public Biome Value; }
  static readonly ConditionalWeakTable<World, Binding> Bindings = new();
  static readonly Dictionary<Biome, Texture2D> Atlases = new();
  static readonly Dictionary<string, Sprite> Sprites = new();
  public const int CellCount = 19;
  public static Biome For(World w) => Bindings.TryGetValue(w,out var b)?b.Value:Biome.TemperateGrassland;
  public static void Bind(World w,Biome biome) { if(biome<Biome.TemperateGrassland||biome>Biome.TropicalHighlands)biome=Biome.TemperateGrassland;var b=Bindings.GetValue(w,_=>new Binding());if(b.Value==biome)return;b.Value=biome;w.Touch(); }
  public static Texture2D Atlas(World w) { var b=For(w);if(!Atlases.TryGetValue(b,out var t)){t=Resources.Load<Texture2D>("Art/Biomes/"+b+"_atlas");Atlases[b]=t;}return t; }
  static Sprite Load(string name) { if(name==null)return null;if(!Sprites.TryGetValue(name,out var s)){s=Resources.Load<Sprite>("Art/Biomes/"+name);Sprites[name]=s;}return s; }
  static readonly Dictionary<Biome,string[]> SpeciesCache = new(), PlantCache = new(), RockCache = new();
  static string[] Cached(Dictionary<Biome,string[]> cache,Biome b,System.Func<Biome,string[]> load) { if(!cache.TryGetValue(b,out var a)){a=load(b);cache[b]=a;}return a; }
  public static string[] Species(Biome b)=>Cached(SpeciesCache,b,SpeciesList);
  public static string[] Plants(Biome b)=>Cached(PlantCache,b,PlantList);
  public static string[] Rocks(Biome b)=>Cached(RockCache,b,RockList);
  static string[] SpeciesList(Biome b) { switch(b) {
case Biome.TemperateGrassland: return new[] { "grassland_oak_0", "grassland_birch_0" };
case Biome.TemperateForest: return new[] { "forest_oak_0", "forest_beech_0", "forest_birch_0" };
case Biome.BorealForest: return new[] { "boreal_spruce_0", "boreal_pine_0", "boreal_birch_0" };
case Biome.Highlands: return new[] { "highlands_scots_pine_0", "highlands_rowan_0", "highlands_silver_birch_0" };
case Biome.AlpineMountains: return new[] { "alpine_mountain_pine_0", "alpine_larch_0", "alpine_dwarf_pine_0" };
case Biome.Wetlands: return new[] { "wetlands_weeping_willow_0", "wetlands_alder_0", "wetlands_white_willow_0" };
case Biome.MediterraneanScrub: return new[] { "mediterranean_olive_0", "mediterranean_cypress_0", "mediterranean_umbrella_pine_0" };
case Biome.Steppe: return new[] { "steppe_siberian_elm_0", "steppe_river_poplar_0" };
case Biome.SemiAridScrub: return new[] { "semi_arid_scrub_tamarisk_0", "semi_arid_scrub_juniper_0" };
case Biome.Tundra: return System.Array.Empty<string>();
case Biome.Desert: return new[] { "desert_date_palm_0", "desert_desert_acacia_0" };
case Biome.Savanna: return new[] { "savanna_flat_top_acacia_0", "savanna_baobab_0", "savanna_fan_palm_0" };
case Biome.SubtropicalForest: return new[] { "subtropical_forest_camphor_0", "subtropical_forest_magnolia_0", "subtropical_forest_fan_palm_0" };
case Biome.TropicalRainforest: return new[] { "tropical_rainforest_kapok_0", "tropical_rainforest_strangler_fig_0", "tropical_rainforest_rainforest_palm_0" };
case Biome.TropicalSeasonalForest: return new[] { "tropical_seasonal_forest_teak_0", "tropical_seasonal_forest_sal_0", "tropical_seasonal_forest_palmyra_0" };
case Biome.TropicalHighlands: return new[] { "tropical_highlands_tree_fern_0", "tropical_highlands_cloud_forest_laurel_0", "tropical_highlands_mountain_podocarp_0" };
default:return System.Array.Empty<string>();} }
  static string[] PlantList(Biome b) { switch(b) {
case Biome.TemperateGrassland: return new[] { "grassland_shrub_0", "grassland_shrub_1", "grassland_shrub_2" };
case Biome.TemperateForest: return new[] { "forest_shrub_0", "forest_shrub_1", "forest_shrub_2" };
case Biome.BorealForest: return new[] { "boreal_shrub_0", "boreal_shrub_1", "boreal_shrub_2" };
case Biome.Highlands: return new[] { "highlands_heather_0", "highlands_heather_1", "highlands_heather_2" };
case Biome.AlpineMountains: return new[] { "alpine_cushion_0", "alpine_cushion_1", "alpine_cushion_2" };
case Biome.Wetlands: return new[] { "wetlands_cattails_0", "wetlands_cattails_1", "wetlands_cattails_2", "wetlands_reeds_0", "wetlands_reeds_1", "wetlands_reeds_2" };
case Biome.MediterraneanScrub: return new[] { "mediterranean_lavender_0", "mediterranean_lavender_1", "mediterranean_lavender_2", "mediterranean_scrub_0", "mediterranean_scrub_1", "mediterranean_scrub_2" };
case Biome.Steppe: return new[] { "steppe_plant_feather_grass_0", "steppe_plant_feather_grass_1", "steppe_plant_feather_grass_2", "steppe_plant_wormwood_0", "steppe_plant_wormwood_1", "steppe_plant_wormwood_2" };
case Biome.SemiAridScrub: return new[] { "semi_arid_scrub_plant_dry_tussock_0", "semi_arid_scrub_plant_dry_tussock_1", "semi_arid_scrub_plant_dry_tussock_2", "semi_arid_scrub_plant_thorn_scrub_0", "semi_arid_scrub_plant_thorn_scrub_1", "semi_arid_scrub_plant_thorn_scrub_2" };
case Biome.Tundra: return new[] { "tundra_plant_dwarf_birch_0", "tundra_plant_dwarf_birch_1", "tundra_plant_dwarf_birch_2", "tundra_plant_dwarf_willow_0", "tundra_plant_dwarf_willow_1", "tundra_plant_dwarf_willow_2", "tundra_plant_lichen_cushion_0", "tundra_plant_lichen_cushion_1", "tundra_plant_lichen_cushion_2" };
case Biome.Desert: return new[] { "desert_plant_desert_grass_0", "desert_plant_desert_grass_1", "desert_plant_desert_grass_2", "desert_plant_saltbush_0", "desert_plant_saltbush_1", "desert_plant_saltbush_2" };
case Biome.Savanna: return new[] { "savanna_plant_tall_grass_0", "savanna_plant_tall_grass_1", "savanna_plant_tall_grass_2", "savanna_plant_thorn_scrub_0", "savanna_plant_thorn_scrub_1", "savanna_plant_thorn_scrub_2" };
case Biome.SubtropicalForest: return new[] { "subtropical_forest_plant_bamboo_0", "subtropical_forest_plant_bamboo_1", "subtropical_forest_plant_bamboo_2", "subtropical_forest_plant_fern_0", "subtropical_forest_plant_fern_1", "subtropical_forest_plant_fern_2" };
case Biome.TropicalRainforest: return new[] { "tropical_rainforest_plant_banana_0", "tropical_rainforest_plant_banana_1", "tropical_rainforest_plant_banana_2", "tropical_rainforest_plant_fern_0", "tropical_rainforest_plant_fern_1", "tropical_rainforest_plant_fern_2" };
case Biome.TropicalSeasonalForest: return new[] { "tropical_seasonal_forest_plant_bamboo_0", "tropical_seasonal_forest_plant_bamboo_1", "tropical_seasonal_forest_plant_bamboo_2", "tropical_seasonal_forest_plant_dry_tussock_0", "tropical_seasonal_forest_plant_dry_tussock_1", "tropical_seasonal_forest_plant_dry_tussock_2" };
case Biome.TropicalHighlands: return new[] { "tropical_highlands_plant_fern_0", "tropical_highlands_plant_fern_1", "tropical_highlands_plant_fern_2", "tropical_highlands_plant_montane_tussock_0", "tropical_highlands_plant_montane_tussock_1", "tropical_highlands_plant_montane_tussock_2" };
default:return System.Array.Empty<string>();} }
  static string[] RockList(Biome b) { switch(b) {
case Biome.Highlands:return new[] { "highlands_outcrop_0", "highlands_outcrop_1", "highlands_outcrop_2", "highlands_outcrop_3" };
case Biome.AlpineMountains:return new[] { "alpine_outcrop_0", "alpine_outcrop_1", "alpine_outcrop_2", "alpine_outcrop_3" };
default:return System.Array.Empty<string>();} }
  static string Pick(string[] a,int x,int y,int salt) => a.Length==0?null:a[System.Math.Min(a.Length-1,(int)(Noise.Hash2(x,y,salt)*a.Length))];
  public static Sprite Tree(World w,int x,int y,bool sapling=false) {
   var species=Species(For(w));
   // Preserve old-save wood resources in treeless tundra as dwarf woody shrubs.
   if(species.Length==0)return Load(Pick(Plants(For(w)),x,y,71));
   string baseName=Pick(species,x,y,69),stem=baseName.Substring(0,baseName.Length-2);
   if(sapling)return Load(stem+"_sapling")??Load(baseName);
   var grove=w.TreeGrove(x,y);int n=System.Math.Max(1,System.Math.Min(4,grove.Item1));
   return Load(n>1?stem+"_grove_"+n:stem+"_"+(Noise.Hash2(x,y,73)<.5?0:1))??Load(baseName);
  }
  public static Sprite Rock(World w,int x,int y)=>Load(Pick(Rocks(For(w)),x,y,75));
  public static Sprite Understory(World w,int x,int y) {
   if(w.ZoneMap[y,x]!=Zone.None||w.IsMountain(x,y)||Noise.Hash2(x,y,77)>.045)return null;
   return Load(Pick(Plants(For(w)),x,y,79));
  }
  public static int GroundCell(World w,int x,int y) {
   if(w.IsMountain(x,y))return w.TileLevel(x,y)>=Defs.SnowLevel?ArtLibrary.CellSnow:ArtLibrary.CellRock;
   if(w.DecoMap[y,x]==Deco.Rock&&w.RockLevel(x,y)<=0)return ArtLibrary.CellWorkedStone;
   double patch=Noise.Hash2(x/6,y/6,w.Seed+83),variant=Noise.Hash2(x,y,85);
   int offset=w.Fert[y,x]?4+(variant<.5?0:1):patch>.82?6+(variant<.5?0:1):(int)(variant*4);
   return 11+System.Math.Min(7,offset);
  }
 }
}
