using System.Collections.Generic;
using System.Text;
using Larder.Core.Model;

namespace Larder.Core
{
    /// <summary>Plain-text plan for the console and the log (the panel has its own rich-text formatting).</summary>
    internal static class Report
    {
        public static List<string> Lines(PlanView v)
        {
            var lines = new List<string>();
            if (v.Error != null)
            {
                lines.Add("Larder: couldn't plan: " + v.Error);
                return lines;
            }
            Snapshot s = v.Snapshot;
            lines.Add("Larder: goal " + v.Goal + ", radius " + PluginConfig.Radius.Value + " m, " + s.Containers +
                " containers, " + s.Feasts + " feasts, " + s.Stock.Count + " item kinds, " + s.Pool.Count +
                " foods, " + v.Millis.ToString("0.00") + " ms");
            foreach (PlannedSlot p in v.Slots.Planned)
            {
                FoodStats f = p.Food;
                lines.Add("Larder:   " + f.Name + " " + f.Health + "/" + f.Stamina + "/" + f.Eitr + " " +
                    Labels.Duration(f.BurnTime) + " — " + FirstSource(s, f.Id) + ", " + p.State +
                    (p.Seconds > 0f ? " " + Labels.Duration(p.Seconds) : ""));
            }
            foreach (OtherFood o in v.Slots.Others)
                lines.Add("Larder:   outside plan: " + FoodCatalog.ItemName(o.Id) + " frees in " + Labels.Duration(o.SecondsUntilFree));
            lines.Add("Larder: plan " + v.Planned.Health + "/" + v.Planned.Stamina + "/" + v.Planned.Eitr +
                " regen " + v.Planned.Regen + "; now " + v.Now.Health.ToString("0") + "/" + v.Now.Stamina.ToString("0") +
                "/" + v.Now.Eitr.ToString("0"));
            Cook(lines, "ready", v.Cook.Ready);
            Cook(lines, "almost", v.Cook.Almost);
            var st = new StringBuilder("Larder: stations:");
            foreach (KeyValuePair<string, int> kv in v.Stations.All)
                st.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            lines.Add(st.ToString());
            return lines;
        }

        private static string FirstSource(Snapshot s, string id)
        {
            List<Source> list;
            return s.FoodSources.TryGetValue(id, out list) && list.Count > 0 ? list[0].Label : "?";
        }

        private static void Cook(List<string> lines, string label, CookSuggestion c)
        {
            if (c == null)
            {
                lines.Add("Larder: cook " + label + ": none");
                return;
            }
            lines.Add("Larder: cook " + label + ": " + c.Dish.Food.Name + " via " + FoodCatalog.StationName(c.Plan.Root.Via.StationId));
            Tree(lines, c.Plan.Root, 2);
            foreach (KeyValuePair<string, int> m in c.Plan.MissingItems)
                lines.Add("Larder:     missing " + FoodCatalog.ItemName(m.Key) + " x" + m.Value);
            foreach (StationNeed n in c.Plan.MissingStations)
                lines.Add("Larder:     needs " + FoodCatalog.StationName(n.StationId) + " level " + n.Level);
        }

        private static void Tree(List<string> lines, CookStep step, int indent)
        {
            foreach (CookStep c in step.Inputs)
            {
                lines.Add("Larder: " + new string(' ', indent * 2) + FoodCatalog.ItemName(c.ItemId) + " " + c.FromStock + "/" + c.Need +
                    (c.Via != null ? " (+" + (c.Need - c.FromStock) + " via " + FoodCatalog.StationName(c.Via.StationId) + ")" : "") +
                    (c.Missing > 0 ? " MISSING " + c.Missing : ""));
                if (c.Via != null)
                    Tree(lines, c, indent + 1);
            }
        }
    }
}
