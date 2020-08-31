using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using cdabtesttools.Config;
using Newtonsoft.Json;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class ItemNumberRange : IMissionFilter
    {
        private readonly string key;
        private string filterDefinition;
        private double min;
        private double max;
        private double step;
        private string formatter;
        private Regex parser;
        private string label;
        private readonly Func<double[], Func<IOpenSearchResultItem, bool>> itemValueValidator;
        private readonly Func<double[], Func<IOpenSearchResultCollection, bool>> resultsValidator;

        [JsonIgnore]
        public string Key => key;

        public string FullName => filterDefinition;

        public double Min => min;

        public double Max => max;

        public double Step => step;

        [JsonIgnore]
        public Func<double[], Func<IOpenSearchResultItem, bool>> ItemValueValidator => itemValueValidator;

        [JsonIgnore]
        public Func<double[], Func<IOpenSearchResultCollection, bool>> ResultsValidator => resultsValidator;

        public string Formatter => formatter;

        public Regex Parser => parser;

        public string Label => label;

        public ItemNumberRange(string key, string filterDefinition, double min, double max, double step, string formatter, Regex parser, string label, Func<double[], Func<IOpenSearchResultItem, bool>> itemValidator, Func<double[], Func<IOpenSearchResultCollection, bool>> resultsValidator)
        {
            this.key = key;
            this.filterDefinition = filterDefinition;
            this.min = min;
            this.max = max;
            this.step = step;
            this.formatter = formatter;
            this.parser = parser;
            this.label = label;
            this.itemValueValidator = itemValidator;
            this.resultsValidator = resultsValidator;
        }

        public Func<IOpenSearchResultItem, bool> GetItemValidator(OpenSearchParameter parameter)
        {
            if (ItemValueValidator == null)
                return null;

            IEnumerable<double> values = ParseFilterValue(parameter.Value);
            if (values == null) return null;
            return ItemValueValidator.Invoke(values.ToArray());
        }

        private IEnumerable<double> ParseFilterValue(string value)
        {
            if (Parser == null) return null;
            Match match = Parser.Match(value);
            if (!match.Success)
                return null;
            try
            {
                return match.Groups.Cast<Group>().Skip(1).Select(g => double.Parse(g.Value));
            }
            catch
            {
                return null;
            }
        }
    }
}