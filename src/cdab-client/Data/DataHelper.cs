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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using cdabtesttools.Config;
using cdabtesttools.Target;
using log4net;
using NetTopologySuite.IO;
using Terradue.Metadata.EarthObservation.OpenSearch.Extensions;
using Terradue.OpenSearch;
using Terradue.OpenSearch.Result;

namespace cdabtesttools.Data
{
    /// <summary>
    /// Helper functions related to EO data discovery and search.
    /// </summary>
    public static class DataHelper
    {
        private static ILog log = LogManager.GetLogger(typeof(DataHelper));
        private static WKTReader wktreader = new WKTReader();

        public static FiltersDefinition GenerateOfflineDataFiltersDefinition(int max, IEnumerable<Mission> missions, Dictionary<string, CatalogueSetConfiguration> catConfig)
        {
            FiltersDefinition _filtersDefinition = Mission.ShuffleSimpleRandomFiltersCombination(missions, catConfig, 1).First();
            _filtersDefinition.Name = "Offline-Random";

            _filtersDefinition.AddFilter("archiveStatus", "{http://a9.com/-/opensearch/extensions/eo/1.0/}statusSubType",
               "Offline", "Offline data", Mission.GetArchivingStatusValidator(Terradue.ServiceModel.Ogc.Eop21.StatusSubTypeValueEnumerationType.OFFLINE), null);
            _filtersDefinition.AddFilter("sensingEnd", "{http://a9.com/-/opensearch/extensions/time/1.0/}end", DateTime.UtcNow.Subtract(TimeSpan.FromDays(700)).ToString("s"), "more than 2 years ago", null, null);

            // Relative orbit cannot be queried via ODATA interface
            foreach (FilterDefinition f in _filtersDefinition.Filters)
            {
                if (f.FullName == "{http://a9.com/-/opensearch/extensions/eo/1.0/}track")
                {
                    log.DebugFormat("Filter for track will be removed (not queryable via ODATA interface)");
                }
            }
            _filtersDefinition.RemoveFilter("{http://a9.com/-/opensearch/extensions/eo/1.0/}track");

            return _filtersDefinition;

        }

        internal static IEnumerable<FiltersDefinition> GenerateOfflineDataStatusFiltersDefinition(OfflineDataStatus offlineDataStatus, TargetSiteWrapper target)
        {
            List<FiltersDefinition> _offlineFiltersDefinition = new List<FiltersDefinition>();

            foreach (var offlineDataStatusItem in offlineDataStatus.OfflineData.Where(di => di.TargetSiteName == target.Name))
            {
                FiltersDefinition fd = new FiltersDefinition(offlineDataStatusItem.Identifier);
                fd.AddFilter("uid", "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid", offlineDataStatusItem.Identifier, offlineDataStatusItem.Identifier, null, null);
                fd.RemoveFilter("{http://a9.com/-/opensearch/extensions/eo/1.0/}timeliness");
                // to force usage of ODATA for DHUS wrapper
                fd.AddFilter("archiveStatus", "dummy", "dummy", "", null, null);
                _offlineFiltersDefinition.Add(fd);
            }

            return _offlineFiltersDefinition;
        }

