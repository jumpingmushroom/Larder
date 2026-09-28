using System;

namespace Larder.Core.Model
{
    public static class Labels
    {
        /// <summary>"45s", "25m", "1h 05m". Rounds seconds up so a running timer never reads 0s.</summary>
        public static string Duration(float seconds)
        {
            int s = (int)Math.Ceiling(Math.Max(0f, seconds));
            if (s < 60)
                return s + "s";
            int m = s / 60;
            if (m < 60)
                return m + "m";
            return (m / 60) + "h " + (m % 60).ToString("00") + "m";
        }
    }
}
