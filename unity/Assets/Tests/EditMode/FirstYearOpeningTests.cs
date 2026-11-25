using System;using System.Linq;using System.Collections.Generic;using CrownAndHearth.Sim;
internal static class FirstYearPlan {
 public static readonly List<string> Actions=new List<string>();
 static int Dist(int x,int y,int a,int b)=>Math.Max(Math.Abs(x-a),Math.Abs(y-b));
 static readonly (int x,int y)[] Dirs={(1,0),(-1,0),(0,1),(0,-1)};
 static List<(int x,int y)> RoadTo(World w,int x,int y,int bx,int by,int size){
  var prev=new Dictionary<int,int>();var queue=new Queue<int>();int start=World.Tk(x,y);queue.Enqueue(start);prev[start]=-1;int end=-1;
  while(queue.Count>0){int k=queue.Dequeue(),cx=k%Defs.N,cy=k/Defs.N;if(w.Grid[cy,cx]?.T=="road"&&w.Grid[cy,cx].Served){end=k;break;}
   foreach(var d in Dirs){int nx=cx+d.x,ny=cy+d.y,n=World.Tk(nx,ny);if(!World.Inb(nx,ny)||prev.ContainsKey(n)||!w.Claimed(nx,ny)||Dist(nx,ny,Defs.Start,Defs.Start)>30||(nx>=bx&&nx<bx+size&&ny>=by&&ny<by+size))continue;
    if(w.Grid[ny,nx]!=null&&w.Grid[ny,nx].T!="road"||w.ZoneMap[ny,nx]==Zone.Farm||w.IsSolid(nx,ny))continue;prev[n]=k;queue.Enqueue(n);}}
  if(end<0)return null;var path=new List<(int,int)>();for(int k=end;k>=0;k=prev[k])path.Add((k%Defs.N,k/Defs.N));return path;
 }
 public static Building Build(World w,string type){
  int size=Defs.PlacementSize(type);Building hunter=w.Blds.FirstOrDefault(b=>b.T=="hunter");var candidates=new List<(double score,int x,int y)>();
  for(int y=Defs.Start-24;y<=Defs.Start+24;y++)for(int x=Defs.Start-24;x<=Defs.Start+24;x++){
   if(!w.CanPlace(type,x,y).Ok||w.ZonedUnder(type,x,y)>0)continue;int resource=0,nearest=99;
   for(int j=y-6;j<y+size+6;j++)for(int i=x-6;i<x+size+6;i++){bool good=(type=="hunter"||type=="wood")?w.HasTree(i,j):type=="quarry"?w.DecoMap[j,i]==Deco.Rock:type=="mine"?w.DecoMap[j,i]==Deco.Ore:false;if(!good)continue;resource++;nearest=Math.Min(nearest,Dist(x,y,i,j));}
   if((type=="hunter"||type=="wood")&&resource<8)continue;if(type=="quarry"&&resource<3)continue;if(type=="mine"&&w.MineFaceAt(x,y,size)<0)continue;
   double score=Dist(x,y,Defs.Start,Defs.Start)+(type=="hunter"||type=="wood"?nearest*5-resource*.04:0);
   if(type=="mine"&&resource>0)score-=25+resource; if(type=="wood"&&hunter!=null&&Dist(x,y,hunter.X,hunter.Y)<10)score+=60;
   candidates.Add((score,x,y));}
  foreach(var c in candidates.OrderBy(c=>c.score).Take(100)){
   var edge=new List<(int x,int y)>();int face=type=="mine"?w.MineFaceAt(c.x,c.y,size):-1;
   for(int i=0;i<size;i++)for(int j=0;j<size;j++)foreach(var d in Dirs){if(face>=0&&(d.x!=Defs.FaceDirs[face,0]||d.y!=Defs.FaceDirs[face,1]))continue;int ex=c.x+i+d.x,ey=c.y+j+d.y;if(ex>=c.x&&ex<c.x+size&&ey>=c.y&&ey<c.y+size)continue;if(w.Grid[ey,ex]!=null&&w.Grid[ey,ex].T!="road"||w.IsSolid(ex,ey)||w.ZoneMap[ey,ex]==Zone.Farm)continue;edge.Add((ex,ey));}
   var routes=edge.Select(e=>RoadTo(w,e.x,e.y,c.x,c.y,size)).Where(p=>p!=null).OrderBy(p=>p.Count).ToList();if(routes.Count==0)continue;var route=routes[0];if(route.Count>22)continue;
   foreach(var t in route)if(w.Grid[t.y,t.x]==null&&w.DecoMap[t.y,t.x]!=Deco.None)w.Remove(t.x,t.y);
   var road=w.PlaceRoadPath(route);if(!road.Ok)continue;var result=w.Place(type,c.x,c.y);if(!result.Ok)continue;var b=w.Grid[c.y,c.x];w.Recalc();if(!b.Served)throw new Exception(type+" not served");Actions.Add(w.Day+":"+type+"@"+c.x+","+c.y+" roads="+route.Count);return b;
  }throw new Exception("No affordable reachable site for "+type+" on seed "+w.Seed+", wood="+w.ResAmt[Res.Wood]);
 }
 public static void Fields(World w,int count){var farm=w.Blds.Single(b=>b.T=="farm");w.SetFlaxPercent(0);for(int r=1;r<=World.FarmReach&&w.FarmFields().Count<count;r++)for(int y=farm.Y-r;y<=farm.Y+farm.Sz-1+r&&w.FarmFields().Count<count;y++)for(int x=farm.X-r;x<=farm.X+farm.Sz-1+r&&w.FarmFields().Count<count;x++){if(w.Grid[y,x]!=null||!w.Fert[y,x])continue;w.ZoneRect(Zone.Farm,x,y,x,y);}if(w.FarmFields().Count<count)throw new Exception("Not enough usable fields");}
 public static void Lumber(World w,Building lodge){var hunter=w.Blds.First(b=>b.T=="hunter");int marked=0;for(int r=1;r<=Defs.WorkR&&marked<18;r++)for(int y=lodge.Y-r;y<=lodge.Y+lodge.Sz-1+r&&marked<18;y++)for(int x=lodge.X-r;x<=lodge.X+lodge.Sz-1+r&&marked<18;x++)if(w.HasTree(x,y)&&Dist(x,y,hunter.X,hunter.Y)>7&&w.SetZone(Zone.Wood,x,y).Ok)marked++;w.Recalc();Actions.Add("lumber tiles="+marked);}
 public static void Mining(World w,Building b){for(int y=b.Y-8;y<=b.Y+b.Sz+8;y++)for(int x=b.X-8;x<=b.X+b.Sz+8;x++)if(w.ZoneMap[y,x]==Zone.None)w.SetZone(Zone.Mine,x,y);w.Recalc();}
 // Feed only the camera/panel signals a player supplies; world goals still use the simulated village.
 static void AdvanceTutorial(World w,Tutorial t){for(int guard=0;guard<Tutorial.Steps.Count&&!t.Finished;guard++){
  if(t.Current.Explanation){t.Next(w);continue;}
  t.SpeedChanged=t.Panned=t.Zoomed=t.Rotated=t.WorkersOpened=t.UndergroundViewed=true;
  if(t.Check(w)==null)break;
 }}
 public static World Run(int seed,int plots,int mineDay){Actions.Clear();var w=new World(seed);var tutorial=new Tutorial();tutorial.Start(w);Fields(w,plots);Build(w,"silo");Build(w,"hunter");double min=99999;int firstStarve=0;double firstOre=0;int firstOreDay=0;double logs=0,stone=0;int firstHarvest=0;double before;for(int i=1;i<=360;i++){
  if(i==30)Lumber(w,Build(w,"wood"));if(i==60)Build(w,"sawmill");if(i==120)Mining(w,Build(w,"quarry"));if(i==mineDay){w.SetMineSupportsRequired(true);Mining(w,Build(w,"mine"));}
  before=w.ResAmt[Res.Food];double oldOre=w.ResAmt[Res.Ore],oldLogs=w.ResAmt[Res.Logs],oldStone=w.ResAmt[Res.Stone];w.Tick();if(firstHarvest==0&&w.ResAmt[Res.Food]>before+50)firstHarvest=i;if(firstOreDay==0&&w.ResAmt[Res.Ore]>oldOre+.01)firstOreDay=i;firstOre+=Math.Max(0,w.ResAmt[Res.Ore]-oldOre);logs+=Math.Max(0,w.ResAmt[Res.Logs]-oldLogs);stone+=Math.Max(0,w.ResAmt[Res.Stone]-oldStone);min=Math.Min(min,w.EdibleFoodStored);if(w.Starving)throw new Exception("Starvation on day "+i);if(w.WaterShortDays>0)throw new Exception("Thirst on "+i); AdvanceTutorial(w,tutorial);
  if(i==180)Console.WriteLine("summer food="+Math.Round(w.EdibleFoodStored)+" pop="+w.Peasants);
 }Console.WriteLine("SEED="+seed+" plots="+plots+" mineDay="+mineDay+" starve="+firstStarve+" minFood="+Math.Round(min,1)+" finalFood="+Math.Round(w.EdibleFoodStored,1)+" pop="+w.Peasants+" wood="+Math.Round(w.ResAmt[Res.Wood])+" logsDeliveredNet="+logs+" stone="+stone+" ore="+Math.Round(firstOre,1)+" oreFirst="+firstOreDay+" harvest="+firstHarvest+" deaths="+w.MinerDeaths+" actions="+string.Join(";",Actions));
 NUnit.Framework.Assert.That(tutorial.Finished,NUnit.Framework.Is.True,"Tutorial stuck at "+tutorial.Current?.Id);
 return w;
 }
}

