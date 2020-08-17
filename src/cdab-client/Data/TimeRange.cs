using System;
using Newtonsoft.Json;

namespace cdabtesttools.Data
{
    public class TimeRange
    {
        private string startFullName;

        [JsonIgnore]
        public string ParameterStartFullName
        {
            get
            {
                return startFullName;
            }
        }

        private string endFulllName;

        [JsonIgnore]
        public string ParameterEndFullName
        {
            get
            {
                return endFulllName;
            }
        }

        private DateTime start;

        public DateTime Start
        {
            get
            {
                return start;
            }
        }

        private DateTime stop;

        public DateTime Stop
        {
            get
            {
                return stop;
            }
        }

        public TimeRange(string startFullName, string endFullName, DateTime start, DateTime stop)
        {
            this.startFullName = startFullName;
            this.endFulllName = endFullName;
            this.start = start;
            this.stop = stop;
        }
    }
}