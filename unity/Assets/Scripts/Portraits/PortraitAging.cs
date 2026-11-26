// Turns a character's permanent appearance (their young self) into what they look like at a given age: the age-stage overlay, graying, a receding
// hairline and baldness. Face, skin, eyes, brows and facial-hair style never change, so a person stays recognisable. Rules and stage boundaries are
// library data; the onset ages belong to the person (0 = never), so not everyone greys early, recedes or goes bald.
using UnityEngine;

namespace CrownAndHearth.Portraits
{
    public static class PortraitAging
    {
        // The age stage covering `years` (the last stage whose minYears is reached; the first stage below that).
        public static AgeStageDef StageFor(PortraitLibrary lib, int years)
        {
            AgeStageDef best = null;
            foreach (var s in lib.ageStages) if (years >= s.minYears && (best == null || s.minYears >= best.minYears)) best = s;
            if (best != null) return best;
            AgeStageDef first = null;
            foreach (var s in lib.ageStages) if (first == null || s.minYears < first.minYears) first = s;
            return first;
        }

        public static PortraitAppearance Apply(PortraitLibrary lib, PortraitAppearance original, int years, int greyAge, int recedeAge, int baldAge)
        {
            var a = original.Clone();
            var stage = StageFor(lib, years); if (stage != null) a.age = stage.id;
            var rules = lib.aging;

            if (greyAge > 0 && years >= greyAge)
            {
                int past = years - greyAge; string target = null; int targetIndex = -1;
                for (int i = 0; i < rules.greyingSteps.Count; i++) if (past >= rules.greyingSteps[i].yearsAfterOnset) { target = rules.greyingSteps[i].hairColorId; targetIndex = i; }
                if (target != null)
                {
                    int ownIndex = rules.greyingSteps.FindIndex(s => s.hairColorId == original.hair_color);   // already as grey or greyer: keep
                    if (ownIndex < targetIndex && lib.FindEntry(lib.hairSetId, target) != null) a.hair_color = target;
                }
            }
            if (recedeAge > 0 && years >= recedeAge && !rules.recedeKeepStyles.Contains(original.hair) && lib.Find(PortraitLayer.Hair, rules.recedingStyleId) != null) a.hair = rules.recedingStyleId;
            if (baldAge > 0 && years >= baldAge && original.hair != rules.baldStyleId && lib.Find(PortraitLayer.Hair, rules.baldStyleId) != null) a.hair = rules.baldStyleId;
            return a;
        }
    }
}
