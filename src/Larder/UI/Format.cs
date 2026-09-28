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

        public static string Cook(PlanView v)
        {
            if (v.Cook.Ready == null && v.Cook.Almost == null)
                return v.Combo.Count > 0 ? C(Dim, "Nothing you can cook improves this combo.") : "";
            var sb = new StringBuilder();
            if (v.Cook.Ready != null)
            {
                sb.Append("<b>Cook next</b>\n");
                Suggestion(sb, v.Cook.Ready, v);
            }
            if (v.Cook.Almost != null)
            {
                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("<b>If you had…</b>\n");
                Suggestion(sb, v.Cook.Almost, v);
            }
            return sb.ToString();
        }

        private static void Suggestion(StringBuilder sb, CookSuggestion c, PlanView v)
        {
            Core.Model.Totals t = Core.Model.Totals.Of(c.Combo, v.Planned.Health - Sum(v.Combo, 0), v.Planned.Stamina - Sum(v.Combo, 1));
            sb.Append(c.Dish.Food.Name).Append("  →  ").Append(Stats(t.Health, t.Stamina, t.Eitr))
              .Append(C(Dim, "  (" + Delta(t.Health - v.Planned.Health, "hp") + Delta(t.Stamina - v.Planned.Stamina, "st") +
                  Delta(t.Eitr - v.Planned.Eitr, "eitr") + ")")).Append('\n');
            Producer via = c.Plan.Root.Via;
            sb.Append("<indent=1em>").Append(Station(via, c.Plan.Root.StationMissing)).Append("</indent>\n");
            Steps(sb, c.Plan.Root, 1);
            foreach (StationNeed n in c.Plan.MissingStations)
            {
                if (n.StationId != via.StationId)
                    sb.Append("<indent=1em>").Append(C(Bad, "needs " + FoodCatalog.StationName(n.StationId) + " level " + n.Level)).Append("</indent>\n");
            }
        }

        private static float Sum(List<FoodStats> foods, int stat)
        {
            float s = 0f;
            foreach (FoodStats f in foods)
                s += stat == 0 ? f.Health : f.Stamina;
            return s;
        }

        private static string Delta(float d, string unit)
        {
            if (d > -0.5f && d < 0.5f)
                return "";
            return (d > 0 ? "+" : "") + d.ToString("0") + " " + unit + " ";
        }

        private static string Station(Producer p, bool missing)
        {
            string name = FoodCatalog.StationName(p.StationId);
            if (p.StationId.Length > 0 && p.StationLevel > 1)
                name += " level " + p.StationLevel;
            return missing ? C(Bad, "at " + name + " (not in range)") : C(Good, "at " + name);
        }

        private static void Steps(StringBuilder sb, CookStep step, int depth)
        {
            foreach (CookStep c in step.Inputs)
            {
                string color = c.Missing > 0 ? Bad : c.Via != null ? St : Good;
                sb.Append("<indent=").Append(depth + 1).Append("em>")
                  .Append(C(color, FoodCatalog.ItemName(c.ItemId) + " " + (c.Need - c.Missing) + "/" + c.Need));
                if (c.Via != null)
                    sb.Append(C(Dim, "  make " + (c.Need - c.FromStock) + " " + Station(c.Via, c.StationMissing)));
                sb.Append("</indent>\n");
                if (c.Via != null)
                    Steps(sb, c, depth + 1);
            }
        }
    }
}