namespace CrownAndHearth.Tests {
 public class FirstYearOpeningTests {
  [NUnit.Framework.TestCase(7)] [NUnit.Framework.TestCase(42)] [NUnit.Framework.TestCase(123)]
  public void EighteenWheatFieldsAndStagedIndustrySurviveYearOne(int seed) {
   var w=FirstYearPlan.Run(seed,18,120);
   NUnit.Framework.Assert.That(w.Day,NUnit.Framework.Is.EqualTo(361));
   NUnit.Framework.Assert.That(w.EdibleFoodStored,NUnit.Framework.Is.GreaterThanOrEqualTo(w.DailyFoodDemand*100));
   NUnit.Framework.Assert.That(w.ResAmt[Res.Ore],NUnit.Framework.Is.GreaterThan(10),"ore must be delivered, not just a mine placed");
   NUnit.Framework.Assert.That(w.ResAmt[Res.Stone],NUnit.Framework.Is.GreaterThan(30));
   NUnit.Framework.Assert.That(w.ResAmt[Res.Wood],NUnit.Framework.Is.GreaterThan(120));
   NUnit.Framework.Assert.That(w.MinerDeaths,NUnit.Framework.Is.Zero);
   foreach(string type in new[]{"hunter","wood","sawmill","quarry","mine"})NUnit.Framework.Assert.That(w.Blds.Any(b=>b.T==type&&b.Served),NUnit.Framework.Is.True,type);
  }
 }
}
