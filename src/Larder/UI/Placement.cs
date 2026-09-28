using System.Collections.Generic;
using Larder.Core.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Larder.UI
{
    /// <summary>
    /// Positions the panel on screen (PLAN §2.3). Works in screen pixels (y up) so it doesn't care
    /// which transform the panel or the other UI hang off: every rect is read through
    /// GetWorldCorners + WorldToScreenPoint, the target top-left is chosen and clamped in screen
    /// space, then converted into the panel's parent space and written to localPosition (the panel's
    /// pivot is its top-left). Offsets and spacings are in canvas units, scaled by the canvas'
    /// scaleFactor. Nothing is cached between calls except scratch buffers.
    /// </summary>
    internal static class Placement
    {
        private const float Gap = 24f;
        private const float Spacing = 12f;
        private const float MinSize = 8f;

        private static readonly Vector3[] Corners = new Vector3[4];
        private static readonly List<Graphic> Graphics = new List<Graphic>();
        private static readonly List<ScreenRect> Candidates = new List<ScreenRect>();

        public static void Apply(RectTransform panel, InventoryGui gui, Transform exclude)
        {
            var parent = panel.parent as RectTransform;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            if (parent == null || canvas == null || gui == null || gui.m_player == null)
                return;
            canvas = canvas.rootCanvas;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float sf = canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Rect screen = canvas.pixelRect;

            ScreenRect player = ScreenRectOf(gui.m_player, cam);
            if (player.Right - player.Left < 1f || player.Top - player.Bottom < 1f)
                return; // Collapsed (e.g. mid-animation): keep the last position until the next refresh.

            float x, y;
            switch (PluginConfig.Placement.Value)
            {
                case PanelPlacement.Manual:
                    x = screen.xMin;
                    y = screen.yMax;
                    break;
                case PanelPlacement.Below:
                {
                    RectTransform anchor = gui.m_container != null && gui.m_container.gameObject.activeInHierarchy
                        ? gui.m_container
                        : gui.m_player;
                    ScreenRect a = ScreenRectOf(anchor, cam);
                    x = a.Left;
                    y = a.Bottom - Spacing * sf;
                    break;
                }
                default:
                {
                    Transform root = gui.m_inventoryRoot != null ? gui.m_inventoryRoot : gui.m_player;
                    CollectCandidates(root, panel, exclude, cam, screen.width);
                    float right = PlacementMath.ClusterRight(player.Left, player.Right, player.Top, player.Bottom,
                        Candidates, Gap * sf);
                    x = right + Spacing * sf;
                    y = player.Top;
                    break;
                }
            }
            x += PluginConfig.OffsetX.Value * sf;
            y += PluginConfig.OffsetY.Value * sf;

            // Clamp on screen, using the panel's current laid-out size.
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            ScreenRect self = ScreenRectOf(panel, cam);
            float w = self.Right - self.Left;
            float h = self.Top - self.Bottom;
            x = Mathf.Max(screen.xMin, Mathf.Min(x, screen.xMax - w));
            y = Mathf.Min(screen.yMax, Mathf.Max(y, screen.yMin + h));

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(x, y), cam, out local))
                return;
            panel.localPosition = new Vector3(local.x, local.y, panel.localPosition.z);
        }

        /// <summary>
        /// Everything drawn under the inventory root that could be beside the inventory: active,
        /// enabled Graphics with visible colour, at least 8x8 px, not ours, not the root itself,
        /// not empty text, and not wider than half the screen (a backdrop, not a neighbour).
        /// </summary>
        private static void CollectCandidates(Transform root, RectTransform panel, Transform exclude, Camera cam,
            float screenWidth)
        {
            Candidates.Clear();
            Graphics.Clear();
            root.GetComponentsInChildren(false, Graphics);
            foreach (Graphic g in Graphics)
            {
                if (g == null || !g.enabled || g.color.a <= 0.01f || g.transform == root)
                    continue;
                var text = g as TMP_Text;
                if (text != null && string.IsNullOrEmpty(text.text))
                    continue;
                RectTransform rt = g.rectTransform;
                if (rt.IsChildOf(panel) || (exclude != null && rt.IsChildOf(exclude)))
                    continue;
                ScreenRect r = ScreenRectOf(rt, cam);
                float w = r.Right - r.Left;
                if (w < MinSize || r.Top - r.Bottom < MinSize || w > screenWidth * 0.5f)
                    continue;
                Candidates.Add(r);
            }
            Graphics.Clear();
        }

        private static ScreenRect ScreenRectOf(RectTransform rt, Camera cam)
        {
            rt.GetWorldCorners(Corners);
            float l = float.MaxValue, r = float.MinValue, t = float.MinValue, b = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, Corners[i]);
                l = Mathf.Min(l, p.x);
                r = Mathf.Max(r, p.x);
                t = Mathf.Max(t, p.y);
                b = Mathf.Min(b, p.y);
            }
            return new ScreenRect(l, r, t, b);
        }
    }
}
