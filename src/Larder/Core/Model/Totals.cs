using System.Collections.Generic;

namespace Larder.Core.Model
{
    /// <summary>What a set of foods gives at full value, on top of the player's base health and stamina.</summary>
    public struct Totals
    {
        public float Health;
        public float Stamina;
        public float Eitr;
        public float Regen;

        public static Totals Of(IEnumerable<FoodStats> foods, float baseHealth, float baseStamina)
        {
            var t = new Totals { Health = baseHealth, Stamina = baseStamina };
            foreach (FoodStats f in foods)
            {
                t.Health += f.Health;
                t.Stamina += f.Stamina;
                t.Eitr += f.Eitr;
                t.Regen += f.Regen;
            }
            return t;
        }
    }
}
