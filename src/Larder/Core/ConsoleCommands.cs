using System;
using System.Collections.Generic;
using Larder.Core.Model;

namespace Larder.Core
{
    /// <summary>
    /// "larder" prints the plan; "larder foods" the catalogue; "larder goal &lt;name&gt;" sets the goal.
    /// Output is mirrored to the BepInEx log for build/logs.sh.
    /// </summary>
    internal static class ConsoleCommands
    {
        public static void Register()
        {
            new Terminal.ConsoleCommand("larder", "Larder: food plan (foods | goal <balanced|health|stamina|eitr>)",
                delegate (Terminal.ConsoleEventArgs args)
                {
                    Player p = Player.m_localPlayer;
                    if (p == null)
                    {
                        Say(args.Context, "Larder: not in a game.");
                        return;
                    }
                    string sub = args.Length > 1 ? args[1].ToLowerInvariant() : "";
                    if (sub == "foods")
                    {
                        Foods(args.Context);
                        return;
                    }
                    if (sub == "goal")
                    {
                        Goal g;
                        if (args.Length > 2 && Enum.TryParse(args[2], true, out g))
                            GoalStore.Set(p, g);
                        Say(args.Context, "Larder: goal " + GoalStore.Get(p));
                        Runtime.Refresh();
                        return;
                    }
                    foreach (string line in Report.Lines(Planner.Compute(p, GoalStore.Get(p))))
                        Say(args.Context, line);
                });
        }

        internal static void Say(Terminal ctx, string line)
        {
            if (ctx != null)
                ctx.AddString(line);
            LarderPlugin.Log.LogInfo(line);
        }

        private static void Foods(Terminal ctx)
        {
            FoodCatalog.EnsureBuilt();
            var foods = new List<FoodStats>(FoodCatalog.Foods.Values);
            foods.Sort((a, b) => (b.Health + b.Stamina + b.Eitr).CompareTo(a.Health + a.Stamina + a.Eitr));
            foreach (FoodStats f in foods)
                Say(ctx, "Larder:   " + f.Id + " " + f.Name + " " + f.Health + "/" + f.Stamina + "/" + f.Eitr +
                    " " + Labels.Duration(f.BurnTime) + " regen " + f.Regen + " producers " + FoodCatalog.Producers.For(f.Id).Count);
            foreach (KeyValuePair<string, string> kv in FoodCatalog.FeastFood)
                Say(ctx, "Larder:   feast item " + kv.Key + " -> food " + kv.Value);
            Say(ctx, "Larder: " + foods.Count + " foods, " + FoodCatalog.FeastFood.Count + " feasts, " + FoodCatalog.Dishes.Count + " dishes.");
        }
    }
}
