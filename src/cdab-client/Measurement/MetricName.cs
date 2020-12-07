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