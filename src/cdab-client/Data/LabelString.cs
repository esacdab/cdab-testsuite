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

        public string Value
        {
            get
            {
                return value;
            }
        }

        private string label;
        private readonly Func<IOpenSearchResultItem, bool> validator;

        [JsonIgnore]
        public Func<IOpenSearchResultItem, bool> Validator
        {
            get
            {
                return validator;
            }
        }

        public string Label
        {
            get
            {
                return label;
            }
        }

        public Func<NameValueCollection, bool> Condition { get; }

        public LabelString(string value, string label, Func<IOpenSearchResultItem, bool> validator, Func<NameValueCollection, bool> condition = null)
        {
            this.value = value;
            this.label = label;
            this.validator = validator;
            Condition = condition;
        }
    }
}