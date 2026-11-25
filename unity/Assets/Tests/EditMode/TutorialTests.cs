using System.Linq;
using CrownAndHearth.Sim;
using NUnit.Framework;
namespace CrownAndHearth.Tests {
 public class TutorialTests {
  static int Index(string id)=>Tutorial.Steps.Select((s,i)=>(s,i)).First(p=>p.s.Id==id).i;
  static Tutorial At(World w,string id){var t=new Tutorial();t.Start(w,Index(id));return t;}
  [Test] public void EveryStepFitsThePanelAndHasAUniqueIdAndGoal(){
   Assert.That(Tutorial.Steps.Count,Is.GreaterThanOrEqualTo(20));Assert.AreEqual(Tutorial.Steps.Count,Tutorial.Steps.Select(s=>s.Id).Distinct().Count());
   foreach(var s in Tutorial.Steps){Assert.IsNotEmpty(s.Title,s.Id);Assert.IsNotEmpty(s.Body,s.Id);Assert.IsNotEmpty(s.Goal,s.Id);Assert.That(s.Body.Length,Is.LessThan(560),s.Id);}
  }
  [Test] public void FoodAndIndustryPrecedeHousingExpansion(){
   foreach(var id in new[]{"farmland","services","hunting","lumber","sawmill","harvest","stone","mine","ore","winter","year"})Assert.That(Index(id),Is.LessThan(Index("housing")),id);
   Assert.That(Index("services"),Is.LessThan(Index("harvest")));Assert.That(Index("time"),Is.LessThan(Index("camera")));
  }
  [Test] public void TutorialNeverSpawnsAnArtificialRaid(){
   var w=new World(7);var t=new Tutorial();t.Start(w);for(int i=0;i<Tutorial.Steps.Count;i++){Assert.IsNull(w.Raid);t.Next(w);}Assert.IsTrue(t.Finished);Assert.IsFalse(t.Active);
  }
  [Test] public void CameraSignalsBackAndCloseWork(){
   var w=new World(7);var t=At(w,"camera");t.Panned=true;Assert.IsNull(t.Check(w));t.Zoomed=true;Assert.AreEqual("camera",t.Check(w)?.Id);Assert.AreEqual("workers",t.Current.Id);Assert.IsFalse(t.Panned);t.Back(w);Assert.AreEqual("camera",t.Current.Id);t.Close();Assert.IsFalse(t.Active);
  }
  [Test] public void UsableExistingFieldsCountButFlaxDoesNotPass(){
   var w=new World(7);FirstYearPlan.Fields(w,18);var t=At(w,"farmland");w.SetFlaxPercent(100);Assert.IsNull(t.Check(w));w.SetFlaxPercent(0);Assert.AreEqual("farmland",t.Check(w)?.Id);
  }
  [Test] public void SiloMustBeConnectedAndHuntingMustBeStaffed(){
   var w=new World(7);w.AddBld("silo",200,200);w.Recalc();Assert.IsNull(At(w,"services").Check(w));FirstYearPlan.Build(w,"silo");Assert.AreEqual("services",At(w,"services").Check(w)?.Id);
   var lodge=FirstYearPlan.Build(w,"hunter");lodge.Workers=0;Assert.IsNull(At(w,"hunting").Check(w));w.Recalc();Assert.AreEqual("hunting",At(w,"hunting").Check(w)?.Id);
  }
  [Test] public void StarterFoodDoesNotCountAsHarvest(){var w=new World(7);Assert.AreEqual(1300,w.ResAmt[Res.Food]);Assert.IsNull(At(w,"harvest").Check(w));}
  [Test] public void SafetyRequiresSupports(){
   var w=new World(7);var t=At(w,"danger");Assert.IsNull(t.Check(w));w.SetMineSupportsRequired(true);Assert.AreEqual("danger",t.Check(w)?.Id);
  }
  [Test] public void SmokedMeatAndEggsCountForTheReserveGoal(){var w=new World(7);w.ResAmt[Res.Food]=0;w.ResAmt[Res.SmokedMeat]=500;w.ResAmt[Res.Eggs]=60;Assert.AreEqual("reserve",At(w,"reserve").Check(w)?.Id);}
  [Test] public void PassingTimeAndStockpilingDoNotReplaceIndustryGoals(){var w=new World(7);w.Day=361;w.ResAmt[Res.Food]=4000;Assert.IsNull(At(w,"year").Check(w));}
 }
}
