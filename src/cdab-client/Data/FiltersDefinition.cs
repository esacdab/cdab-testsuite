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
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using cdabtesttools.Config;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class FiltersDefinition
    {
        private string name;
        private List<FilterDefinition> filters;

        public string Name { get => name; set => name = value; }
        
        public string Label
        {
            get
            {
                string label = "";

                foreach (var filter in filters)
                {
                    label += string.Format("{0} ", filter.Label);
                }

                return label.Trim(' ');
            }
        }

        public List<FilterDefinition> Filters { get => filters; set => filters = value; }

        public FiltersDefinition(string name)
        {
            this.name = name;
            filters = new List<FilterDefinition>();
        }

        public FiltersDefinition(FiltersDefinition filtersd)
        {
            this.name = filtersd.name;
            this.filters = new List<FilterDefinition>(filtersd.GetFilters());
        }

        internal void AddFilter(string key, string fullName, string value, string label, Func<IOpenSearchResultItem, bool> itemValidator, Func<IOpenSearchResultCollection, bool> resultsValidator)
        {
            filters.Add(new FilterDefinition(key, fullName, value, label, itemValidator, resultsValidator));
        }

        internal void AddFilter(FilterDefinition filterd)
        {
            filters.Add(filterd);
        }

        internal void AddFilters(IEnumerable<FilterDefinition> filterds)
        {
            filters.AddRange(filterds);
        }

        internal NameValueCollection GetNameValueCollection()
        {
            NameValueCollection nvc = new NameValueCollection();

            foreach (var filter in filters)
            {
                nvc.Set(filter.FullName, filter.Value);
            }

            return nvc;
        }

        internal IEnumerable<FilterDefinition> GetFilters()
        {
            return filters;
        }

        internal void RemoveFilter(string fullName)
        {
            var filter = filters.FirstOrDefault(f => f.FullName == fullName);
            if (filter != null)
                filters.Remove(filter);
        }

        internal bool IsApplied(string key, object value)
        {
            var filter = filters.FirstOrDefault(f => f.FullName == key);
            if (filter == null)
                return false;
            if (!(value is string))
                return false;
            return filter.Value == (string)value;
        }

        internal void AddFilters(IEnumerable<OpenSearchParameter> parameters)
        {
            if (parameters == null || parameters.Count() == 0)
                return;
            AddFilters(
                parameters.Select(p => new FilterDefinition(p.Key, p.FullName, p.Value, p.Label, null, null))
            );
        }

        internal void AddFilters(IEnumerable<OpenSearchParameter> parameters, Mission mission)
        {
            if (parameters == null || parameters.Count() == 0)
                return;
            foreach (var parameter in parameters)
            {
                AddFilter(new FilterDefinition(parameter.Key,parameter.FullName, parameter.Value, parameter.Label, mission.GetItemValidator(parameter), null));
            }
        }
    }
}
