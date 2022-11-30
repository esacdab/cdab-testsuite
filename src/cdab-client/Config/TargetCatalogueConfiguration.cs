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

namespace cdabtesttools.Config
{
    /// <summary>
    /// Data object class for the <em>service_providers.*.data.catalogue</em> nodes in the configuration YAML file.
    /// </summary>
    public class TargetCatalogueConfiguration
    {
        public Dictionary<string, CatalogueSetConfiguration> Sets { get; set; }

        public List<OpenSearchParameter> LocalParameters { get; set; }

        public string SystematicSearchMission { get; set; }

        public string SystematicSearchMissionRegex { get; set; }

        public string SystematicSearchProductType { get; set; }

        public string SystematicSearchProductTypeRegex { get; set; }

        public string SystematicSearchAoiWkt { get; set; }

        public string SystematicSearchAoiDescription { get; set; }

        public int SystematicSearchDays { get; set; }

        public bool AllowOpenSearch { get; set; }

        public bool? LatencyPolling { get; set; }

        public int? LatencyCheckInterval { get; set; }

        public int? LatencyCheckMaxDuration { get; set; }

        public int? LatencyCheckOffset { get; set; }

        public bool? LimitQuery { get; set; }

        public string DefaultBoundingBox { get; set; }

        public double? MaxAreaDegrees { get; set; }

        public string DefaultStartTime { get; set; }

        public string DefaultEndTime { get; set; }

        public long? MaxPeriodSeconds { get; set; }

        public int? QueryPollingInterval { get; set; }
    }
}