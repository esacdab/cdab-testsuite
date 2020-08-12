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