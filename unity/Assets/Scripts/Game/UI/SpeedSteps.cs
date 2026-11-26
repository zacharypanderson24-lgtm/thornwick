// The game speeds the + and - keys step through. Pure C#, tested directly.
namespace CrownAndHearth.Game.UI
{
    public static class SpeedSteps
    {
        public static readonly int[] Speeds = { 1, 2, 4 };

        /// <summary>The next speed up (direction 1) or down (-1), staying at the ends. From a pause, either key resumes at 1x going up, or stays 1x going down.</summary>
        public static int Next(int current, int direction)
        {
            if (current <= 0) return 1;
            int i = System.Array.IndexOf(Speeds, current);
            if (i < 0) i = 0;
            i += direction > 0 ? 1 : -1;
            if (i < 0) i = 0; else if (i >= Speeds.Length) i = Speeds.Length - 1;
            return Speeds[i];
        }
    }
}
