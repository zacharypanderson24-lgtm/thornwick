using System.Linq;
using CrownAndHearth.Sim;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class CharterTests
    {
        static World Town() { var w = new World(3, TerrainStyle.Ranges, Relief.Upland, WaterPreset.None); w.Tick(); return w; }

        [Test] public void RankFollowsThePeople()
        {
            Assert.AreEqual(TownRank.Hamlet, World.RankFor(10));
            Assert.AreEqual(TownRank.Village, World.RankFor(25));
            Assert.AreEqual(TownRank.Town, World.RankFor(60));
            Assert.AreEqual(TownRank.City, World.RankFor(500));
        }

        [Test] public void EveryRungOffersOneCharterForEachOfTheSixPaths()
        {
            var paths = new[] { "Farmer", "Trader", "Warlord", "Seafarer", "Industrialist", "Fortress" };
            foreach (var rung in new[] { TownRank.Village, TownRank.Town })
            {
                var w = Town();
                CollectionAssert.AreEquivalent(paths, w.CharterChoices(rung).Select(c => c.Path).ToArray(), rung.ToString());
            }
            Assert.AreEqual(Charter_Ids().Length, Charter_Ids().Distinct().Count(), "ids are unique");
        }

        static string[] Charter_Ids() => World.Charters.Select(c => c.Id).ToArray();

        [Test] public void EveryCharterStatesAGainAndAPrice()
        {
            foreach (var c in World.Charters) { Assert.IsNotEmpty(c.Gain, c.Id); Assert.IsNotEmpty(c.Price, c.Id); }
        }

        [Test] public void AdoptingNeedsTheRankAndOnlyOnePerRung()
        {
            var w = Town(); w.Peasants = 10;
            Assert.IsFalse(w.AdoptCharter("merchant1").Ok, "a hamlet cannot sign a village charter");
            Assert.IsNull(w.OpenCharterRung());
            w.Peasants = 30;
            Assert.AreEqual(TownRank.Village, w.OpenCharterRung());
            Assert.IsTrue(w.AdoptCharter("merchant1").Ok);
            Assert.IsFalse(w.AdoptCharter("martial1").Ok, "one charter for each rung");
            Assert.IsNull(w.OpenCharterRung());
            Assert.AreEqual(1, w.CharterDepth("Trader"));
            Assert.IsFalse(w.AdoptCharter("nonsense").Ok);
            w.Peasants = 70;
            Assert.AreEqual(TownRank.Town, w.OpenCharterRung());
            Assert.IsTrue(w.AdoptCharter("merchant2").Ok);
            Assert.AreEqual(2, w.CharterDepth("Trader"));
        }

        [Test] public void ChoicesSurviveSaveAndLoad()
        {
            var w = Town(); w.Peasants = 70;
            w.AdoptCharter("maritime1"); w.AdoptCharter("martial2");
            var loaded = World.LoadJson(w.SaveJson());
            Assert.AreEqual("maritime1", loaded.CharterAt[TownRank.Village]);
            Assert.AreEqual("martial2", loaded.CharterAt[TownRank.Town]);
        }

        [Test] public void AnOldSaveWithNoCharterFieldLoadsWithNoCharters()
        {
            var w = Town();
            var save = JObject.Parse(w.SaveJson());
            save.Remove("charters");
            var loaded = World.LoadJson(save.ToString());
            Assert.AreEqual(0, loaded.CharterAt.Count);
        }

        [Test] public void ACharterSavedForTheWrongRungOrUnknownIsIgnored()
        {
            var w = Town();
            var save = JObject.Parse(w.SaveJson());
            save["charters"] = new JObject { ["Village"] = "merchant2", ["Town"] = "bogus" };
            var loaded = World.LoadJson(save.ToString());
            Assert.AreEqual(0, loaded.CharterAt.Count);
        }
    }
}
