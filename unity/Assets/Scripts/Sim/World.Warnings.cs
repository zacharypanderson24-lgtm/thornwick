// What a production building is doing and why it is not doing more: the wood chain (lodge -> sawmill / charcoal clamp) and its warnings. Every warning says
// what is happening, why, and what to do about it, from real simulation state (no invented advice). Data only: the inspector, the notices and a future
// governor all read the same calls.
using System;
using System.Collections.Generic;
using System.Text;
using static CrownAndHearth.Sim.Defs;

namespace CrownAndHearth.Sim
{
    public struct ProductionWarning
    {
        public string Title, What, Why, Fix; public bool Critical;
        public override string ToString() => $"{Title}: {What} {Why} Recommended action: {Fix}";
    }

    /// <summary>One link of the timber chain, for the production-chain view.</summary>
    public struct ChainLink { public string From, Via, To; public bool Built, Working; public string Note; }

    public partial class World
    {
        // ---------- the wood chain ----------
        /// <summary>The chain as it is actually implemented: forest -> lodge -> logs; logs -> sawmill -> planks (Res.Wood, what you build with); logs -> charcoal clamp -> charcoal.
        /// The Town Hall saws too, at half yield. There is no firewood: nothing in the game burns it.</summary>
        public List<ChainLink> TimberChain()
        {
            bool Any(string t) => Blds.Exists(b => b.T == t);
            bool Run(string t) => Blds.Exists(b => b.T == t && b.Served && b.Workers > 0);
            return new List<ChainLink>
            {
                new ChainLink { From = "Forest", Via = "Woodcutter Lodge", To = "Raw logs", Built = Any("wood"), Working = Run("wood"), Note = "Lodges fell mature trees in their area; a sustainable lodge replants." },
                new ChainLink { From = "Raw logs", Via = "Sawmill", To = "Planks", Built = Any("sawmill"), Working = Run("sawmill"), Note = $"A sawmill turns up to {SawRate} logs a day into as many planks; the Town Hall saws {HallSaw} a day at half yield." },
                new ChainLink { From = "Raw logs", Via = "Charcoal Clamp", To = "Charcoal", Built = Any("charcoal"), Working = Run("charcoal"), Note = $"{LogsPerCharcoal} logs make 1 charcoal; smithies burn it for extra metal." },
            };
        }

        public string SawmillStatus(Building b)
        {
            double logs = ResAmt[Res.Logs], cap = SawRate * b.Eff;
            var sb = new StringBuilder();
            sb.Append($"Saws up to {cap:0.#} logs a day into {cap:0.#} planks (staffed {b.Workers}/{b.Jobs}); logs in store {logs:0}");
            if (!b.Served) sb.Append("\nIt cannot work: it needs a road to the Town Hall");
            else if (logs < 0.5) sb.Append("\nNo logs to saw: logs come from woodcutter lodges");
            else if (b.Workers == 0) sb.Append("\nNo workers: the Town Hall saws the logs at half yield instead");
            return sb.ToString();
        }

        public string CharcoalStatus(Building b)
        {
            double room = CropStorage(Res.Charcoal) - ResAmt[Res.Charcoal];
            var sb = new StringBuilder($"Burns up to {b.Eff * 2:0.#} charcoal a day from {b.Eff * 2 * LogsPerCharcoal:0.#} logs (staffed {b.Workers}/{b.Jobs}); charcoal {ResAmt[Res.Charcoal]:0.#}, logs {ResAmt[Res.Logs]:0}");
            if (!b.Served) sb.Append("\nIt cannot work: it needs a road to the Town Hall");
            else if (ResAmt[Res.Logs] < LogsPerCharcoal) sb.Append($"\nNot enough logs: {LogsPerCharcoal} logs make 1 charcoal");
            else if (room < 1) sb.Append("\nThe charcoal store is full: smithies use it up");
            return sb.ToString();
        }

