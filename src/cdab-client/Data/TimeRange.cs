using System;
using Newtonsoft.Json;

namespace cdabtesttools.Data
{
    public class TimeRange
    {
        private string startFullName;
        private string endFulllName;
        private DateTime start;
        private DateTime stop;

        [JsonIgnore]
        public string ParameterStartFullName => startFullName;

        [JsonIgnore]
        public string ParameterEndFullName => endFulllName;

        public DateTime Start => start;

        public DateTime Stop => stop;

        public TimeRange(string startFullName, string endFullName, DateTime start, DateTime stop)
        {
            this.startFullName = startFullName;
            this.endFulllName = endFullName;
            this.start = start;
            this.stop = stop;
        }
    }
}