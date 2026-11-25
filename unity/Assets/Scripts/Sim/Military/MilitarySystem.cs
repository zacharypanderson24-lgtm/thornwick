// The military of one settlement, population-driven (replaces the old barracks-makes-soldiers model).
//
//   * Everybody is a resident of the existing population. Peasants = civilians (every peasant is a worker); Soldiers = professionals (they left
//     the workforce for good and are counted in the population once, in World.Soldiers / World.Archers, which the raid, camp and conquest code
//     still read). MilitarySystem keeps its companies in step with those two numbers (Reconcile), so nobody is counted twice and nobody appears
//     from nowhere: soldiers only come from recruitment.
//   * Registered militia are peasants too. They stay in World.Peasants, keep their jobs and homes and eat as civilians; only the ones who are
//     MOBILISED leave the workforce (World.Recalc fills jobs from Peasants - MobilisedMilitia). Demobilising simply gives them back, and the next
//     Recalc hands the jobs out again by the usual trade priority, so no job assignment is ever lost.
//   * Companies (MilitaryCompany) are the unit of management; nothing here knows about the UI or the renderer.
//
// Files: MilitaryConfig (all numbers), MilitaryCompany (state, enums), MilitaryBuildingDefinition (buildings as data), this file (companies,
// recruitment, mobilisation, daily step), MilitarySystem.Save.cs. Equipment, training facilities, orders in motion and supply are later phases;
// the stats and the order enum already exist so they slot in without changing company management.
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    /// <summary>What a recruitment or call-up would do to the workforce, shown to the player before they confirm.</summary>
    public struct WorkforceImpact
    {
        public int Before, After;
        public string Warning;   // empty when nothing worrying
        public override string ToString() => $"civilian workforce {Before} -> {After}";
    }

    public sealed partial class MilitarySystem
    {
        readonly World _w;
        public readonly List<MilitaryCompany> Companies = new List<MilitaryCompany>();
        public int NextId = 1;
        /// <summary>Raid being tracked for combat experience (its kill count when last looked at).</summary>
        object _raidRef; int _lastKilled;
        double _wounds;

        public MilitarySystem(World w) { _w = w; }

        // ---------- counts (aggregate, derived from the companies) ----------

        public int Registered => Companies.Where(c => c.IsMilitia).Sum(c => c.Personnel);
        public int MobilisedCount => Companies.Where(c => c.IsMilitia && c.Mobilised).Sum(c => c.Personnel);
        public int Professionals => Companies.Where(c => c.Category == MilitaryCategory.Professional).Sum(c => c.Personnel);
        public MilitaryCompany Find(int id) => Companies.FirstOrDefault(c => c.Id == id);

        /// <summary>Civilians of an age to serve (this sim: every peasant).</summary>
        public int EligiblePopulation => (int)Math.Floor(_w.Peasants * MilitaryConfig.EligibleShare);
        /// <summary>Civilians at work: everyone but the mobilised militia.</summary>
        public int CivilianWorkforce => Math.Max(0, _w.Peasants - MobilisedCount);
        /// <summary>Civilians still free to register as militia or be recruited as professionals (nobody is in two companies).</summary>
        public int AvailableToServe => Math.Max(0, EligiblePopulation - Registered - MilitaryConfig.MinCivilians);

        public int MusterCapacity => _w.Blds.Count(b => b.T == "muster") * MilitaryConfig.MusterCapacity;
        public int Barracks => _w.Blds.Count(b => b.T == "barracks");
        /// <summary>Bunks in barracks that are connected to the town (a barracks off the road houses nobody).</summary>
        public int BunkCapacity => _w.D?.Bunks ?? 0;
        public int BunksFree => Math.Max(0, BunkCapacity - _w.Soldiers - _w.TroopsReturning);   // a bunk is kept for every soldier on his way home
        /// <summary>Professionals living outside barracks because the barracks that housed them are gone or full. They are not lost.</summary>
        public int Unhoused => Math.Max(0, _w.Soldiers - BunkCapacity);

        // ---------- keeping the companies and the world's numbers in step ----------

        /// <summary>Make the companies match World.Soldiers/Archers/Peasants. Called by Recalc, so any code that changes those numbers directly
        /// (casualties, an army coming home, the town builder, old saves) is picked up: missing soldiers are taken from the biggest companies of
        /// that unit, new ones join a company with room (or a new one). Cheap when nothing changed.</summary>
        public void Reconcile()
        {
            if (_w.Soldiers < 0) _w.Soldiers = 0;
            if (_w.Archers > _w.Soldiers) _w.Archers = _w.Soldiers;
            if (_w.Archers < 0) _w.Archers = 0;
            SyncUnit(MilitaryUnitType.HunterArcher, _w.Archers);
            SyncUnit(MilitaryUnitType.Spearman, _w.Soldiers - _w.Archers);

            int surplus = Registered - _w.Peasants;   // militia cannot outnumber the civilians they come from (famine, raids)
            if (surplus > 0)
            {
                foreach (var c in Companies.Where(c => c.IsMilitia).OrderByDescending(c => c.Mobilised).ThenByDescending(c => c.Personnel).ThenBy(c => c.Id).ToList())
                {
                    int cut = Math.Min(surplus, c.Personnel);
                    c.Personnel -= cut; surplus -= cut;
                    if (surplus <= 0) break;
                }
            }
            Companies.RemoveAll(c => c.Personnel <= 0 && c.Target <= 0);
        }

        /// <summary>Archers and infantry are the two aggregate totals of the world; every infantry type (spearman, shield) counts in the infantry total.</summary>
        static bool SameArm(MilitaryUnitType a, MilitaryUnitType b) => TroopDefinitions.Get(a).Ranged == TroopDefinitions.Get(b).Ranged;

        void SyncUnit(MilitaryUnitType unit, int want)
        {
            int have = Companies.Where(c => c.Category == MilitaryCategory.Professional && SameArm(c.Unit, unit)).Sum(c => c.Personnel);
            if (have == want) return;
            if (have > want)
            {
                int cut = have - want;
                foreach (var c in Companies.Where(c => c.Category == MilitaryCategory.Professional && SameArm(c.Unit, unit))
                             .OrderByDescending(c => c.Personnel).ThenBy(c => c.Id).ToList())
                {
                    int n = Math.Min(cut, c.Personnel);
                    c.Personnel -= n; cut -= n;
                    if (cut <= 0) break;
                }
                return;
            }
            int add = want - have;   // soldiers the world has that no company holds: an army home again, an old save, a scenario
            while (add > 0)
            {
                var c = Companies.Where(x => x.Category == MilitaryCategory.Professional && x.Unit == unit && x.Personnel < x.Max).OrderBy(x => x.Id).FirstOrDefault()
                        ?? NewCompany(MilitaryCategory.Professional, unit);
                int n = Math.Min(add, c.Max - c.Personnel);
                Join(c, n, MilitaryConfig.MigratedTraining, MilitaryConfig.MigratedExperience);
                if (c.Target < c.Personnel) c.Target = c.Personnel;
                add -= n;
            }
        }

        /// <summary>Add men to a company; its training and experience become the average of the old and the new men (newcomers dilute it).</summary>
        static void Join(MilitaryCompany c, int n, double training, double experience)
        {
            if (n <= 0) return;
            int total = c.Personnel + n;
            c.Training = (c.Training * c.Personnel + training * n) / total;
            c.Experience = (c.Experience * c.Personnel + experience * n) / total;
            c.Personnel = total;
        }

        MilitaryCompany NewCompany(MilitaryCategory cat, MilitaryUnitType unit)
        {
            int id = NextId++;
            var c = new MilitaryCompany
            {
                Id = id, Category = cat, Unit = unit, Name = $"{MilitaryCompanies.UnitName(unit)} Company {id}",
                Training = 0, Experience = 0, Morale = MilitaryConfig.RecruitMorale,
            };
            var station = cat == MilitaryCategory.Militia ? _w.Blds.FirstOrDefault(b => b.T == "muster") : _w.Blds.FirstOrDefault(b => b.T == "barracks");
            if (station != null) { c.StationX = station.X; c.StationY = station.Y; }
            Companies.Add(c);
            return c;
        }

        // ---------- the workforce impact, shown before the player commits ----------

        public WorkforceImpact Impact(int professionalsDelta, int mobilisedDelta)
        {
            int before = CivilianWorkforce;
            int after = Math.Max(0, before - professionalsDelta - mobilisedDelta);
            var res = new WorkforceImpact { Before = before, After = after, Warning = "" };
            if (after >= before || _w.D == null) return res;
            // hand the workers out the way Recalc does (trade priority) and see which trades would be left short
            int avail = after; var short1 = new List<string>();
            foreach (var t in _w.Prio)
            {
                if (!_w.D.Work.TryGetValue(t, out var wk)) continue;
                int got = Math.Min(wk.jobs, avail); avail -= got;
                if (got < wk.jobs && (t == "farming" || t == "hunting")) short1.Add($"{Defs.TradeNames[t].ToLower()} would be {wk.jobs - got} short");
            }
            if (short1.Count > 0) res.Warning = "Food is at risk: " + string.Join(", ", short1) + ".";
            else if (_w.D.Jobs > after) res.Warning = $"{_w.D.Jobs - after} jobs would go unfilled.";
            return res;
        }

        // ---------- commands (each returns why not, or Yes) ----------

        public Result CreateCompany(MilitaryUnitType unit)
        {
            if (unit == MilitaryUnitType.PeasantLevy)
            {
                if (MusterCapacity <= 0) return Result.No("Build a Muster Ground first");
                NewCompany(MilitaryCategory.Militia, unit);
            }
            else
            {
                if (TroopDefinitions.Get(unit).Era > _w.Era) return Result.No($"{TroopDefinitions.Get(unit).Name} needs a later era");
                if (Barracks == 0) return Result.No("Build a Barracks first: professionals need somewhere to live");
                NewCompany(MilitaryCategory.Professional, unit);
            }
            _w.Touch();
            return Result.Yes();
        }

        public Result Rename(int id, string name)
        {
            var c = Find(id); if (c == null) return Result.No("No such company");
            name = (name ?? "").Trim();
            if (name.Length == 0) return Result.No("A company needs a name");
            c.Name = name.Length > 32 ? name.Substring(0, 32) : name;
            _w.Touch();
            return Result.Yes();
        }

        /// <summary>Set how many men a company should have. Militia register (or leave the roll) at once; professionals arrive over the days that follow.</summary>
        public Result SetTarget(int id, int target)
        {
            var c = Find(id); if (c == null) return Result.No("No such company");
            target = Math.Clamp(target, 0, c.Max);
            if (c.IsMilitia)
            {
                if (target > c.Personnel) return RegisterMilitia(target - c.Personnel, id);
                if (target < c.Personnel) return Unregister(id, c.Personnel - target);
                return Result.Yes();
            }
            if (c.Category != MilitaryCategory.Professional) return Result.No("Field companies are set by their orders");
            c.Target = target;
            _w.Touch();
            return Result.Yes();
        }

        /// <summary>Put civilians on the militia roll. They keep their jobs: the workforce does not change until a company is mobilised.</summary>
        public Result RegisterMilitia(int count, int companyId = 0)
        {
            if (count <= 0) return Result.No("Nothing to register");
            if (MusterCapacity <= 0) return Result.No("Build a Muster Ground to register militia");
            int room = MusterCapacity - Registered;
            if (room <= 0) return Result.No("The muster grounds are full: build another or let some men go");
            int can = Math.Min(count, Math.Min(room, AvailableToServe));
            if (can <= 0) return Result.No("No civilians left to register: nobody can be in two companies, and the town keeps a few");
            var target = companyId != 0 ? Find(companyId) : null;
            if (target != null && !target.IsMilitia) return Result.No("That is not a militia company");
            int done = 0;
            while (can > 0)
            {
                var c = target ?? Companies.Where(x => x.IsMilitia && x.Personnel < x.Max).OrderBy(x => x.Id).FirstOrDefault() ?? NewCompany(MilitaryCategory.Militia, MilitaryUnitType.PeasantLevy);
                int n = Math.Min(can, c.Max - c.Personnel);
                if (n <= 0) { if (target != null) break; continue; }
                Join(c, n, MilitaryConfig.MilitiaStartTraining, 0);
                if (c.Target < c.Personnel) c.Target = c.Personnel;
                can -= n; done += n;
                if (target != null && c.Personnel >= c.Max) break;
            }
            _w.Recalc();
            return done > 0 ? Result.Yes(done) : Result.No("That company is full");
        }

        public Result Unregister(int id, int count)
        {
            var c = Find(id); if (c == null || !c.IsMilitia) return Result.No("That is not a militia company");
            int n = Math.Min(count, c.Personnel);
            if (n <= 0) return Result.No("Nobody to remove");
            c.Personnel -= n; c.Target = Math.Min(c.Target, c.Personnel);
            if (c.Personnel <= 0) Companies.Remove(c);
            _w.Recalc();
            return Result.Yes(n);
        }

        public Result Mobilise(int id)
        {
            var c = Find(id); if (c == null || !c.IsMilitia) return Result.No("Only militia companies are mobilised");
            if (c.Mobilised) return Result.No("Already mobilised");
            if (c.Personnel <= 0) return Result.No("The company is empty");
            c.Mobilised = true; c.MobilisedDays = 0; c.Order = MilitaryOrderKind.Assemble; c.Location = "Assembling at the muster point";
            _w.Recalc();
            return Result.Yes(c.Personnel);
        }

        public Result Demobilise(int id)
        {
            var c = Find(id); if (c == null || !c.IsMilitia) return Result.No("Only militia companies are demobilised");
            if (!c.Mobilised) return Result.No("Not mobilised");
            c.Mobilised = false; c.MobilisedDays = 0; c.Order = MilitaryOrderKind.StandDown; c.Location = "At home, back at work";
            _w.Recalc();
            return Result.Yes(c.Personnel);
        }

        /// <summary>Call up a set number of a militia company: a detachment of that many men is split off and mobilised (the rest stay on the roll).</summary>
        public Result Mobilise(int id, int count)
        {
            var c = Find(id); if (c == null || !c.IsMilitia) return Result.No("Only militia companies are mobilised");
            if (c.Mobilised) return Result.No("Already mobilised");
            if (count <= 0) return Result.No("Type a number of men first");
            if (count >= c.Personnel) return Mobilise(id);
            var part = Split(c, count);
            return Mobilise(part.Id);
        }

        /// <summary>Send a set number of a mobilised company back to work (a detachment is split off and demobilised).</summary>
        public Result Demobilise(int id, int count)
        {
            var c = Find(id); if (c == null || !c.IsMilitia) return Result.No("Only militia companies are demobilised");
            if (!c.Mobilised) return Result.No("Not mobilised");
            if (count <= 0) return Result.No("Type a number of men first");
            if (count >= c.Personnel) return Demobilise(id);
            var part = Split(c, count);
            return Demobilise(part.Id);
        }

        /// <summary>Split n men off a company into a new one beside it (same category, stats and state).</summary>
        MilitaryCompany Split(MilitaryCompany c, int n)
        {
            var p = new MilitaryCompany
            {
                Id = NextId++, Name = c.Name + " detachment", Category = c.Category, Unit = c.Unit, HomeSettlement = c.HomeSettlement,
                Personnel = n, Target = n, Training = c.Training, Experience = c.Experience, Morale = c.Morale, Readiness = c.Readiness,
                Mobilised = c.Mobilised, MobilisedDays = c.MobilisedDays, Order = c.Order, OrderX = c.OrderX, OrderY = c.OrderY,
                StationX = c.StationX, StationY = c.StationY, Location = c.Location, Fatigue = c.Fatigue, Formation = c.Formation, Supply = c.Supply,
            };
            c.Personnel -= n; c.Target = Math.Min(c.Target, c.Personnel);
            Companies.Insert(Companies.IndexOf(c) + 1, p);
            return p;
        }

        public int MobiliseAll() { int n = 0; foreach (var c in Companies.Where(c => c.IsMilitia && !c.Mobilised && c.Personnel > 0).ToList()) if (Mobilise(c.Id).Ok) n += c.Personnel; return n; }
        public int DemobiliseAll() { int n = 0; foreach (var c in Companies.Where(c => c.IsMilitia && c.Mobilised).ToList()) if (Demobilise(c.Id).Ok) n += c.Personnel; return n; }

        /// <summary>Send professionals back to civil life: they rejoin the peasants (and so the workforce) at once.</summary>
        public Result Discharge(int id, int count)
        {
            var c = Find(id); if (c == null || c.Category != MilitaryCategory.Professional) return Result.No("Only professional soldiers are discharged");
            int n = Math.Min(count, c.Personnel);
            if (n <= 0) return Result.No("Nobody to discharge");
            c.Personnel -= n; c.Target = Math.Min(c.Target, c.Personnel);
            _w.Soldiers -= n; _w.Peasants += n;
            if (c.Unit == MilitaryUnitType.HunterArcher) _w.Archers = Math.Max(0, _w.Archers - n);
            if (c.Personnel <= 0) Companies.Remove(c);
            _w.Recalc();
            return Result.Yes(n);
        }

        /// <summary>Break a company up: professionals are discharged, militia leave the roll.</summary>
        public Result Disband(int id)
        {
            var c = Find(id); if (c == null) return Result.No("No such company");
            if (c.Personnel > 0) { var r = c.IsMilitia ? Unregister(id, c.Personnel) : Discharge(id, c.Personnel); if (!r.Ok) return r; }
            Companies.Remove(c);
            _w.Recalc();
            return Result.Yes();
        }

        /// <summary>Give a company an order. Phase 1 stores it (and an Assemble/Train order shapes training); Phase 3 makes companies walk to it.</summary>
        public Result Order(int id, MilitaryOrderKind kind, int x = -1, int y = -1)
        {
            var c = Find(id); if (c == null) return Result.No("No such company");
            c.Order = kind; c.OrderX = x; c.OrderY = y;
            _w.Touch();
            return Result.Yes();
        }

        /// <summary>How many professionals could be recruited right now: workers who are not on a roll, bunks free, and no more than the companies ask for.</summary>
        public int RecruitableNow() => Math.Max(0, Math.Min(AvailableToServe, BunksFree));

        void Recruit(MilitaryCompany c, int n)
        {
            if (n <= 0) return;
            _w.Peasants -= n; _w.Soldiers += n; _w.Trained += n;
            if (c.Unit == MilitaryUnitType.HunterArcher) _w.Archers += n;
            Join(c, n, MilitaryConfig.RecruitTraining, 0);
            c.Morale = (c.Morale * (c.Personnel - n) + MilitaryConfig.RecruitMorale * n) / c.Personnel;
        }

        // ---------- the daily step (called from World.Tick) ----------

        /// <param name="away">Professionals away on campaign or marching home: they still count against the companies' targets so nobody recruits a second army.</param>
        public void StepDaily(int away)
        {
            Reconcile();
            if (_w.Drafting)
            {
                int wanted = Companies.Where(c => c.Category == MilitaryCategory.Professional).Sum(c => c.Target) - _w.Soldiers - away;
                foreach (var c in Companies.Where(c => c.Category == MilitaryCategory.Professional).OrderBy(c => c.Id).ToList())
                {
                    int n = Math.Min(Math.Min(MilitaryConfig.RecruitPerDay, c.Target - c.Personnel), Math.Min(wanted, RecruitableNow()));
                    if (n <= 0) continue;
                    Recruit(c, n); wanted -= n;
                }
            }
            UpdateStats();
        }

        bool HasMuster => _w.Blds.Any(b => b.T == "muster");

        void UpdateStats()
        {
            int killed = 0;
            var raid = _w.Raid;
            if (raid != null && !raid.Over)
            {
                if (!ReferenceEquals(raid, _raidRef)) { _raidRef = raid; _lastKilled = 0; }
                killed = Math.Max(0, raid.Killed - _lastKilled); _lastKilled = raid.Killed;
            }
            bool fed = !_w.Starving, paid = _w.ResAmt[Res.Gold] > 0;
            bool muster = HasMuster;
            foreach (var c in Companies)
            {
                bool serving = c.Category == MilitaryCategory.Professional || c.Mobilised;
                if (c.Mobilised) c.MobilisedDays++;
                bool drills = c.Order == MilitaryOrderKind.StandDown || c.Order == MilitaryOrderKind.Train || c.Order == MilitaryOrderKind.Assemble;
                // training: slow for militia (only at a muster ground), faster for professionals, both towards a cap
                if (c.Personnel > 0 && drills)
                {
                    if (c.IsMilitia && muster && c.Training < MilitaryConfig.MilitiaTrainCap)
                        c.Training += MilitaryConfig.MilitiaTrainRate * (1 - c.Training / 100.0);
                    else if (c.Category == MilitaryCategory.Professional && c.Training < MilitaryConfig.ProTrainCap)
                        c.Training += MilitaryConfig.ProTrainRate * (1 - c.Training / 100.0);
                }
                // experience: service and combat, never lost
                if (c.Personnel > 0 && serving)
                    c.Experience = Math.Min(MilitaryConfig.ExperienceCap, c.Experience + MilitaryConfig.ServiceExperienceRate + killed * MilitaryConfig.ExperiencePerKill);
                // morale drifts towards what the company's conditions say it should be
                double goal = MilitaryConfig.MoraleBase;
                if (!fed) goal += MilitaryConfig.MoraleStarving;
                if (c.Category == MilitaryCategory.Professional && !paid) goal += MilitaryConfig.MoraleUnpaid;
                if (c.Category == MilitaryCategory.Professional && _w.Soldiers > BunkCapacity) goal += MilitaryConfig.MoraleOverCapacity;
                if (c.Mobilised && c.MobilisedDays > MilitaryConfig.MobilisationComfortDays) goal += MilitaryConfig.MoraleLongMobilisation;
                double step = MilitaryConfig.MoraleMovePerDay;
                c.Morale = Math.Clamp(c.Morale + Math.Clamp(goal - c.Morale, -step, step), 0, 100);
                // readiness: how soon the company can act (men, food, call-up), not how good it is
                double activation = c.Category == MilitaryCategory.Professional ? 1 : c.Mobilised ? Math.Min(1, c.MobilisedDays / (double)MilitaryConfig.MobiliseDays) : 0;
                c.Readiness = Math.Round(50 * c.Fill + (fed ? 20 : 0) + 30 * activation, 1);
            }
        }

        // ---------- a mobilised militia in a raid (World.Raids.cs asks for these) ----------

        /// <summary>Soldier-equivalents the mobilised militia add (0.15 each untrained, 0.45 fully trained, scaled by morale and readiness).</summary>
        public double MilitiaPower()
        {
            double p = 0;
            foreach (var c in Companies)
            {
                if (!c.IsMilitia || !c.Mobilised || c.Personnel <= 0) continue;
                double skill = MilitaryConfig.MilitiaPowerUntrained + (MilitaryConfig.MilitiaPowerTrained - MilitaryConfig.MilitiaPowerUntrained) * (c.Training / 100.0);
                p += c.Personnel * skill * (0.5 + c.Morale / 200.0) * (c.Readiness / 100.0 >= 0.8 ? 1 : 0.6);
            }
            return p;
        }

        /// <summary>Where the mobilised militia stand: the first muster ground, or the Town Hall when there is none.</summary>
        public Building MusterPoint() => _w.Blds.FirstOrDefault(b => b.T == "muster") ?? _w.Blds.FirstOrDefault(b => b.T == "hall");

        /// <summary>One fighting day of the mobilised militia against the raiders in reach. Returns (damage per raider, civilians wounded).</summary>
        public double FightDamage(int raidersInReach)
        {
            if (raidersInReach <= 0) return 0;
            double power = MilitiaPower();
            if (power <= 0) return 0;
            _wounds += MobilisedCount * MilitaryConfig.MilitiaWoundRate * Math.Min(raidersInReach, 4);
            return power * MilitaryConfig.MilitiaDamagePerPower / raidersInReach;
        }

        /// <summary>Whole civilians lost to wounds since the last call (they reduce the real population).</summary>
        public int TakeWounded()
        {
            int n = (int)Math.Floor(_wounds); _wounds -= n;
            return n;
        }
    }
}