        public static IEnumerable<FiltersDefinition> GenerateBulkSystematicDataFiltersDefinition(TargetSiteWrapper target)
        {

            List<FiltersDefinition> bulkDataDefs = new List<FiltersDefinition>();

            FiltersDefinition _filtersDefinition = new FiltersDefinition("Systematic");

            // Get search parameters from configuration
            string mission = target.TargetSiteConfig.Data.Catalogue.SystematicSearchMission;
            string missionRegex = target.TargetSiteConfig.Data.Catalogue.SystematicSearchMissionRegex;
            string productType = target.TargetSiteConfig.Data.Catalogue.SystematicSearchProductType;
            string productTypeRegex = target.TargetSiteConfig.Data.Catalogue.SystematicSearchProductTypeRegex;
            string aoiWkt = target.TargetSiteConfig.Data.Catalogue.SystematicSearchAoiWkt;
            string aoiDescription = target.TargetSiteConfig.Data.Catalogue.SystematicSearchAoiDescription;
            int days = target.TargetSiteConfig.Data.Catalogue.SystematicSearchDays;

            // Set default values if parameters are not configured
            if (mission == null) {
                mission = "Sentinel-1";
                if (productType == null) productType = "GRD";
            }
            if (aoiWkt == null)
            {
                aoiWkt = "POLYGON((-5.664 14.532,-5.196 13.991,-4.854 13.969,-4.877 13.637,-4.114 13.938,-3.96 13.378,-3.443 13.158,-3.27 13.698,-2.874 13.654,-2.839 14.054,-2.474 14.299,-2 14.191,-1.98 14.476,-0.745 15.066,-1.686 15.431,-2.532 15.322,-2.816 15.774,-3.262 15.857,-3.8 15.491,-4.135 15.81,-5.23 15.674,-5.1 15.196,-5.546 14.931,-5.664 14.532))";
                aoiDescription = "over Mopti floodable area in Mali";
            }
            if (aoiDescription == null)
            {
                int pos = aoiWkt.IndexOf(',');
                if (pos > 0) aoiDescription = String.Format("AOI: {0}...", aoiWkt.Substring(0, pos));
                else aoiDescription = "AOI: unknown area";
            }
            if (days == 0) days = 7;

            _filtersDefinition.AddFilter("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform",
               mission, mission,
               missionRegex == null ? null : Mission.GetIdentifierValidator(new Regex(missionRegex)), null);

            _filtersDefinition.AddFilter("productType", "{http://a9.com/-/opensearch/extensions/eo/1.0/}productType",
                productType, productType,
                productTypeRegex == null ? null : Mission.GetIdentifierValidator(new Regex(productTypeRegex)), null);

            var geom = wktreader.Read(aoiWkt);

            _filtersDefinition.AddFilter("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
                aoiWkt,
                aoiDescription,
                Mission.GetGeometryValidator(geom), null);

            var since = DateTime.UtcNow.Subtract(TimeSpan.FromDays(days)).ToUniversalTime();

            _filtersDefinition.AddFilter("modified", "{http://purl.org/dc/terms/}modified",
                since.ToString("s") + "Z",
                "ingested since " + since.ToString(),
                Mission.GetIngestionDateValidator(since), null);

            // _filtersDefinition.AddFilter("archiveStatus", "{http://a9.com/-/opensearch/extensions/eo/1.0/}statusSubType", "online", "Online", null, null);

            bulkDataDefs.Add(_filtersDefinition);

            return bulkDataDefs;

        }

