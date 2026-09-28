using System.Collections.Generic;
using Larder.Core.Model;
using Xunit;
using static Larder.Tests.CookPlannerTests;

namespace Larder.Tests
{
    public class CookAdvisorTests
    {
        private static FoodStats F(string id, float hp)
        {
            return ComboSolverTests.F(id, hp);
        }

        private static readonly List<FoodStats> Pool = new List<FoodStats> { F("a", 30), F("b", 30), F("c", 30) };

        private static CookAdvice Advise(IEnumerable<Dish> dishes, ProducerIndex idx, Dictionary<string, int> stock,
            StationLevels st, System.Func<string, bool> known = null)
        {
            return CookAdvisor.Advise(Pool, Goal.Health, dishes, idx, stock, st, known ?? (_ => true));
        }

        [Fact]
        public void NoDishesNoAdvice()
        {
            CookAdvice a = Advise(new Dish[0], Index(), new Dictionary<string, int>(), Stations());
            Assert.Null(a.Ready);
            Assert.Null(a.Almost);
        }

        [Fact]
        public void ReadyDishThatImprovesIsSuggested()
        {
            var idx = Index(R("pie", "", 1, ("lox", 1)));
            CookAdvice a = Advise(new[] { new Dish(F("pie", 60)) }, idx, new Dictionary<string, int> { { "lox", 1 } }, Stations());
            Assert.Equal("pie", a.Ready.Dish.Food.Id);
            Assert.Contains(a.Ready.Combo, f => f.Id == "pie");
        }

        [Fact]
        public void DishThatDoesNotImproveIsIgnored()
        {
            var idx = Index(R("gruel", "", 1, ("oat", 1)));
            CookAdvice a = Advise(new[] { new Dish(F("gruel", 10)) }, idx, new Dictionary<string, int> { { "oat", 1 } }, Stations());
            Assert.Null(a.Ready);
        }

        [Fact]
        public void DishAlreadyOwnedIsIgnored()
        {
            var idx = Index(R("a", "", 1, ("x", 1)));
            CookAdvice a = Advise(new[] { new Dish(F("a", 99)) }, idx, new Dictionary<string, int> { { "x", 1 } }, Stations());
            Assert.Null(a.Ready);
        }

        [Fact]
        public void BestReadyDishWins()
        {
            var idx = Index(R("pie", "", 1, ("lox", 1)), R("soup", "", 1, ("lox", 1)));
            var dishes = new[] { new Dish(F("pie", 60)), new Dish(F("soup", 80)) };
            CookAdvice a = Advise(dishes, idx, new Dictionary<string, int> { { "lox", 1 } }, Stations());
            Assert.Equal("soup", a.Ready.Dish.Food.Id);
        }

        [Fact]
        public void AlmostOnlyWhenItBeatsTheReadyPick()
        {
            var idx = Index(R("pie", "", 1, ("lox", 1)), R("feast", "", 1, ("lox", 1), ("honey", 2)), R("snack", "", 1, ("honey", 1)));
            var dishes = new[] { new Dish(F("pie", 60)), new Dish(F("feast", 90)), new Dish(F("snack", 40)) };
            CookAdvice a = Advise(dishes, idx, new Dictionary<string, int> { { "lox", 1 } }, Stations());
            Assert.Equal("pie", a.Ready.Dish.Food.Id);
            Assert.Equal("feast", a.Almost.Dish.Food.Id);
            Assert.Equal(2, a.Almost.Plan.MissingItems["honey"]);
        }

        [Fact]
        public void AlmostSkipsDishesNeedingUnseenIngredients()
        {
            var idx = Index(R("feast", "", 1, ("honey", 2)));
            CookAdvice a = Advise(new[] { new Dish(F("feast", 90)) }, idx, new Dictionary<string, int>(), Stations(),
                id => id != "honey");
            Assert.Null(a.Almost);
        }

        [Fact]
        public void FeastDishIsPlannedByItsMakeId()
        {
            var idx = Index(R("feastItem", "", 1, ("lox", 1)));
            var dish = new Dish(F("feastFood", 70), "feastItem");
            CookAdvice a = Advise(new[] { dish }, idx, new Dictionary<string, int> { { "lox", 1 } }, Stations());
            Assert.Equal("feastItem", a.Ready.Plan.Root.ItemId);
        }
    }
}
