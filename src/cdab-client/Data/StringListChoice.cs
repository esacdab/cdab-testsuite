using System;
using System.Linq;
using System.Text.RegularExpressions;
using cdabtesttools.Config;
using Newtonsoft.Json;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class StringListChoice : IMissionFilter
    {
        private readonly string key;
        private string fullName;
        private LabelString[] labelStrings;

        [JsonIgnore]
        public string Key => key;

        public string FullName => fullName;

        [JsonProperty("options")]
        public LabelString[] LabelStrings => labelStrings;

        public StringListChoice(string key, string fqdn, LabelString[] labelStrings)
        {
            this.key = key;
            this.fullName = fqdn;
            this.labelStrings = labelStrings;
        }

        public FilterDefinition GetFilterDefinition(string value)
        {
            var choice = LabelStrings.First(sl => sl.Value == value);

            return new FilterDefinition(key, fullName, choice.Value, choice.Label, choice.Validator, null);
        }

        public Func<IOpenSearchResultItem, bool> GetItemValidator(OpenSearchParameter parameter)
        {
            var labelString = LabelStrings.FirstOrDefault(ls => ls.Value == parameter.Value);
            if (labelString == null) return null;
            return labelString.Validator;
        }
    }
}