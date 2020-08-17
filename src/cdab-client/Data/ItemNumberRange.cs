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

        [JsonIgnore]
        public string Key
        {
            get
            {
                return key;
            }
        }

        private string filterDefinition;

        public string FullName
        {
            get
            {
                return filterDefinition;
            }
        }

        private double min;

        public double Min
        {
            get
            {
                return min;
            }
        }

        private double max;

        public double Max
        {
            get
            {
                return max;
            }
        }

        private double step;

        public double Step
        {
            get
            {
                return step;
            }
        }

        private string formatter;
        private string label;
        private readonly Func<double[], Func<IOpenSearchResultItem, bool>> itemValueValidator;

        [JsonIgnore]
        public Func<double[], Func<IOpenSearchResultItem, bool>> ItemValueValidator
        {
            get
            {
                return itemValueValidator;
            }
        }

        private readonly Func<double[], Func<IOpenSearchResultCollection, bool>> resultsValidator;

        [JsonIgnore]
        public Func<double[], Func<IOpenSearchResultCollection, bool>> ResultsValidator
        {
            get
            {
                return resultsValidator;

            }
        }

        public string Label
        {
            get
            {
                return label;
            }
        }

        public string Formatter
        {
            get
            {
                return formatter;
            }
        }

        public Regex Parser { get; private set; }

        public ItemNumberRange(string key, string filterDefinition, double min, double max, double step, string formatter, Regex parser, string label, Func<double[], Func<IOpenSearchResultItem, bool>> itemValidator, Func<double[], Func<IOpenSearchResultCollection, bool>> resultsValidator)
        {
            this.key = key;
            this.filterDefinition = filterDefinition;
            this.min = min;
            this.max = max;
            this.step = step;
            this.formatter = formatter;
            this.label = label;
            this.itemValueValidator = itemValidator;
            this.resultsValidator = resultsValidator;
            this.Parser = parser;
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