        /// <summary>
        /// Creates a list of cross filter definitions for a target site against a reference target site.
        /// </summary>
        /// <param name="setName">The name of the catalogue set.</param>
        /// <param name="setConfiguration">The configuration set</param>
        /// <param name="target">The target</param>
        /// <returns>A list of cross filter definitions.</returns>
        internal static IEnumerable<CrossCatalogueCoverageFiltersDefinition> GenerateCrossCatalogueCoverageFiltersDefinition(string setName, CatalogueSetConfiguration setConfiguration, TargetSiteWrapper target)
        {
            List<CrossCatalogueCoverageFiltersDefinition> bulkDataDefs = new List<CrossCatalogueCoverageFiltersDefinition>();

            // Let's get the baseline
            var catalogueSetConfiguration = ResolveBaseline(setName, setConfiguration);

            // Let's get the reference target
            TargetSiteWrapper ref_target = new TargetSiteWrapper(setConfiguration.ReferenceTargetSite, Configuration.Current.GetTargetSiteConfiguration(setConfiguration.ReferenceTargetSite));
            // Let's build the filter defintions
            foreach (FiltersDefinition pt in GenerateFilterDefinitionsFromCollectionsDefinition(catalogueSetConfiguration))
            {
                FiltersDefinition _targetFiltersDefinition = new FiltersDefinition("[target]");

                _targetFiltersDefinition.AddFilters(pt.Filters);
                _targetFiltersDefinition.AddFilter("count", "{http://a9.com/-/spec/opensearch/1.1/}count", "0", "", null, null);
                try
                {
                    _targetFiltersDefinition.AddFilters(catalogueSetConfiguration.Parameters);
                }
                catch { }
                FiltersDefinition _refFiltersDefinition = new FiltersDefinition(_targetFiltersDefinition);
                _refFiltersDefinition.Name = "[reference]";
                // special filter for filtering out product that are not considered as in the reference baseline
                try
                {
                    _targetFiltersDefinition.AddFilters(setConfiguration.Parameters);
                }
                catch { }

                bulkDataDefs.Add(new CrossCatalogueCoverageFiltersDefinition(
                    new TargetAndFiltersDefinition(target, _targetFiltersDefinition),
                    new TargetAndFiltersDefinition(ref_target, _refFiltersDefinition)
                    ));
            }
            return bulkDataDefs;

        }

        public static CatalogueSetConfiguration ResolveBaseline(string setName, CatalogueSetConfiguration setConfiguration)
        {
            switch (setConfiguration.Type)
            {
                // ... unless it is a baseline and we need to get the reference baseline
                case TargetCatalogueSetType.baseline:
                    // if configured in the catalogue set
                    var referenceSetId = setConfiguration.ReferenceSetId;
                    // otherwise, we use the same set name
                    if (string.IsNullOrEmpty(referenceSetId))
                        referenceSetId = setName;
                    // We check that the baseline exists
                    if (!Configuration.Current.Data.Sets.ContainsKey(referenceSetId))
                    {
                        log.WarnFormat("Target Site Catalogue Set named '{0}' reference a catalogue reference not found '{1}', skipping!", setName, setConfiguration.ReferenceSetId);
                        return null;
                    }
                    return Configuration.Current.Data.Sets[referenceSetId];
                case TargetCatalogueSetType.local:
                default:
                    return setConfiguration;
            }

        }

        private static IEnumerable<FiltersDefinition> GenerateFilterDefinitionsFromCollectionsDefinition(CatalogueSetConfiguration catDefinition)
        {
            List<FiltersDefinition> filtersDefinitions = new List<FiltersDefinition>();

            foreach (var collection in catDefinition.Collections)
            {
                var fds = new FiltersDefinition(collection.Key, collection.Value);
                fds.AddFilters(collection.Value.Parameters);
                fds.AddFilters(catDefinition.Parameters);
                filtersDefinitions.Add(fds);
            }

            return filtersDefinitions;
        }

        public static IEnumerable<CrossCatalogueCoverageFiltersDefinition> GenerateDataAvailabilityLatencyFiltersDefinition(string setName, CatalogueSetConfiguration setConfiguration, TargetSiteWrapper target)
        {

            List<CrossCatalogueCoverageFiltersDefinition> bulkDataDefs = new List<CrossCatalogueCoverageFiltersDefinition>();

            // Let's get the baseline
            var catalogueSetConfiguration = ResolveBaseline(setName, setConfiguration);

            TargetSiteWrapper ref_target = new TargetSiteWrapper(setConfiguration.ReferenceTargetSite, Configuration.Current.GetTargetSiteConfiguration(setConfiguration.ReferenceTargetSite));

            foreach (FiltersDefinition fd in GenerateFilterDefinitionsFromCollectionsDefinition(catalogueSetConfiguration))
            {
                // if there is no sensing start defined in the catalogue set
                // add a sensing time for the last 40 days
                if (!fd.Filters.Any(f => f.FullName == "{http://a9.com/-/opensearch/extensions/time/1.0/}start"))
                {
                    float max = 40;
                    var startDate = DateTime.UtcNow.Subtract(TimeSpan.FromDays(max));
                    fd.RemoveFilter("{http://a9.com/-/opensearch/extensions/time/1.0/}start");
                    fd.AddFilter("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", startDate.ToString("s"), string.Format("last {0} days", max), null, null);
                }
                fd.AddFilter("count", "{http://a9.com/-/spec/opensearch/1.1/}count", "20", "", null, null);
                bulkDataDefs.Add(new CrossCatalogueCoverageFiltersDefinition(
                    new TargetAndFiltersDefinition(target, fd),
                    new TargetAndFiltersDefinition(ref_target, fd)
                ));
            }
            return bulkDataDefs;

        }

