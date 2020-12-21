/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

/*
cdab-client is part of the software suite used to run Test Scenarios 
for bechmarking various Copernicus Data Provider targets.
    
Copyright (C) 2020 Terradue Ltd, www.terradue.com
    
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published
by the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.
This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.
You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

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
