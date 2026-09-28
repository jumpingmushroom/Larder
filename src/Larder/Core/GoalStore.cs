using System;
using Larder.Core.Model;

namespace Larder.Core
{
    /// <summary>The goal, per character, in Player.m_customData (saved with the character, never synced).</summary>
    internal static class GoalStore
    {
        private const string Key = "larder.goal";

        public static Goal Get(Player p)
        {
            string s;
            Goal g;
            if (p != null && p.m_customData.TryGetValue(Key, out s) && Enum.TryParse(s, out g))
                return g;
            return Goal.Balanced;
        }

        public static void Set(Player p, Goal g)
        {
            if (p != null)
                p.m_customData[Key] = g.ToString();
        }
    }
}