        public static IEnumerable<FiltersDefinition> GenerateDataOperationalLatencyFiltersDefinition(string setName, CatalogueSetConfiguration setConfiguration)
        {
            List<FiltersDefinition> bulkDataDefs = new List<FiltersDefinition>();

            // Let's get the baseline
            var catalogueSetConfiguration = ResolveBaseline(setName, setConfiguration);

            foreach (FiltersDefinition fd in GenerateFilterDefinitionsFromCollectionsDefinition(catalogueSetConfiguration))
            {
                float max = 240;
                var startDate = DateTime.UtcNow.Subtract(TimeSpan.FromHours(max));
                fd.RemoveFilter("{http://a9.com/-/opensearch/extensions/time/1.0/}timeliness");
                fd.RemoveFilter("{http://a9.com/-/opensearch/extensions/time/1.0/}start");
                fd.AddFilter("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", startDate.ToString("s"), string.Format("last {0} hours", max), null, null);
                fd.AddFilter("count", "{http://a9.com/-/spec/opensearch/1.1/}count", "50", "", null, null);
                bulkDataDefs.Add(fd);
            }
            return bulkDataDefs;
        }

        internal static FiltersDefinition GenerateFiltersDefinitionFromItem(string label, IOpenSearchResultItem item)
        {
            FiltersDefinition fd = new FiltersDefinition(string.Format("{0}-{1}", label, item.Identifier));

            if (item == null) return fd;

            fd.AddFilter("uid", "{http://a9.com/-/opensearch/extensions/geo/1.0/}uid", item.Identifier, item.Identifier, null, null);

            var platform = item.FindPlatformShortName();
            if (!string.IsNullOrEmpty(platform))
            {
                fd.AddFilter("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", platform, platform, null, null);
            }

            var instrument = item.FindInstrumentShortName();
            if (!string.IsNullOrEmpty(instrument))
            {
                fd.AddFilter("instrument", "{http://a9.com/-/opensearch/extensions/eo/1.0/}instrument", instrument, instrument, null, null);
            }

            var productType = item.FindProductType();
            if (!string.IsNullOrEmpty(productType))
            {
                fd.AddFilter("productType", "{http://a9.com/-/opensearch/extensions/eo/1.0/}productType", productType, productType, null, null);
            }

            var processingLevel = item.FindProcessingLevel();
            if (!string.IsNullOrEmpty(processingLevel))
            {
                fd.AddFilter("processingLevel", "{http://a9.com/-/opensearch/extensions/eo/1.0/}processingLevel", processingLevel, processingLevel, null, null);
            }

            try
            {
                var startDate = item.FindStartDate();
                if (startDate != DateTime.MinValue)
                    fd.AddFilter("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", startDate.ToString("O"), string.Format("starting at {0}", startDate.ToString()), null, null);

                var endDate = item.FindEndDate();
                if (endDate != DateTime.MinValue)
                    fd.AddFilter("sensingEnd", "{http://a9.com/-/opensearch/extensions/time/1.0/}end", endDate.ToString("O"), string.Format("ending at {0}", endDate.ToString()), null, null);

            }
            catch { }

            return fd;
        }

