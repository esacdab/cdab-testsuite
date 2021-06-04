CREATE DATABASE IF NOT EXISTS testcases;

USE testcases;

CREATE TABLE IF NOT EXISTS TestCase (
	id int NOT NULL AUTO_INCREMENT,
	name varchar(5),
	target varchar(10),
	startedAt datetime,
	PRIMARY KEY(id)
);

CREATE TABLE IF NOT EXISTS Metrics (
	id int NOT NULL AUTO_INCREMENT,
	name varchar(50),
	PRIMARY KEY(id)
);

CREATE TABLE IF NOT EXISTS Runs (
	id int NOT NULL AUTO_INCREMENT,
	metricId int NOT NULL,
	testCaseId int,
	value float,
	FOREIGN KEY(metricId) REFERENCES Metrics(id),
	FOREIGN KEY(testcaseId) REFERENCES TestCase(id),
	PRIMARY KEY(id)
);

INSERT IGNORE INTO Metrics (name) VALUES
("avgResponseTime"),
("peakResponseTime"),
("errorRate"),
("avgConcurrency"),
("throughput"),
("peakConcurrency"),
("avgSize"),
("maxSize"),
("totalReadResults"),
("maxTotalResults"),
("resultsErrorRate"),
("avgDataAvailabilityLatency"),
("avgDataOperationalLatency"),
("catalogueCoverage"),
("offlineDataAvailabilityLatency"),
("maxDataOperationalLatency"),
("maxDataAvailabilityLatency"),
("totalValidatedResults"),
("totalWrongResults"),
("totalReferenceResults"),
("dataCoverage"),
("dataOfferConsistency"),
("totalResults"),
("totalSize"),
("avgProvisioningLatency");