        // ---------- warnings ----------
        public List<ProductionWarning> BuildingWarnings(Building b)
        {
            var w = new List<ProductionWarning>();
            string name = b.Name;
            void Add(string title, string what, string why, string fix, bool critical = false) => w.Add(new ProductionWarning { Title = $"{name} — {title}", What = what, Why = why, Fix = fix, Critical = critical });
            if (b.T != "wood" && b.T != "quarry" && b.T != "mine" && b.T != "sawmill" && b.T != "charcoal") return w;
            if (!b.Served)
            {
                Add("Not connected", "It cannot work.", "It has no road to the Town Hall.", "Lay a road from its door to a road that reaches the Town Hall.", true);
                return w;
            }
            switch (b.T)
            {
                case "wood":
                {
                    var f = ForestAround(b);
                    if (b.Mode == ForestMode.Sustainable && !UsesZone(b, Zone.Wood) && f.Mature <= SustainFloor && f.Mature + f.Saplings > 0)
                        Add("Limited sustainable production", "Output is held back.", $"Nearby mature trees are being harvested faster than they regrow: {f.Mature} are left and the lodge leaves {SustainFloor} standing.", "Build another lodge near a larger forest, or use less timber while the saplings grow.");
                    if (f.Mature == 0 && b.Mode != ForestMode.Clearing)
                        Add("No trees to cut", "The crew is idle.", f.ProtectedTrees > 0 ? "Every tree in reach is protected." : f.Saplings > 0 ? "The trees in reach are still saplings." : $"There is no forest within {WorkR} tiles on your land.", f.ProtectedTrees > 0 ? "Lift the protection, or build the lodge by other woodland." : "Build the lodge beside a forest.");
                    if (b.Mode == ForestMode.Intensive && f.Mature > 0 && f.Mature * (double)LogsPerTree / Math.Max(0.01, LodgeDailyEstimate(Math.Max(1, b.Workers))) < 360 * 3)
                        Add("Stripping its forest", "The area will be bare within about three years.", "Intensive mode fells without a floor and does not replant.", "Switch it back to Sustainable when the shortage is over.");
                    if (b.Mode == ForestMode.Clearing && f.Mature == 0)
                        Add("Nothing designated", "The crew is idle.", "Land clearing works only land you mark for clearing.", "Mark woodland for clearing, or switch the lodge back to Sustainable.");
                    break;
                }
                case "quarry":
                {
                    var r = QuarryReach(b);
                    if (r.Dig == 0 && r.Locked > 0)
                        Add("Restricted extraction", "Nothing is open to dig yet.", "The only rock in reach is inside a mountain, and a quarry cuts in from an open edge.", "Build a quarry where the mountain meets open ground.");
                    else if (r.Dig == 0)
                        Add("Restricted extraction", "Nothing to quarry.", r.Beyond > 0 ? $"The nearest stone ({r.Beyond} tiles) lies beyond its {QuarryAutoR}-tile area." : $"No rock is left within {QuarryAutoR} tiles.", r.Beyond > 0 ? "Paint a Mining boundary to widen its area, or establish another quarry." : "Establish another quarry beside fresh rock, or demolish this one.");
                    if (ResAmt[Res.Stone] >= Storage) Add("Store full", "The crew is waiting.", "The stone store is full.", "Use some stone, or build a storehouse.");
                    break;
                }
                case "mine":
                {
                    if (b.Closed > 0) Add("Closed for repairs", $"Work resumes in {b.Closed} days.", "A cave-in closed the tunnel.", "Keep the tunnel shored with timber to make cave-ins rarer.", true);
                    if (b.ReserveStage >= 2) Add("Out of ore", "The mine has stopped.", "All the ore within reach of its tunnel is gone.", "Develop another deposit, or demolish this mine.", true);
                    else if (b.ReserveStage == 1) Add("Declining reserves", "Output will fall.", "Most accessible ore has been exhausted.", "Develop another deposit before this one runs out.");
                    if (RequireMineSupports && b.Shored.Count < b.Tunnel.Count) Add("Waiting for timber", "Miners will not work the unshored tunnel.", "Supports are required and the store is short of wood.", "Produce more planks, or turn the support requirement off.");
                    break;
                }
                case "sawmill":
                    if (ResAmt[Res.Logs] < 0.5) Add("Production stopped", "It is idle.", "No logs are available.", "Expand forestry production (another woodcutter lodge, or a sustainable lodge by a bigger forest).", true);
                    else if (b.Workers == 0) Add("No workers", "It saws nothing.", "No villager is free to work it.", "Grow the town, or free workers from another trade (Workers panel).");
                    else if (CropStorage(Res.Wood) - ResAmt[Res.Wood] < 1) Add("Store full", "It is waiting.", "The plank store is full.", "Build, or add a storehouse.");
                    break;
                case "charcoal":
                    if (ResAmt[Res.Logs] < LogsPerCharcoal) Add("Production stopped", "It is idle.", $"It needs {LogsPerCharcoal} logs for each charcoal and the store has none to spare.", "Expand forestry production.", true);
                    else if (CropStorage(Res.Charcoal) - ResAmt[Res.Charcoal] < 1) Add("Store full", "It is waiting.", "The charcoal store is full.", "Smithies burn it; build a smithery or add a storehouse.");
                    break;
            }
            return w;
        }
    }
}
