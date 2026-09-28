using System.Collections.Generic;
using Larder.Core.Model;
using Xunit;

namespace Larder.Tests
{
    public class PlacementMathTests
    {
        // Screen space, y up: the band is the inventory block, 100..500 wide, 1000 (top) .. 600 (bottom).
        private const float L = 100f, R = 500f, Top = 1000f, Bottom = 600f, Gap = 24f;

        private static ScreenRect Rect(float left, float right, float top, float bottom)
        {
            return new ScreenRect(left, right, top, bottom);
        }

        [Fact]
        public void NoCandidatesKeepsStartRight()
        {
            Assert.Equal(R, PlacementMath.ClusterRight(L, R, Top, Bottom, new List<ScreenRect>(), Gap));
        }

        [Fact]
        public void AdjacentGridExtendsTheCluster()
        {
            var rects = new List<ScreenRect> { Rect(510f, 700f, 990f, 700f) };
            Assert.Equal(700f, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }

        [Fact]
        public void FarPanelBeyondTheGapIsIgnored()
        {
            var rects = new List<ScreenRect> { Rect(3200f, 3800f, 1000f, 200f) };
            Assert.Equal(R, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }

        [Fact]
        public void ChainsThroughTwoAdjacentRectsInAnyOrder()
        {
            // The second only becomes reachable once the first has extended the cluster.
            var rects = new List<ScreenRect> { Rect(720f, 900f, 900f, 800f), Rect(510f, 700f, 990f, 700f) };
            Assert.Equal(900f, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }

        [Fact]
        public void CandidateOutsideTheVerticalBandIsIgnored()
        {
            var rects = new List<ScreenRect> { Rect(510f, 700f, 500f, 300f), Rect(510f, 800f, 1080f, 1010f) };
            Assert.Equal(R, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }

        [Fact]
        public void CandidateLeftOfTheClusterIsIgnored()
        {
            var rects = new List<ScreenRect> { Rect(50f, 800f, 900f, 700f) };
            Assert.Equal(R, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }

        [Fact]
        public void ChildInsideTheClusterDoesNotShrinkIt()
        {
            var rects = new List<ScreenRect> { Rect(150f, 300f, 900f, 700f) };
            Assert.Equal(R, PlacementMath.ClusterRight(L, R, Top, Bottom, rects, Gap));
        }
    }
}
