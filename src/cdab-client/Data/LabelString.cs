using System;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class LabelString : IMissionFilterOption
    {
        private string value;
        private string label;
        private readonly Func<IOpenSearchResultItem, bool> validator;
        private Func<NameValueCollection, bool> condition;

        public string Value => value;

        public string Label => label;

        [JsonIgnore]
        public Func<IOpenSearchResultItem, bool> Validator => validator;

        public Func<NameValueCollection, bool> Condition => condition;

        public LabelString(string value, string label, Func<IOpenSearchResultItem, bool> validator, Func<NameValueCollection, bool> condition = null)
        {
            this.value = value;
            this.label = label;
            this.validator = validator;
            this.condition = condition;
        }
    }
}