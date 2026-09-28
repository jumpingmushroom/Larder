using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>An axis-aligned rectangle in screen pixels, y up (Top is greater than Bottom).</summary>
    public struct ScreenRect
    {
        public readonly float Left, Right, Top, Bottom;

        public ScreenRect(float left, float right, float top, float bottom)
        {
            Left = left;
            Right = right;
            Top = top;
            Bottom = bottom;
        }
    }

    public static class PlacementMath
    {
        /// <summary>
        /// The right edge of the cluster of UI that sits beside a starting block (the inventory).
        /// A candidate joins the cluster when its vertical span overlaps the band [bottom, top] and
        /// its left edge lies between the cluster's left edge and the current right edge plus
        /// <paramref name="gap"/>; joining can extend the right edge, which can bring more candidates
        /// in reach, so this repeats until nothing changes. A panel beyond an empty gap wider than
        /// <paramref name="gap"/> is never reached. Screen space, y up.
        /// </summary>
        public static float ClusterRight(float startLeft, float startRight, float top, float bottom,
            IList<ScreenRect> candidates, float gap)
        {
            float right = startRight;
            if (candidates == null)
                return right;
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < candidates.Count; i++)
                {
                    ScreenRect c = candidates[i];
                    bool inBand = Math.Min(top, c.Top) > Math.Max(bottom, c.Bottom);
                    if (!inBand || c.Left < startLeft || c.Left > right + gap || c.Right <= right)
                        continue;
                    right = c.Right;
                    grew = true;
                }
            }
            return right;
        }
    }
}
