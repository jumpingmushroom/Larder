using System;
using System.Collections.Generic;
using Larder.Core;
using Larder.Core.Model;

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

        /// <summary>The "Eat N" button: eats every planned food it safely can from the bag, in order,
        /// re-reading the game between eats since UseItem changes it. Bag only (PLAN §2.3); never
        /// throws out into Unity's event system.</summary>
        public static void EatAll()
        {
            try
            {
                Player p = Player.m_localPlayer;
                PlanView view = Runtime.Last;
                if (p != null && view != null && view.Combo.Count > 0)
                    EatLoop(p, view.Combo);
            }
            catch (Exception e)
            {
                LarderPlugin.WarnOnce("Larder: eat all failed", e);
            }
            Runtime.Refresh();
        }

        private static void EatLoop(Player p, List<FoodStats> plan)
        {
            for (int i = 0; i < EatOrder.MaxEats; i++)
            {
                List<ActiveFood> active = ActiveFoods.Read(p);
                ISet<string> inBag = BagFoods(p);
                List<FoodStats> next = EatOrder.Next(plan, active, inBag, Game.m_foodRate);
                if (next.Count == 0)
                    break;
                string foodId = next[0].Id;
                ItemDrop.ItemData item = FindInBag(p, foodId);
                if (item == null || !p.CanEat(item, false))
                    break;
                // Inventory.RemoveOneItem decrements m_stack on the same object rather than removing
                // it while m_stack > 1 (stacked food, the common case), so "gone from the bag" alone
                // can't tell a successful eat from a blocked one; a lower stack count can. Checking
                // this exact item (not just "a stack of this food"), the same way UseItem itself
                // checks inventory.ContainsItem(item) (Humanoid.cs:923), avoids a false failure when
                // another stack of the same food also sits in the bag.
                int stackBefore = item.m_stack;
                p.UseItem(p.GetInventory(), item, true);
                bool consumed = !p.GetInventory().ContainsItem(item) || item.m_stack < stackBefore;
                if (!consumed)
                    break; // still in the bag at the same count: the eat didn't take (status effect conflict, etc.)
            }
        }

        /// <summary>Shared names of bag items that are food and not feast items (same rule as FindInBag).</summary>
        private static HashSet<string> BagFoods(Player p)
        {
            var set = new HashSet<string>();
            foreach (ItemDrop.ItemData item in p.GetInventory().GetAllItems())
            {
                if (item == null || item.m_shared == null)
                    continue;
                string id = item.m_shared.m_name;
                if (FoodCatalog.Foods.ContainsKey(id) && !FoodCatalog.FeastFood.ContainsKey(id))
                    set.Add(id);
            }
            return set;
        }
    }
}
