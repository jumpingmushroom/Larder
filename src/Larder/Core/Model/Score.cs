using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>
    /// A lexicographic score: A decides, then B, C, D. Values within Epsilon compare equal, so
    /// the same foods summed in another order score the same.
    /// </summary>
    public struct Score : IComparable<Score>
    {
        public const float Epsilon = 0.001f;

        public readonly float A;
        public readonly float B;
        public readonly float C;
        public readonly float D;

        public Score(float a, float b, float c, float d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public int CompareTo(Score o)
        {
            int r = Cmp(A, o.A);
            if (r != 0)
                return r;
            r = Cmp(B, o.B);
            if (r != 0)
                return r;
            r = Cmp(C, o.C);
            return r != 0 ? r : Cmp(D, o.D);
        }

        private static int Cmp(float x, float y)
        {
            return Math.Abs(x - y) <= Epsilon ? 0 : (x < y ? -1 : 1);
        }

        public static bool operator >(Score a, Score b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <(Score a, Score b)
        {
            return a.CompareTo(b) < 0;
        }
    }

    /// <summary>
    /// Goal scores. Decay follows the same curve for every food (PLAN §1.2), so full values rank
    /// foods exactly as lifetime averages would; duration is only the last tie-breaker.
    /// </summary>
    public static class Scoring
    {
        public static Score Of(Goal goal, FoodStats f)
        {
            return Of(goal, f.Health, f.Stamina, f.Eitr, f.Regen, f.BurnTime);
        }

        public static Score Of(Goal goal, IEnumerable<FoodStats> foods)
        {
            float h = 0f, s = 0f, e = 0f, r = 0f, b = 0f;
            foreach (FoodStats f in foods)
            {
                h += f.Health;
                s += f.Stamina;
                e += f.Eitr;
                r += f.Regen;
                b += f.BurnTime;
            }
            return Of(goal, h, s, e, r, b);
        }

        private static Score Of(Goal goal, float h, float s, float e, float r, float b)
        {
            switch (goal)
            {
                case Goal.Health:
                    return new Score(h, s + e, r, b);
                case Goal.Stamina:
                    return new Score(s, h + e, r, b);
                case Goal.Eitr:
                    return new Score(e, h + s, r, b);
                default:
                    return new Score(h + s, e, r, b);
            }
        }
    }
}
