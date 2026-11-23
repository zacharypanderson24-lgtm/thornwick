using System;
using System.IO;
using CrownAndHearth.Game;
using CrownAndHearth.Sim;
using UnityEditor;
using UnityEngine;
namespace CrownAndHearth.EditorTools {
 public static class BiomeArtVerification {
  [MenuItem("Crown & Hearth/Verify Biome Art")]
  public static void Run() {
   int atlases=0,sprites=0;
   var world=new World(42,TerrainStyle.Ranges,Relief.Upland);
   foreach(Biome biome in Enum.GetValues(typeof(Biome))) {
    if(biome<Biome.TemperateGrassland)continue;
    BiomeArt.Bind(world,biome);
    var atlas=BiomeArt.Atlas(world);
    if(atlas==null||atlas.width!=608||atlas.height!=16||atlas.filterMode!=FilterMode.Point||atlas.wrapMode!=TextureWrapMode.Clamp)
     throw new InvalidOperationException("Invalid biome atlas: "+biome);
    atlases++;
    foreach(string name in BiomeArt.Species(biome)) {
     string stem=name.Substring(0,name.Length-2);
     foreach(string suffix in new[]{"_0","_1","_grove_2","_grove_3","_grove_4","_sapling"})Verify(stem+suffix);
    }
    foreach(string name in BiomeArt.Plants(biome))Verify(name);
    foreach(string name in BiomeArt.Rocks(biome))Verify(name);
    for(int y=80;y<100;y++)for(int x=80;x<100;x++) {
     int cell=BiomeArt.GroundCell(world,x,y);
     if(cell<0||cell>=BiomeArt.CellCount)throw new InvalidOperationException("Atlas cell out of bounds");
     if(BiomeArt.Tree(world,x,y)==null||BiomeArt.Tree(world,x,y,true)==null)throw new InvalidOperationException("Missing tree or sapling: "+biome);
    }
   }
   var other=new World(43,TerrainStyle.Ranges,Relief.Upland);
   BiomeArt.Bind(world,Biome.Desert);BiomeArt.Bind(other,Biome.TropicalRainforest);
   if(BiomeArt.For(world)!=Biome.Desert||BiomeArt.For(other)!=Biome.TropicalRainforest)throw new InvalidOperationException("World biome bindings leaked");
   string report="{\"status\":\"PASS\",\"atlases\":"+atlases+",\"sprites\":"+sprites+",\"worldBindings\":true,\"groundCells\":true}";
   File.WriteAllText(Path.Combine(Application.dataPath,"..","biome-art-verification.json"),report);
   Debug.Log("[BiomeArt] "+report);
   void Verify(string name) {
    var s=Resources.Load<Sprite>("Art/Biomes/"+name);
    if(s==null||s.rect.width!=32||s.rect.height!=48||s.pivot!=new Vector2(16,0)||s.pixelsPerUnit!=32||s.texture.filterMode!=FilterMode.Point)
     throw new InvalidOperationException("Invalid biome sprite: "+name);
    sprites++;
   }
  }
 }
}
