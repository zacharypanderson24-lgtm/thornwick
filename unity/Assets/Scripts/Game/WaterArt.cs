using System.Collections.Generic;
using UnityEngine;
namespace CrownAndHearth.Game {
 public enum WaterKind { Still, Ocean, River }
 /// <summary>Eight-frame pixel water art. River direction: 0 east/+X, 1 south/+Y, 2 west/-X, 3 north/-Y.
 /// This is a playback helper, not water simulation or local water placement.</summary>
 public static class WaterArt {
  static readonly Dictionary<string,Sprite> Cache = new();
  static readonly string[] Directions = { "east", "south", "west", "north" };
  public static Sprite Frame(WaterKind kind,float seconds,bool shallow=false,int worldDirection=0,int cameraRotation=0,int variant=0) {
   string type=kind==WaterKind.Still?"still":kind==WaterKind.Ocean?"ocean":"river";
   string depth=kind==WaterKind.Still?(shallow?"shallow":"deep"):kind==WaterKind.Ocean?(shallow?"coastal":"deep"):"flow";
   int direction=((worldDirection+cameraRotation)%4+4)%4;
   int frame=((Mathf.FloorToInt(seconds*(kind==WaterKind.Still?4:8))%8)+8)%8;
   string name="water_"+type+"_"+depth+(kind==WaterKind.River?"_"+Directions[direction]:"")+"_v"+(variant&1)+"_f"+frame;
   if(!Cache.TryGetValue(name,out var sprite)){sprite=Resources.Load<Sprite>("Art/Water/"+name);Cache[name]=sprite;}
   return sprite;
  }
 }
}
