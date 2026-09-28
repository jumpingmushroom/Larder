using System.Collections.Generic;
using System.Text;
using Larder.Core;
using Larder.Core.Model;

namespace Larder.UI
{
    /// <summary>Rich-text strings for the panel. Colours follow the game's tooltip (eitr #9090ff).</summary>
    internal static class Format
    {
        public const string Hp = "#ff8080";
        public const string St = "#ffe080";
        public const string Eitr = "#9090ff";
        public const string Time = "#ffa040";
        public const string Dim = "#b0b0b0";
        public const string Good = "#80e080";
        public const string Bad = "#ff7070";

        public static string C(string color, string text)
        {
            return "<color=" + color + ">" + text + "</color>";
        }

        public static string Stats(float hp, float st, float eitr)
        {
            string s = C(Hp, hp.ToString("0") + " hp") + "  " + C(St, st.ToString("0") + " st");
            return eitr > 0f ? s + "  " + C(Eitr, eitr.ToString("0") + " eitr") : s;
        }

        public static string Row(PlannedSlot slot, PlanView v)
        {
            FoodStats f = slot.Food;
            var sb = new StringBuilder();
            sb.Append("<b>").Append(f.Name).Append("</b>\n");
            sb.Append(Stats(f.Health, f.Stamina, f.Eitr)).Append("  ").Append(C(Time, Labels.Duration(f.BurnTime)));
            string buff = Buff(f.Id);
            if (buff != null)
                sb.Append("  ").Append(C(Dim, "+ " + buff));
            sb.Append("\n<size=85%>").Append(C(Dim, Where(v.Snapshot, f.Id) + " · " + State(slot))).Append("</size>");
            return sb.ToString();
        }

        public static string State(PlannedSlot slot)
        {
            switch (slot.State)
            {
                case SlotState.Active:
                    return "active, " + Labels.Duration(slot.Seconds) + " left";
                case SlotState.RefreshNow:
                    return C(Good, "can refresh now");
                case SlotState.EatLater:
                    return "eat in " + Labels.Duration(slot.Seconds);
                default:
                    return C(Good, "eat now");
            }
        }

        public static string Totals(PlanView v)
        {
            return "<b>Plan</b>  " + Stats(v.Planned.Health, v.Planned.Stamina, v.Planned.Eitr) +
                "  " + C(Dim, "regen " + v.Planned.Regen.ToString("0")) +
                "\n<size=85%>" + C(Dim, "now " + v.Now.Health.ToString("0") + " hp  " + v.Now.Stamina.ToString("0") + " st" +
                    (v.Now.Eitr > 0f ? "  " + v.Now.Eitr.ToString("0") + " eitr" : "")) + "</size>";
        }

        public static string Others(PlanView v)
        {
            var sb = new StringBuilder();
            foreach (OtherFood o in v.Slots.Others)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append(C(Dim, FoodCatalog.ItemName(o.Id) + " isn't in the plan: " +
                    (o.SecondsUntilFree <= 0f ? "replaceable now" : "slot frees in " + Labels.Duration(o.SecondsUntilFree))));
            }
            return sb.ToString();
        }

        private static string Where(Snapshot s, string id)
        {
            List<Source> list;
            if (s == null || !s.FoodSources.TryGetValue(id, out list) || list.Count == 0)
                return "?";
            foreach (Source src in list)
            {
                if (src.Kind != SourceKind.Eaten)
                    return src.Label;
            }
            return list[0].Label;
        }

        private static string Buff(string foodId)
        {
            ItemDrop.ItemData item;
            if (!FoodCatalog.Items.TryGetValue(foodId, out item) || item.m_shared.m_consumeStatusEffect == null)
                return null;
            return FoodCatalog.ItemName(item.m_shared.m_consumeStatusEffect.m_name);
        }
    }
}
