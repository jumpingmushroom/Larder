using Larder.Core;

namespace Larder.UI
{
    /// <summary>
    /// Eats a planned food from the bag through Humanoid.UseItem(..., fromInventoryGui: true),
    /// the same path as right-clicking it, so every vanilla check and message applies (PLAN §1.6).
    /// </summary>
    internal static class Eater
    {
        public static ItemDrop.ItemData FindInBag(Player p, string foodId)
        {
            if (p == null || string.IsNullOrEmpty(foodId) || FoodCatalog.FeastFood.ContainsKey(foodId))
                return null;
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
            {
                if (item != null && item.m_shared != null && item.m_shared.m_name == foodId)
                    return item;
            }
            return null;
        }

        public static bool CanEatNow(Player p, string foodId)
        {
            ItemDrop.ItemData item = FindInBag(p, foodId);
            return item != null && p.CanEat(item, false);
        }

        public static void Eat(string foodId)
        {
            Player p = Player.m_localPlayer;
            ItemDrop.ItemData item = FindInBag(p, foodId);
            if (item == null)
                return;
            p.UseItem(p.GetInventory(), item, true);
            Runtime.Refresh();
        }
    }
}