        private static bool CheckTimelinessParameter(TargetSiteWrapper target)
        {
            var os = target.CreateOpenSearchableEntity();
            var parameters = os.GetOpenSearchParameters(os.DefaultMimeType);
            return parameters.AllKeys.Any(key =>
            {
                return key == "timeliness" || parameters[key].Contains("timeliness");
            });
        }

        private static IEnumerable<IEnumerable<FilterDefinition>> GenerateTimeRangeForMission(Mission mission)
        {
            var current = mission.Lifetime.Start;
            List<DateTime[]> timeRanges = new List<DateTime[]>();
            while (current <= mission.Lifetime.Stop)
            {
                var start = current;
                var stop = start.AddSeconds(0);
                stop = stop.AddDays(-stop.Day + 1);
                stop = stop.AddMonths(1);
                timeRanges.Add(new DateTime[2] { start, stop });
                current = stop;
            }
            return timeRanges.Select(tr =>
            {
                var fds = new FilterDefinition[2];
                fds[0] = new FilterDefinition("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", tr[0].ToString("s"), string.Format("{0}Z", tr[0].ToString("yyyy/MM/dd")), null, null);
                fds[1] = new FilterDefinition("sensingEnd", "{http://a9.com/-/opensearch/extensions/time/1.0/}end", tr[1].ToString("s"), string.Format("{0}Z", tr[1].ToString("yyyy/MM/dd")), null, null);
                return fds;
            });
        }

        private static IEnumerable<IEnumerable<FilterDefinition>> GenerateTimeRangeForCoverage()
        {
            List<IEnumerable<FilterDefinition>> timeRanges = new List<IEnumerable<FilterDefinition>>();
            // All
            var fds = new FilterDefinition[0];
            timeRanges.Add(fds);
            // Last 24 Months
            fds = new FilterDefinition[1];
            var date = DateTime.UtcNow.AddMonths(-24);
            fds[0] = new FilterDefinition("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", date.ToString("s") + "Z", "last 24 months", null, null);
            timeRanges.Add(fds);
            // Last 12 Months
            fds = new FilterDefinition[1];
            date = DateTime.UtcNow.AddMonths(-12);
            fds[0] = new FilterDefinition("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", date.ToString("s") + "Z", "last 12 months", null, null);
            timeRanges.Add(fds);
            // Last 3 Months
            fds = new FilterDefinition[1];
            date = DateTime.UtcNow.AddMonths(-3);
            fds[0] = new FilterDefinition("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", date.ToString("s") + "Z", "last 3 months", null, null);
            timeRanges.Add(fds);
            // Last Month
            fds = new FilterDefinition[1];
            date = DateTime.UtcNow.AddMonths(-1);
            fds[0] = new FilterDefinition("sensingStart", "{http://a9.com/-/opensearch/extensions/time/1.0/}start", date.ToString("s") + "Z", "last month", null, null);
            timeRanges.Add(fds);

            return timeRanges;
        }

