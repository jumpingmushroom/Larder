using System;
using System.Collections.Generic;

namespace Larder.Core.Model
{
    public enum ProducerKind
    {
        /// <summary>An ObjectDB recipe (cauldron, food preparation table, by hand).</summary>
        Recipe,
        /// <summary>A CookingStation or Smelter conversion (cooking station, oven, windmill).</summary>
        Conversion
    }

    public sealed class Ingredient
    {
        public readonly string ItemId;
        public readonly int Amount;

        public Ingredient(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }

    /// <summary>One way to make an item. StationId is the station's m_name token, "" for by hand.</summary>
    public sealed class Producer
    {
        public readonly ProducerKind Kind;
        public readonly string OutputId;
        public readonly int Yield;
        public readonly string StationId;
        public readonly int StationLevel;
        public readonly IList<Ingredient> Inputs;
        /// <summary>Recipe.m_requireOnlyOneIngredient: any one input is enough.</summary>
        public readonly bool AnyOneInput;

        public Producer(ProducerKind kind, string outputId, int yield, string stationId, int stationLevel,
            IList<Ingredient> inputs, bool anyOneInput = false)
        {
            Kind = kind;
            OutputId = outputId;
            Yield = Math.Max(1, yield);
            StationId = stationId ?? "";
            StationLevel = Math.Max(1, stationLevel);
            Inputs = inputs ?? new Ingredient[0];
            AnyOneInput = anyOneInput;
        }
    }

    public sealed class ProducerIndex
    {
        private static readonly IList<Producer> None = new Producer[0];
        private readonly Dictionary<string, List<Producer>> _byOutput = new Dictionary<string, List<Producer>>();

        public int Count { get; private set; }

        public void Add(Producer p)
        {
            List<Producer> list;
            if (!_byOutput.TryGetValue(p.OutputId, out list))
                _byOutput[p.OutputId] = list = new List<Producer>();
            list.Add(p);
            Count++;
        }

        public IList<Producer> For(string itemId)
        {
            List<Producer> list;
            return itemId != null && _byOutput.TryGetValue(itemId, out list) ? list : None;
        }
    }

    public interface IStationLevels
    {
        /// <summary>Highest level of that station in range; 0 when none is.</summary>
        int Level(string stationId);
    }

    public sealed class StationLevels : IStationLevels
    {
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        public IEnumerable<KeyValuePair<string, int>> All
        {
            get { return _levels; }
        }

        public void Set(string id, int level)
        {
            if (string.IsNullOrEmpty(id))
                return;
            int have;
            if (!_levels.TryGetValue(id, out have) || level > have)
                _levels[id] = level;
        }

        public int Level(string stationId)
        {
            int level;
            return stationId != null && _levels.TryGetValue(stationId, out level) ? level : 0;
        }
    }
}
