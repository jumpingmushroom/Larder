using System.Collections.Generic;
using UnityEngine;

namespace Larder.Core
{
    /// <summary>
    /// Pieces within a radius. The game keeps no list of containers, so this walks
    /// Piece.s_allPieces (private, publicized), which covers everything loaded (PLAN §1.4).
    /// </summary>
    internal static class NearbyPieces
    {
        public static List<Piece> Collect(Vector3 at, float radius)
        {
            var list = new List<Piece>();
            float r2 = radius * radius;
            foreach (Piece p in Piece.s_allPieces)
            {
                if (p != null && (p.transform.position - at).sqrMagnitude <= r2)
                    list.Add(p);
            }
            return list;
        }
    }
}
