using System.Collections.Generic;
using Larder.Core.Model;

namespace Larder.Core
{
    internal static class ActiveFoods
    {
        public static List<ActiveFood> Read(Player p)
        {
            var list = new List<ActiveFood>();
            foreach (Player.Food f in p.GetFoods())
            {
                if (f != null && f.m_item != null && f.m_item.m_shared != null)
                    list.Add(new ActiveFood(f.m_item.m_shared.m_name, f.m_time, f.m_item.m_shared.m_foodBurnTime));
            }
            return list;
        }
    }
}
