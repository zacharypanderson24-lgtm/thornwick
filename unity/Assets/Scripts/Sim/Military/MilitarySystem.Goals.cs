// Goal-style commands for the Military panel: the player says how many full-time soldiers he wants or how many militia should be on the roll,
// and these set the companies' targets/rolls to match (creating, filling, trimming or discharging as needed). No "selected company" step.
using System;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public sealed partial class MilitarySystem
    {
        /// <summary>How many full-time soldiers the companies are set to have (men now plus recruits still to come).</summary>
        public int SoldierGoal => Companies.Where(c => c.Category == MilitaryCategory.Professional).Sum(c => Math.Max(c.Target, c.Personnel));

        /// <summary>Aim for this many full-time soldiers. More: new recruits (of newType) are added to companies with room, or to new companies, and join over the next days.
        /// Fewer: targets are cut from the newest companies and anyone above the new number is discharged back to work at once.</summary>
        public Result SetSoldierGoal(int total, MilitaryUnitType newType)
        {
            total = Math.Max(0, total);
            int goal = SoldierGoal;
            if (total == goal) return Result.Yes();
            if (total > goal)
            {
                var def = TroopDefinitions.Get(newType);
                if (def.Levy) return Result.No("Militia are not full-time soldiers");
                if (def.Era > _w.Era) return Result.No($"{def.Name} needs a later era");
                if (Barracks == 0) return Result.No("Build a Barracks first: soldiers need somewhere to live");
                int add = total - goal;
                foreach (var c in Companies.Where(c => c.Category == MilitaryCategory.Professional && c.Unit == newType && c.Target < c.Max).OrderBy(c => c.Id).ToList())
                {
                    int take = Math.Min(add, c.Max - c.Target);
                    c.Target += take; add -= take;
                    if (add <= 0) break;
                }
                while (add > 0)
                {
                    var c = NewCompany(MilitaryCategory.Professional, newType);
                    int take = Math.Min(add, c.Max);
                    c.Target = take; add -= take;
                }
                _w.Touch();
                return Result.Yes(total - goal);
            }
            int cut = goal - total, cutDone = cut;
            foreach (var c in Companies.Where(c => c.Category == MilitaryCategory.Professional).OrderByDescending(c => c.Id).ToList())
            {
                if (cut <= 0) break;
                int t = Math.Min(cut, Math.Max(c.Target, c.Personnel));
                c.Target = Math.Max(0, Math.Max(c.Target, c.Personnel) - t); cut -= t;
                int extra = c.Personnel - c.Target;
                if (extra > 0) Discharge(c.Id, extra);
            }
            Companies.RemoveAll(c => c.Category == MilitaryCategory.Professional && c.Personnel <= 0 && c.Target <= 0);
            _w.Recalc();
            return Result.Yes(cutDone);
        }

        /// <summary>Put exactly this many villagers on the militia roll (as many as the muster grounds and the free civilians allow).</summary>
        public Result SetRoll(int total)
        {
            total = Math.Max(0, total);
            int reg = Registered;
            if (total > reg) return RegisterMilitia(total - reg);
            int cut = reg - total;
            foreach (var c in Companies.Where(c => c.IsMilitia).OrderBy(c => c.Mobilised).ThenByDescending(c => c.Id).ToList())
            {
                if (cut <= 0) break;
                int n = Math.Min(cut, c.Personnel);
                if (Unregister(c.Id, n).Ok) cut -= n;
            }
            return Result.Yes(reg - total);
        }

        /// <summary>Call up this many militia from the roll (companies are split as needed). Returns how many were called up.</summary>
        public Result MobiliseCount(int count)
        {
            if (count <= 0) return Result.No("Choose how many to call up");
            int left = count;
            foreach (var c in Companies.Where(c => c.IsMilitia && !c.Mobilised && c.Personnel > 0).OrderBy(c => c.Id).ToList())
            {
                int take = Math.Min(left, c.Personnel);
                if (Mobilise(c.Id, take).Ok) left -= take;
                if (left <= 0) break;
            }
            return left == count ? Result.No("Nobody on the roll to call up") : Result.Yes(count - left);
        }

        /// <summary>Send this many called-up militia home to work.</summary>
        public Result DemobiliseCount(int count)
        {
            if (count <= 0) return Result.No("Choose how many to send home");
            int left = count;
            foreach (var c in Companies.Where(c => c.IsMilitia && c.Mobilised && c.Personnel > 0).OrderByDescending(c => c.Id).ToList())
            {
                int take = Math.Min(left, c.Personnel);
                if (Demobilise(c.Id, take).Ok) left -= take;
                if (left <= 0) break;
            }
            return left == count ? Result.No("Nobody is called up") : Result.Yes(count - left);
        }
    }
}
