using System.Collections.Generic;
using Larder.Core.Model;
using UnityEngine;

namespace Larder.Core
{
    /// <summary>
    /// Every food the game knows and every way to make any item, read from ObjectDB and the
    /// ZNetScene prefabs at runtime (PLAN §1.1, §1.5), so patches and other mods' foods and
    /// stations are included. Rebuilt when the item, recipe or prefab count changes.
    /// </summary>
    internal static class FoodCatalog
    {
        public static readonly Dictionary<string, FoodStats> Foods = new Dictionary<string, FoodStats>();
        public static readonly Dictionary<string, ItemDrop.ItemData> Items = new Dictionary<string, ItemDrop.ItemData>();
        public static readonly Dictionary<string, string> FeastFood = new Dictionary<string, string>();
        public static readonly List<Dish> Dishes = new List<Dish>();
        public static ProducerIndex Producers = new ProducerIndex();
        private static readonly Dictionary<string, string> StationNames = new Dictionary<string, string>();

        private static int _items = -1, _recipes = -1, _prefabs = -1;

        public static void EnsureBuilt()
        {
            ObjectDB db = ObjectDB.instance;
            ZNetScene scene = ZNetScene.instance;
            if (db == null || scene == null || db.m_items.Count == 0)
                return;
            if (db.m_items.Count == _items && db.m_recipes.Count == _recipes && scene.m_prefabs.Count == _prefabs)
                return;
            Build(db, scene);
            _items = db.m_items.Count;
            _recipes = db.m_recipes.Count;
            _prefabs = scene.m_prefabs.Count;
        }

        public static string ItemName(string id)
        {
            return Localization.instance != null ? Localization.instance.Localize(id) : id;
        }

        public static string StationName(string id)
        {
            if (string.IsNullOrEmpty(id))
                return "by hand";
            string name;
            return StationNames.TryGetValue(id, out name) ? name : ItemName(id);
        }

        private static void Build(ObjectDB db, ZNetScene scene)
        {
            Foods.Clear();
            Items.Clear();
            FeastFood.Clear();
            Dishes.Clear();
            StationNames.Clear();
            var producers = new ProducerIndex();

            foreach (GameObject go in db.m_items)
            {
                ItemDrop drop = go != null ? go.GetComponent<ItemDrop>() : null;
                if (drop == null || drop.m_itemData == null || drop.m_itemData.m_shared == null)
                    continue;
                string id = drop.m_itemData.m_shared.m_name;
                if (string.IsNullOrEmpty(id) || Items.ContainsKey(id))
                    continue;
                Items[id] = drop.m_itemData;

                Feast feast = go.GetComponent<Feast>();
                ItemDrop foodDrop = feast != null && feast.m_foodItem != null ? feast.m_foodItem : drop;
                if (foodDrop.m_itemData == null || foodDrop.m_itemData.m_shared == null)
                    continue;
                if (feast != null)
                    FeastFood[id] = foodDrop.m_itemData.m_shared.m_name;
                AddFood(foodDrop.m_itemData);
            }

            foreach (Recipe r in db.m_recipes)
            {
                if (r == null || !r.m_enabled || r.m_item == null || r.m_resources == null)
                    continue;
                var inputs = new List<Ingredient>();
                foreach (Piece.Requirement req in r.m_resources)
                {
                    if (req != null && req.m_resItem != null && req.m_amount > 0)
                        inputs.Add(new Ingredient(req.m_resItem.m_itemData.m_shared.m_name, req.m_amount));
                }
                if (inputs.Count == 0)
                    continue;
                string station = r.m_craftingStation != null ? r.m_craftingStation.m_name : "";
                Name(station, station);
                producers.Add(new Producer(ProducerKind.Recipe, r.m_item.m_itemData.m_shared.m_name, r.m_amount,
                    station, r.m_minStationLevel, inputs, r.m_requireOnlyOneIngredient));
            }

            foreach (GameObject go in scene.m_prefabs)
            {
                if (go == null)
                    continue;
                Piece piece = go.GetComponent<Piece>();
                CookingStation cs = go.GetComponent<CookingStation>();
                if (cs != null)
                {
                    string sid = StationId(piece, cs.m_name);
                    foreach (CookingStation.ItemConversion c in cs.m_conversion)
                        AddConversion(producers, sid, c.m_from, c.m_to);
                }
                Smelter sm = go.GetComponent<Smelter>();
                if (sm != null)
                {
                    string sid = StationId(piece, sm.m_name);
                    foreach (Smelter.ItemConversion c in sm.m_conversion)
                        AddConversion(producers, sid, c.m_from, c.m_to);
                }
            }
            Producers = producers;

            foreach (KeyValuePair<string, ItemDrop.ItemData> kv in Items)
            {
                string foodId;
                if (!FeastFood.TryGetValue(kv.Key, out foodId))
                    foodId = kv.Key;
                FoodStats stats;
                if (Foods.TryGetValue(foodId, out stats) && producers.For(kv.Key).Count > 0)
                    Dishes.Add(new Dish(stats, kv.Key));
            }

            LarderPlugin.Log.LogInfo("Larder: catalogue " + Foods.Count + " foods, " + FeastFood.Count + " feasts, " +
                Dishes.Count + " dishes, " + producers.Count + " producers.");
        }

        private static void AddFood(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.SharedData s = item.m_shared;
            // Player.ConsumeItem only calls EatFood when m_food > 0 (PLAN §1.1).
            if (s.m_food <= 0f || Foods.ContainsKey(s.m_name))
                return;
            Foods[s.m_name] = new FoodStats(s.m_name, ItemName(s.m_name), s.m_food, s.m_foodStamina, s.m_foodEitr,
                s.m_foodBurnTime, s.m_foodRegen);
            if (!Items.ContainsKey(s.m_name))
                Items[s.m_name] = item;
        }

        private static void AddConversion(ProducerIndex producers, string stationId, ItemDrop from, ItemDrop to)
        {
            if (from == null || to == null)
                return;
            producers.Add(new Producer(ProducerKind.Conversion, to.m_itemData.m_shared.m_name, 1, stationId, 1,
                new[] { new Ingredient(from.m_itemData.m_shared.m_name, 1) }));
        }

        private static string StationId(Piece piece, string fallback)
        {
            string id = piece != null && !string.IsNullOrEmpty(piece.m_name) ? piece.m_name : fallback;
            Name(id, id);
            return id;
        }

        private static void Name(string id, string token)
        {
            if (!string.IsNullOrEmpty(id) && !StationNames.ContainsKey(id))
                StationNames[id] = ItemName(token);
        }
    }
}
