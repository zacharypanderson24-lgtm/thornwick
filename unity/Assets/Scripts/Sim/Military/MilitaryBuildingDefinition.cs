// Military buildings as data (the spec's table), not hard-coded: add a row to add a building. Only rows with Implemented = true can be placed
// in the playable game; the others are the planned roster for the next phases and eras and carry their numbers so balance stays in one place.
using System.Collections.Generic;
using System.Linq;

namespace CrownAndHearth.Sim
{
    public enum MilitaryBuildingRole { Muster, Barracks, Armory, Watchtower, Guardhouse, Training, Headquarters, Keep }

    public sealed class MilitaryBuildingDefinition
    {
        public string Type = "", Name = "", Texture = "", Note = "";
        public MilitaryBuildingRole Role;
        public int Size = 1, Era = 1;
        /// <summary>Registered militia (muster), professionals housed (barracks), equipment sets (armory), duty posts (watchtower, guardhouse) or trainees (training grounds).</summary>
        public int Capacity;
        public bool Implemented;
        public Dictionary<Res, int> Cost = new Dictionary<Res, int>();
    }

    public static class MilitaryBuildings
    {
        public static readonly MilitaryBuildingDefinition[] All =
        {
            new MilitaryBuildingDefinition { Type = "muster", Name = "Muster Ground", Role = MilitaryBuildingRole.Muster, Size = 2, Era = 1, Capacity = MilitaryConfig.MusterCapacity, Implemented = true,
                Texture = "ph_muster", Cost = { { Res.Wood, 14 } },
                Note = "Registers militia and gathers them in an emergency. Houses nobody; registered militia keep their jobs and homes." },
            new MilitaryBuildingDefinition { Type = "barracks", Name = "Barracks", Role = MilitaryBuildingRole.Barracks, Size = 2, Era = 2, Capacity = MilitaryConfig.BarracksCapacity, Implemented = true,
                Texture = "barracks", Cost = { { Res.Wood, 20 }, { Res.Stone, 15 }, { Res.Gold, 20 } },
                Note = "Houses 16 professional soldiers. It does not make soldiers by itself: you recruit them into companies from the workforce." },
            new MilitaryBuildingDefinition { Type = "armory", Name = "Armory", Role = MilitaryBuildingRole.Armory, Size = 1, Era = 2, Capacity = 60, Texture = "ph_armory",
                Note = "Phase 2: stores 60 equipment sets (available, reserved, issued)." },
            new MilitaryBuildingDefinition { Type = "watchtower", Name = "Wooden Watchtower", Role = MilitaryBuildingRole.Watchtower, Size = 1, Era = 1, Capacity = 2, Implemented = true,
                Texture = "watchtower", Cost = { { Res.Wood, 12 }, { Res.Stone, 4 } },
                Note = "Spots raiders. Staffing (2 guard posts) and detection by garrisons arrive in Phase 3." },
            new MilitaryBuildingDefinition { Type = "guardhouse", Name = "Guardhouse", Role = MilitaryBuildingRole.Guardhouse, Size = 1, Era = 2, Capacity = 4, Texture = "ph_guardhouse",
                Note = "Phase 3: 4 duty posts for existing soldiers. Recruits nobody, houses nobody." },
            new MilitaryBuildingDefinition { Type = "traininggrounds", Name = "Training Grounds", Role = MilitaryBuildingRole.Training, Size = 3, Era = 3, Capacity = 40, Texture = "ph_traininggrounds",
                Note = "Phase 2/Era III: up to 40 soldiers train faster." },
            new MilitaryBuildingDefinition { Type = "militaryhq", Name = "Military Headquarters", Role = MilitaryBuildingRole.Headquarters, Size = 3, Era = 3, Capacity = 0, Texture = "ph_militaryhq",
                Note = "Era III: command capacity, coordination, reinforcement and several companies at once." },
            new MilitaryBuildingDefinition { Type = "keep", Name = "Fortified Keep", Role = MilitaryBuildingRole.Keep, Size = 3, Era = 3, Capacity = 0, Texture = "ph_keep",
                Note = "Era III/IV: a fortified command post." },
        };

        public static MilitaryBuildingDefinition Get(string type) => All.FirstOrDefault(d => d.Type == type);
        public static bool IsMilitary(string type) => Get(type) != null;
    }
}
