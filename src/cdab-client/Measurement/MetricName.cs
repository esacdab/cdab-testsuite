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

namespace cdabtesttools.Measurement
{
    /// <summary>
    /// Enumeration for the metric types measured by the application.
    /// </summary>
    public enum MetricName
    {
        throughput,

        responseTime,

        queryTime,

        startTime,

        endTime,

        peakResponseTime,

        avgResponseTime,

        errorRate,

        avgConcurrency,

        peakConcurrency,

        responseRate,

        avgSize,

        maxSize,

        size,

        maxTotalResults,

        totalResults,

        totalReferenceResults,

        resultsErrorRate,

        wrongResultsCount,

        totalReadResults,

        totalValidatedResults,

        totalWrongResults,

        exception,
        dataCollectionDivision,

        catalogueCoverage,

        avgDataOperationalLatency,

        maxDataOperationalLatency,

        avgDataAvailabilityLatency,

        maxDataAvailabilityLatency,
        analysisTime,
        httpStatusCode,
        url,
        dataCoverage,
        totalOnlineResults,
        dataOfferConsistency,
        retryNumber,
        maxRetryNumber,
        totalSize,
        beginGetResponseTime,
        endGetResponseTime,
        downloadElapsedTime,
        offlineDataAvailabilityLatency,
    }
}
