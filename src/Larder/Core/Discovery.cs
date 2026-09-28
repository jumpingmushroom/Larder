using Larder.Core.Model;

namespace Larder.Core
{
    /// <summary>
    /// Spoiler rules (PLAN §1.5): a recipe dish is known once the recipe is (Player.m_knownRecipes
    /// holds the recipe item's shared name); a conversion dish once its result is a known material.
    /// </summary>
    internal static class Discovery
    {
        public static bool DishKnown(Player p, Dish d)
        {
            if (PluginConfig.ShowUndiscovered.Value)
                return true;
            foreach (Producer pr in FoodCatalog.Producers.For(d.MakeId))
            {
                if (pr.Kind == ProducerKind.Recipe ? p.IsRecipeKnown(d.MakeId) : p.IsMaterialKnown(d.MakeId))
                    return true;
            }
            return false;
        }

        public static bool ItemKnown(Player p, string id)
        {
            return PluginConfig.ShowUndiscovered.Value || p.IsMaterialKnown(id);
        }
    }
}
