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
