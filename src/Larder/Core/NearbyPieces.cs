using System.Collections.Generic;
using UnityEngine;

namespace Larder.Core
{
    /// <summary>
    /// Pieces within a radius. The game keeps no list of containers, so this walks
    /// Piece.s_allPieces (private, publicized), which covers everything loaded (PLAN §1.4).
    /// Skips build-placement ghosts: a ghost is instantiated with ZNetView.m_forceDisableInit
    /// set (Player.cs ~3685), so its ZNetView is destroyed and never gets a ZDO (ZNetView.cs:57),
    /// but it stays in s_allPieces and is only SetActive(false) while equipped (Player.cs:1370-72).
    /// We skip anything whose ZNetView is missing/invalid, plus the live ghost object itself in
    /// case its ZNetView hasn't finished being destroyed this frame.
    /// </summary>
    internal static class NearbyPieces
    {
        public static List<Piece> Collect(Vector3 at, float radius)
        {
            var list = new List<Piece>();
            float r2 = radius * radius;
            Transform ghost = Ghost();
            foreach (Piece p in Piece.s_allPieces)
            {
                if (p == null || !IsReal(p.m_nview, p, ghost))
                    continue;
                if ((p.transform.position - at).sqrMagnitude <= r2)
                    list.Add(p);
            }
            return list;
        }

        /// <summary>The local player's build-placement ghost, or null.</summary>
        public static Transform Ghost()
        {
            Player player = Player.m_localPlayer;
            return player != null && player.m_placementGhost != null ? player.m_placementGhost.transform : null;
        }

        /// <summary>A placed, networked object: has a valid ZNetView and isn't part of the placement ghost.</summary>
        public static bool IsReal(ZNetView nview, Component c, Transform ghost)
        {
            if (nview == null || !nview.IsValid())
                return false;
            return ghost == null || !c.transform.IsChildOf(ghost);
        }
    }
}
