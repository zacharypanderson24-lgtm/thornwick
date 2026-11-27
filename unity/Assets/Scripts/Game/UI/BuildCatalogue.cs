// The logic behind the Build catalogue, with no UnityEngine: which of the nine catalogue categories a construction tool belongs to,
// search, and the remembered favourites / recently used / last category. The Hud draws it; tests check it.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Game.UI
{
    public enum CatalogueCategory { Housing, Food, Resources, Land, Industry, Services, Transport, Military, Government, Decorations, Zoning }   // Zoning is last in the enum (saved numbers stay valid) but first on screen

    public sealed class CatalogueEntry
    {
        public string Id, Label, Description, Kind, CostText;
        public CatalogueCategory Category;
        public int Order;
    }

    public enum CardStatus { Available, Unaffordable, Locked }

    public sealed class BuildCatalogue
    {
        public const int MaxRecent = 8;

        public static string Title(CatalogueCategory c) => c switch
        {
            CatalogueCategory.Housing => "Housing",
            CatalogueCategory.Food => "Food and Agriculture",
            CatalogueCategory.Resources => "Natural Resources",
            CatalogueCategory.Land => "Land Management",
            CatalogueCategory.Industry => "Industry",
            CatalogueCategory.Services => "Public Services and Utilities",
            CatalogueCategory.Transport => "Trade and Transportation",
            CatalogueCategory.Military => "Military and Defense",
            CatalogueCategory.Government => "Government",
            CatalogueCategory.Zoning => "Zoning",
            _ => "Decorations",
        };

        /// <summary>Where a construction tool is listed. Tools not named here fall back to the tool's old category, so a tool added later still lands somewhere sensible.</summary>
        public static CatalogueCategory CategoryOf(string toolId, string oldCategory)
        {
            switch (toolId)
            {
                case "zoneRes": case "zoneRes2": case "zoneFarm": case "zoneWood": case "zoneMine": case "zoneQuarry": case "zoneHunt": case "zoneFish": return CatalogueCategory.Zoning;
                case "farmhouse": case "silo": case "pigPen": case "sheepPen": case "chickenPen":
                case "hunter": case "smokehouse": case "fisher": case "dock": case "watermill": return CatalogueCategory.Food;
                case "lodge": case "forester": case "quarry": case "mine": return CatalogueCategory.Resources;
                case "clearLand": case "protectWood": case "unprotectWood": return CatalogueCategory.Land;
                case "sawmill": case "charcoal": case "smithery": return CatalogueCategory.Industry;
                case "well": case "cistern": case "canal": return CatalogueCategory.Services;
                case "road": case "bridge": case "ferry": case "ferryRoute": case "port": case "boatyard": case "market": case "storehouse": return CatalogueCategory.Transport;
                case "muster": case "barracks": case "wall": case "wall2": case "watchtower": case "moat": return CatalogueCategory.Military;
                case "claim": return CatalogueCategory.Government;
            }
            switch (oldCategory)
            {
                case "Housing": return CatalogueCategory.Housing;
                case "Farming": case "Hunting": return CatalogueCategory.Food;
                case "Forestry": case "Mining": return CatalogueCategory.Resources;
                case "Trade": case "Roads": return CatalogueCategory.Transport;
                case "Military": return CatalogueCategory.Military;
                case "Water": return CatalogueCategory.Services;
                case "Land": return CatalogueCategory.Government;
                default: return CatalogueCategory.Decorations;
            }
        }

        /// <summary>The tools that are things to construct. Inspect, Demolish and Remove Zones are tools, not buildings, and stay on the quick bar.</summary>
        public static bool IsConstruction(string toolId) => toolId != "look" && toolId != "clear" && toolId != "unzone";

        readonly List<CatalogueEntry> _entries = new List<CatalogueEntry>();
        readonly List<string> _recent = new List<string>();
        readonly HashSet<string> _favourites = new HashSet<string>();
        public CatalogueCategory LastCategory = CatalogueCategory.Housing;

        public IReadOnlyList<CatalogueEntry> Entries => _entries;
        public IReadOnlyList<string> Recent => _recent;
        public bool IsFavourite(string id) => _favourites.Contains(id);
        public int FavouriteCount => _favourites.Count;

        public void Add(CatalogueEntry e) { e.Order = _entries.Count; _entries.Add(e); }

        /// <summary>Categories that hold at least one entry, in the fixed display order. Empty ones are not shown.</summary>
        public List<CatalogueCategory> VisibleCategories() =>
            Enum.GetValues(typeof(CatalogueCategory)).Cast<CatalogueCategory>().Where(c => _entries.Any(e => e.Category == c)).OrderBy(c => c == CatalogueCategory.Zoning ? 0 : 1).ThenBy(c => (int)c).ToList();

        public List<CatalogueEntry> In(CatalogueCategory c) => _entries.Where(e => e.Category == c).OrderBy(e => e.Order).ToList();

        /// <summary>Entries whose name or description contains every word typed (any category). An empty query matches nothing: the caller shows the category instead.</summary>
        public List<CatalogueEntry> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<CatalogueEntry>();
            var words = query.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return _entries.Where(e => words.All(w => e.Label.ToLowerInvariant().Contains(w) || (e.Description ?? "").ToLowerInvariant().Contains(w)))
                .OrderByDescending(e => words.Any(w => e.Label.ToLowerInvariant().Contains(w))).ThenBy(e => e.Order).ToList();
        }

        public List<CatalogueEntry> RecentEntries() => _recent.Select(id => _entries.FirstOrDefault(e => e.Id == id)).Where(e => e != null).ToList();
        public List<CatalogueEntry> FavouriteEntries() => _entries.Where(e => _favourites.Contains(e.Id)).OrderBy(e => e.Order).ToList();

        public void Used(string id)
        {
            _recent.Remove(id); _recent.Insert(0, id);
            if (_recent.Count > MaxRecent) _recent.RemoveRange(MaxRecent, _recent.Count - MaxRecent);
            var e = _entries.FirstOrDefault(x => x.Id == id); if (e != null) LastCategory = e.Category;
        }

        public bool ToggleFavourite(string id) => _favourites.Add(id) || !_favourites.Remove(id);

        // ----- remembered between sessions -----
        public string Serialize() => $"cat={(int)LastCategory};recent={string.Join(",", _recent)};fav={string.Join(",", _favourites.OrderBy(x => x, StringComparer.Ordinal))}";

        public void Restore(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var part in text.Split(';'))
            {
                int eq = part.IndexOf('='); if (eq < 0) continue;
                string k = part.Substring(0, eq), v = part.Substring(eq + 1);
                if (k == "cat" && int.TryParse(v, out int c) && Enum.IsDefined(typeof(CatalogueCategory), c)) LastCategory = (CatalogueCategory)c;
                else if (k == "recent") { _recent.Clear(); foreach (var id in v.Split(',')) if (id.Length > 0 && _entries.Any(e => e.Id == id) && !_recent.Contains(id)) _recent.Add(id); }
                else if (k == "fav") { _favourites.Clear(); foreach (var id in v.Split(',')) if (id.Length > 0 && _entries.Any(e => e.Id == id)) _favourites.Add(id); }
            }
        }

        /// <summary>The first sentence of a description, cut to fit a card.</summary>
        public static string Short(string description, int max = 96)
        {
            if (string.IsNullOrEmpty(description)) return "";
            int dot = description.IndexOf(". ", StringComparison.Ordinal);
            string s = dot > 0 ? description.Substring(0, dot + 1) : description;
            return s.Length <= max ? s : s.Substring(0, max - 1).TrimEnd() + "…";
        }
    }
}
