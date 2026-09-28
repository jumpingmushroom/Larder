using System.Collections.Generic;
using Larder.Core.Model;
using UnityEngine;

namespace Larder.Core
{
    /// <summary>Crafting stations by m_name at their highest level in range, plus conversion stations present (level 1).</summary>
    internal static class StationScanner
    {
        public static StationLevels Scan(Vector3 at, float radius, List<Piece> nearby)
        {
            var levels = new StationLevels();
            float r2 = radius * radius;
            // CraftingStation.Start adds itself to m_allStations when it has no ZNetView
            // (CraftingStation.cs:83-85), so a hammer ghost joins the list; skip it like NearbyPieces does.
            Transform ghost = NearbyPieces.Ghost();
            foreach (CraftingStation s in CraftingStation.m_allStations)
            {
                if (s != null && NearbyPieces.IsReal(s.m_nview, s, ghost) && (s.transform.position - at).sqrMagnitude <= r2)
                    levels.Set(s.m_name, s.GetLevel());
            }
            foreach (Piece p in nearby)
            {
                if (p.GetComponent<CookingStation>() != null || p.GetComponent<Smelter>() != null)
                    levels.Set(p.m_name, 1);
            }
            return levels;
        }
    }
}
