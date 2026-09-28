namespace Larder.Core.Model
{
    /// <summary>
    /// A food's full values, copied from its item data. Id is the item's shared name
    /// (e.g. "$item_loxpie"), which is also how the game tells foods apart in Player.CanEat.
    /// </summary>
    public sealed class FoodStats
    {
        public readonly string Id;
        public readonly string Name;
        public readonly float Health;
        public readonly float Stamina;
        public readonly float Eitr;
        public readonly float BurnTime;
        public readonly float Regen;

        public FoodStats(string id, string name, float health, float stamina, float eitr, float burnTime, float regen)
        {
            Id = id;
            Name = name;
            Health = health;
            Stamina = stamina;
            Eitr = eitr;
            BurnTime = burnTime;
            Regen = regen;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
