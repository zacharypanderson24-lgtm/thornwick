using System.Linq;
using CrownAndHearth.Game;
using CrownAndHearth.Game.UI;
using NUnit.Framework;

namespace CrownAndHearth.Tests
{
    public class BuildCatalogueTests
    {
        static BuildCatalogue Real()
        {
            var c = new BuildCatalogue();
            foreach (var t in GameController.Tools.Where(t => BuildCatalogue.IsConstruction(t.Id)))
                c.Add(new CatalogueEntry { Id = t.Id, Label = t.Label, Description = t.Desc, Kind = "", CostText = "", Category = BuildCatalogue.CategoryOf(t.Id, t.Cat.ToString()) });
            return c;
        }

        [Test] public void EveryConstructionToolIsInExactlyOneCategory()
        {
            var c = Real();
            var ids = GameController.Tools.Where(t => BuildCatalogue.IsConstruction(t.Id)).Select(t => t.Id).ToList();
            Assert.AreEqual(ids.Count, c.Entries.Count);
            Assert.AreEqual(ids.Count, c.VisibleCategories().Sum(cat => c.In(cat).Count), "nothing missing, nothing listed twice");
        }

        [Test] public void InspectAndDemolishAreNotInTheCatalogue()
        {
            var c = Real();
            foreach (var id in new[] { "look", "clear", "unzone" }) Assert.IsFalse(c.Entries.Any(e => e.Id == id), id);
        }

        [Test] public void EmptyCategoriesAreHidden()
        {
            var c = Real();
            CollectionAssert.DoesNotContain(c.VisibleCategories(), CatalogueCategory.Decorations);   // nothing to put there yet
            CollectionAssert.Contains(c.VisibleCategories(), CatalogueCategory.Zoning);   // the zones (housing included) moved to the Zoning section
        }

        [Test] public void ANewToolLandsInItsOldCategoryAutomatically()
        {
            Assert.AreEqual(CatalogueCategory.Military, BuildCatalogue.CategoryOf("someNewTower", "Military"));
            Assert.AreEqual(CatalogueCategory.Food, BuildCatalogue.CategoryOf("someNewPen", "Farming"));
        }

        [Test] public void SearchMatchesNamesAndDescriptionsAndNeedsEveryWord()
        {
            var c = Real();
            Assert.IsTrue(c.Search("sawmill").Any(e => e.Id == "sawmill"));
            Assert.IsTrue(c.Search("WELL").Any(e => e.Id == "well"), "case does not matter");
            Assert.AreEqual(0, c.Search("zzzqqq").Count);
            Assert.AreEqual(0, c.Search("  ").Count);
            var both = c.Search("sawmill logs"); Assert.IsTrue(both.All(e => (e.Label + e.Description).ToLower().Contains("sawmill")));
        }

        [Test] public void RecentFavouritesAndLastCategoryAreRemembered()
        {
            var c = Real();
            c.Used("sawmill"); c.Used("well"); c.Used("sawmill");
            CollectionAssert.AreEqual(new[] { "sawmill", "well" }, c.Recent.ToArray());
            Assert.AreEqual(CatalogueCategory.Industry, c.LastCategory);
            c.ToggleFavourite("market"); Assert.IsTrue(c.IsFavourite("market")); c.ToggleFavourite("market"); Assert.IsFalse(c.IsFavourite("market"));
            c.ToggleFavourite("silo");
            var text = c.Serialize();
            var back = Real(); back.Restore(text);
            CollectionAssert.AreEqual(c.Recent.ToArray(), back.Recent.ToArray());
            Assert.IsTrue(back.IsFavourite("silo")); Assert.AreEqual(c.LastCategory, back.LastCategory);
        }

        [Test] public void BrokenOrUnknownSavedTextIsIgnored()
        {
            var c = Real(); c.Restore("cat=99;recent=nope,sawmill,sawmill;fav=ghost;;junk");
            CollectionAssert.AreEqual(new[] { "sawmill" }, c.Recent.ToArray()); Assert.AreEqual(0, c.FavouriteCount);
        }

        [Test] public void RecentListIsCapped()
        {
            var c = Real(); foreach (var e in c.Entries.Take(12).ToList()) c.Used(e.Id);
            Assert.AreEqual(BuildCatalogue.MaxRecent, c.Recent.Count);
        }

        [Test] public void ShortDescriptionIsTheFirstSentenceCutToFit()
        {
            Assert.AreEqual("Stores wheat.", BuildCatalogue.Short("Stores wheat. Needs a road."));
            Assert.LessOrEqual(BuildCatalogue.Short(new string('a', 400), 50).Length, 50);
        }
    }
}
