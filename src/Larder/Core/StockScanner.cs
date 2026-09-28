using System.Collections.Generic;
using Larder.Core.Model;
using UnityEngine;

namespace Larder.Core
{
    internal enum SourceKind
    {
        Eaten,
        Bag,
        Container,
        Feast,
        FeastItem
    }

    internal sealed class Source
    {
        public SourceKind Kind;
        public string Label;
        public float Distance;
        public int Count;
    }

    internal sealed class Snapshot
    {
        /// <summary>Bag + containers by shared name, for cooking.</summary>
        public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
        /// <summary>Where each food can be had, nearest first.</summary>
        public readonly Dictionary<string, List<Source>> FoodSources = new Dictionary<string, List<Source>>();
        public readonly List<FoodStats> Pool = new List<FoodStats>();
        public int Containers;
        public int Feasts;
    }

    /// <summary>
    /// Food and ingredients in the bag, in containers the player may open (the same checks as
    /// Container.Interact, PLAN §1.4) and on placed feasts with portions left. Reads only.
    /// </summary>
    internal static class StockScanner
    {
        public static Snapshot Take(Player player, List<Piece> nearby)
        {
            var snap = new Snapshot();
            AddInventory(snap, player.GetInventory(), SourceKind.Bag, "bag", 0f);

            Vector3 at = player.transform.position;
            long me = player.GetPlayerID();
            foreach (Piece piece in nearby)
            {
                float d = Vector3.Distance(piece.transform.position, at);

                Feast feast = piece.GetComponent<Feast>();
                if (feast != null)
                {
                    int left = feast.GetStack();
                    if (left > 0 && feast.m_foodItem != null)
                    {
                        snap.Feasts++;
                        AddFood(snap, feast.m_foodItem.m_itemData.m_shared.m_name, new Source
                        {
                            Kind = SourceKind.Feast,
                            Label = FoodCatalog.ItemName(piece.m_name) + " " + Meters(d) + " (" + left + " left)",
                            Distance = d,
                            Count = left
                        });
                    }
                    continue;
                }

                Container c = piece.GetComponentInChildren<Container>();
                // A build-placement ghost's Container never got a ZDO (Container.cs Awake, ~line
                // 67: m_inventory/m_piece are only set "if (m_nview.GetZDO() != null)"), so
                // GetInventory() is null and the private CheckAccess would NRE on m_piece; check
                // that before touching access at all. Tombstones (Container on the same
                // GameObject, TombStone.cs:37) are excluded (PLAN §1.4).
                if (c == null || c.GetComponent<TombStone>() != null)
                    continue;
                Inventory inv = c.GetInventory();
                if (inv == null || !CanOpen(c, me))
                    continue;
                bool vehicle = c.m_wagon != null || piece.GetComponent<Ship>() != null;
                if (vehicle && !PluginConfig.IncludeCartsAndShips.Value)
                    continue;
                snap.Containers++;
                AddInventory(snap, inv, SourceKind.Container, FoodCatalog.ItemName(c.m_name) + " " + Meters(d), d);
            }

            foreach (KeyValuePair<string, List<Source>> kv in snap.FoodSources)
            {
                kv.Value.Sort((x, y) => x.Distance.CompareTo(y.Distance));
                snap.Pool.Add(FoodCatalog.Foods[kv.Key]);
            }
            return snap;
        }

        /// <summary>Adds the player's active foods to the pool: they're already eaten, so they count as available.</summary>
        public static void AddEaten(Snapshot snap, List<ActiveFood> active)
        {
            foreach (ActiveFood a in active)
            {
                FoodStats f;
                if (!FoodCatalog.Foods.TryGetValue(a.Id, out f))
                    continue;
                if (!snap.FoodSources.ContainsKey(a.Id))
                    snap.Pool.Add(f);
                AddFood(snap, a.Id, new Source { Kind = SourceKind.Eaten, Label = "eaten", Distance = -1f, Count = 0 });
            }
        }

        private static bool CanOpen(Container c, long me)
        {
            if (c.m_checkGuardStone && !PrivateArea.CheckAccess(c.transform.position, 0f, false, false))
                return false;
            return c.CheckAccess(me);
        }

        private static void AddInventory(Snapshot snap, Inventory inv, SourceKind kind, string label, float d)
        {
            foreach (ItemDrop.ItemData item in inv.GetAllItems())
            {
                if (item == null || item.m_shared == null)
                    continue;
                string id = item.m_shared.m_name;
                int have;
                snap.Stock.TryGetValue(id, out have);
                snap.Stock[id] = have + item.m_stack;

                string food;
                if (FoodCatalog.FeastFood.TryGetValue(id, out food))
                    AddFood(snap, food, new Source { Kind = SourceKind.FeastItem, Label = label + ", place then eat", Distance = d, Count = item.m_stack });
                else if (FoodCatalog.Foods.ContainsKey(id))
                    AddFood(snap, id, new Source { Kind = kind, Label = label, Distance = d, Count = item.m_stack });
            }
        }

        private static void AddFood(Snapshot snap, string foodId, Source s)
        {
            if (!FoodCatalog.Foods.ContainsKey(foodId))
                return;
            List<Source> list;
            if (!snap.FoodSources.TryGetValue(foodId, out list))
                snap.FoodSources[foodId] = list = new List<Source>();
            list.Add(s);
        }

        private static string Meters(float d)
        {
            return Mathf.RoundToInt(d) + "m";
        }
    }
}
