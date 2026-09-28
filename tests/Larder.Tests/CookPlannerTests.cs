using System.Collections.Generic;
using Larder.Core.Model;
using Xunit;

namespace Larder.Tests
{
    public class CookPlannerTests
    {
        internal static Producer R(string output, string station, int level, params (string id, int n)[] inputs)
        {
            var list = new List<Ingredient>();
            foreach (var i in inputs)
                list.Add(new Ingredient(i.id, i.n));
            return new Producer(ProducerKind.Recipe, output, 1, station, level, list);
        }

        internal static Producer C(string output, string station, string from)
        {
            return new Producer(ProducerKind.Conversion, output, 1, station, 1, new[] { new Ingredient(from, 1) });
        }

        internal static ProducerIndex Index(params Producer[] ps)
        {
            var idx = new ProducerIndex();
            foreach (Producer p in ps)
                idx.Add(p);
            return idx;
        }

        internal static StationLevels Stations(params (string id, int level)[] s)
        {
            var st = new StationLevels();
            foreach (var x in s)
                st.Set(x.id, x.level);
            return st;
        }

        private static Dictionary<string, int> Stock(params (string id, int n)[] s)
        {
            var d = new Dictionary<string, int>();
            foreach (var x in s)
                d[x.id] = x.n;
            return d;
        }

        [Fact]
        public void EverythingInStockIsReady()
        {
            var idx = Index(R("pie", "cauldron", 1, ("lox", 4), ("flour", 4)));
            CookPlan p = CookPlanner.Plan("pie", idx, Stock(("lox", 4), ("flour", 9)), Stations(("cauldron", 1)));
            Assert.True(p.ReadyNow);
            Assert.Equal(2, p.Root.Inputs.Count);
            Assert.Equal(4, p.Root.Inputs[0].FromStock);
        }

        [Fact]
        public void ShortfallIsReportedAsMissing()
        {
            var idx = Index(R("pie", "cauldron", 1, ("lox", 4)));
            CookPlan p = CookPlanner.Plan("pie", idx, Stock(("lox", 1)), Stations(("cauldron", 1)));
            Assert.False(p.ReadyNow);
            Assert.Equal(3, p.MissingItems["lox"]);
            Assert.Equal(3, p.MissingUnits);
        }

        [Fact]
        public void StationTooLowIsReported()
        {
            var idx = Index(R("pie", "cauldron", 3, ("lox", 1)));
            CookPlan p = CookPlanner.Plan("pie", idx, Stock(("lox", 1)), Stations(("cauldron", 2)));
            Assert.False(p.ReadyNow);
            Assert.Equal("cauldron", p.MissingStations[0].StationId);
            Assert.Equal(3, p.MissingStations[0].Level);
        }

        [Fact]
        public void ByHandNeedsNoStation()
        {
            var idx = Index(R("mix", "", 1, ("berry", 2)));
            Assert.True(CookPlanner.Plan("mix", idx, Stock(("berry", 2)), Stations()).ReadyNow);
        }

        [Fact]
        public void IntermediatesAreCraftedThreeStepsDeep()
        {
            // bread <- dough (oven) ; dough <- flour (cauldron) ; flour <- barley (windmill)
            var idx = Index(
                C("bread", "oven", "dough"),
                R("dough", "cauldron", 1, ("flour", 10)),
                new Producer(ProducerKind.Conversion, "flour", 1, "windmill", 1, new[] { new Ingredient("barley", 1) }));
            CookPlan p = CookPlanner.Plan("bread", idx, Stock(("barley", 10)),
                Stations(("oven", 1), ("cauldron", 1), ("windmill", 1)));
            Assert.True(p.ReadyNow);
            CookStep dough = p.Root.Inputs[0];
            Assert.Equal("dough", dough.ItemId);
            Assert.NotNull(dough.Via);
            Assert.Equal("flour", dough.Inputs[0].ItemId);
            Assert.Equal(10, dough.Inputs[0].Crafts);
        }

        [Fact]
        public void FourthStepIsNotCrafted()
        {
            var idx = Index(C("e", "s", "d"), C("d", "s", "c"), C("c", "s", "b"), C("b", "s", "a"));
            CookPlan p = CookPlanner.Plan("e", idx, Stock(("a", 1)), Stations(("s", 1)));
            Assert.False(p.ReadyNow);
            Assert.Equal(1, p.MissingItems["b"]);
        }

        [Fact]
        public void CyclesTerminate()
        {
            var idx = Index(C("x", "s", "y"), C("y", "s", "x"));
            CookPlan p = CookPlanner.Plan("x", idx, Stock(), Stations(("s", 1)));
            Assert.False(p.ReadyNow);
            Assert.True(p.MissingItems.ContainsKey("x"));
        }

        [Fact]
        public void StockIsNotCountedTwice()
        {
            // dish needs 2 salt and 1 brine; brine needs 1 salt. Only 2 salt in stock.
            var idx = Index(R("dish", "", 1, ("salt", 2), ("brine", 1)), R("brine", "", 1, ("salt", 1)));
            CookPlan p = CookPlanner.Plan("dish", idx, Stock(("salt", 2)), Stations());
            Assert.Equal(1, p.MissingItems["salt"]);
        }

        [Fact]
        public void YieldReducesCrafts()
        {
            var idx = Index(
                R("dish", "", 1, ("dough", 4)),
                new Producer(ProducerKind.Recipe, "dough", 3, "", 1, new[] { new Ingredient("flour", 2) }));
            CookPlan p = CookPlanner.Plan("dish", idx, Stock(("flour", 4)), Stations());
            Assert.True(p.ReadyNow);
            Assert.Equal(2, p.Root.Inputs[0].Crafts);
        }

        [Fact]
        public void PrefersTheProducerWhoseStationIsHere()
        {
            var idx = Index(C("meat", "ironstation", "raw"), C("meat", "cookstation", "raw"));
            CookPlan p = CookPlanner.Plan("meat", idx, Stock(("raw", 1)), Stations(("cookstation", 1)));
            Assert.True(p.ReadyNow);
            Assert.Equal("cookstation", p.Root.Via.StationId);
        }

        [Fact]
        public void AnyOneInputUsesWhateverIsInStock()
        {
            var any = new Producer(ProducerKind.Recipe, "stew", 1, "", 1,
                new[] { new Ingredient("boar", 1), new Ingredient("deer", 1) }, anyOneInput: true);
            CookPlan p = CookPlanner.Plan("stew", Index(any), Stock(("deer", 1)), Stations());
            Assert.True(p.ReadyNow);
            Assert.Single(p.Root.Inputs);
            Assert.Equal("deer", p.Root.Inputs[0].ItemId);
        }

        [Fact]
        public void NothingMakesItReturnsNull()
        {
            Assert.Null(CookPlanner.Plan("unobtainium", Index(), Stock(), Stations()));
        }
    }
}