        private static IEnumerable<FilterDefinition> GetCatalogueCoverageGeographicFilters(Mission mission)
        {
            List<FilterDefinition> geoms = new List<FilterDefinition>();
            // Europe
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((-10.547 36.173,-2.109 36.031,6.855 38.548,11.25 37.996,20.391 34.452,35.156 34.162,42.363 67.942,26.895 72.342,-26.016 67.136,-10.547 36.173))",
             "Europe", null, null
             ));
            // North America
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((-169.453 51.836,-141.328 56.753,-120.234 25.8,-72.422 25.8,-48.516 50.736,-59.766 70.378,-170.156 71.301,-169.453 51.836))",
             "North America", null, null
             ));
            // Africa
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((-18.457 22.431,-18.984 33.578,6.855 37.996,32.52 30.902,43.594 11.695,54.844 13.923,47.988 -26.274,19.16 -35.03,10.723 -15.623,5.449 4.04,-9.492 3.689,-17.754 12.039,-18.457 22.431))",
             "Africa", null, null
             ));
            //Asia
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((32.695 41.245,40.781 67.475,52.383 75.931,76.992 78.56,151.523 76.101,194.063 70.845,191.25 59.356,124.102 -9.449,62.227 7.014,43.242 12.211,31.641 31.053,32.695 41.245))",
             "Asia", null, null
             ));
            //Oceania
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((108.984 -22.106,131.484 0.352,170.156 -1.582,198.633 -31.053,169.102 -51.399,105.469 -37.719,108.984 -22.106))",
             "Oceania", null, null
             ));
            //Pacific Ocean
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((149.414 -1.406,125.859 19.643,163.477 56.365,177.187 60.759,216.563 57.704,236.602 30.751,274.219 1.758,277.734 -14.605,287.227 -19.311,279.492 -58.814,174.727 -58.263,180.352 -38.273,149.414 -1.406))",
             "Pacific", null, null
             ));
            //Atlantic Ocean
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((283.008 31.354,297.422 42.553,310.078 47.04,301.289 58.078,330.82 62.915,356.836 62.915,341.719 49.611,348.398 35.461,338.203 16.299,345.938 4.215,363.867 2.811,374.414 -33.724,376.523 -56.945,298.125 -57.516,292.148 -51.399,303.398 -37.719,326.953 -8.059,297.773 13.923,283.008 31.354))",
             "Atlantic", null, null
             ));
            //Indian Ocean
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((46.758 -2.109,61.523 19.311,76.289 4.215,93.164 6.665,111.094 -15.961,121.992 -58.078,25.664 -60.24,44.648 -1.758,46.758 -2.109))",
             "Indian", null, null
             ));
            //Arctic Ocean
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((-180 60,180 60,180 90,-180 90,-180 60))",
             "Arctic", null, null
             ));
            //Antarctica
            geoms.Add(new FilterDefinition("geom", "{http://a9.com/-/opensearch/extensions/geo/1.0/}geometry",
             "POLYGON((-180 -60,180 -60,180 -90,-180 -90,-180 -60))",
             "Antarctica", null, null
             ));

            return geoms;
        }

        public static IEnumerable<IEnumerable<FilterDefinition>> GetCatalogueCoverageProductTypeFilters(Mission mission)
        {
            List<IEnumerable<FilterDefinition>> filtersDefinition = new List<IEnumerable<FilterDefinition>>();
            switch (mission.Name)
            {
                case "Sentinel-1":
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("RAW"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("GRD"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SLC"),
                        new FilterDefinition("processingLevel", "{http://a9.com/-/opensearch/extensions/eo/1.0/}processingLevel}", "L1", "", null,null)
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("OCN"),
                    });
                    break;
                case "Sentinel-2":
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("S2MSI1C"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("S2MSI2A"),
                    });
                    break;
                case "Sentinel-3":
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("OL_1_EFR___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("OL_1_ERR___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("OL_2_LRR___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("OL_2_LFR___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SL_1_RBT___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SL_2_LST___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SR_1_SRA___"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SR_1_SRA_A_"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SR_1_SRA_BS"),
                    });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SR_2_LAN___"),
                    });
                    // filtersDefinition.Add(new List<FilterDefinition>(){
                    //     new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                    //     mission.ProductTypes.GetFilterDefinition("SR_2_WAT___"),
                    // });
                    filtersDefinition.Add(new List<FilterDefinition>(){
                        new FilterDefinition("missionName", "{http://a9.com/-/opensearch/extensions/eo/1.0/}platform", mission.MissionName.Value, mission.MissionName.Label, mission.MissionName.Validator, null),
                        mission.ProductTypes.GetFilterDefinition("SY_2_SYN___"),
                    });
                    // filtersDefinition.Add(new List<FilterDefinition>(){
                    //     mission.ProductTypes.GetFilterDefinition("SY_2_VGP___"),
                    // });
                    break;
            }
            return filtersDefinition;
        }
    }
}
