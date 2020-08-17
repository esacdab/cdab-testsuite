using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    public class FilterDefinition
    {
        private string fullName;

        public string FullName
        {
            get
            {
                return fullName;
            }
        }

        private string value;

        public string Key => key;
        public string Value
        {
            get
            {
                return value;
            }

            private set
            {
                this.value = value;
                if (value.StartsWith("[NOW"))
                {
                    if (value.EndsWith("M]"))
                    {
                        int months = int.Parse(value[4] + value.Substring(5, value.Length - 7));
                        this.value = DateTime.UtcNow.AddMonths(months).ToString("s");
                    }
                    if (value.EndsWith("D]"))
                    {
                        int months = int.Parse(value[4] + value.Substring(5, value.Length - 7));
                        this.value = DateTime.UtcNow.AddDays(months).ToString("s");
                    }
                }
            }
        }

        private string label;
        private readonly System.Func<IOpenSearchResultCollection, bool> resultsValidator;
        private readonly string key;

        [JsonIgnore]
        public System.Func<IOpenSearchResultCollection, bool> ResultsValidator
        {
            get
            {
                return resultsValidator;
            }
        }

        private System.Func<IOpenSearchResultItem, bool> validator1;

        [JsonIgnore]
        public System.Func<IOpenSearchResultItem, bool> ItemValidator
        {
            get
            {
                return validator1;
            }
        }

        public string Label
        {
            get
            {
                return label;
            }
        }

        public FilterDefinition(string key, string fullName, string value, string label, System.Func<IOpenSearchResultItem, bool> validator1, System.Func<IOpenSearchResultCollection, bool> resultsValidator)
        {
            if(string.IsNullOrEmpty(fullName))
                throw new ArgumentNullException(fullName);
            this.key = key;
            this.fullName = fullName;
            this.Value = value;
            this.label = label;
            this.validator1 = validator1;
            this.resultsValidator = resultsValidator;
        }
    }
}