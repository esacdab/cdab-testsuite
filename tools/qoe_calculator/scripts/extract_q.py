import mysql.connector
import argparse 
import json
from sys import exit

def get_args():
	# Using argparse to obtain the argument

	parser = argparse.ArgumentParser(description="This script extracts from a database\
		the data needed to calculate Quality of Experience indicators 1, 2, 3 and 4\n\
		A configuration file must be provided with the informations needed to \
		connect to a database that contains the metrics stored with the schema \
		provided by the create_cdab_db_stats script.\n\
		Further information can be obtained from the official GitHub repository\
		https://github.com/esa-cdab/cdab-testsuite")

	parser.add_argument(
		"-c", 
		metavar="Config", 
		required=True, 
		help="Specifies \
		the full path to the file containing the database configuration")

	return parser.parse_args()


def open_db(config):
	# Tries to open a connection to the database

	try:
		db = mysql.connector.connect(
		  host=config["host"],
		  user=config["user"],
		  password=config["password"],
		  database=data["database"]
		)
	except mysql.connector.Error as err:
		print("Something went wrong: {}".format(err))
		exit(1)

	return (db.cursor(), db)

def fetch_metric(cursor, metric):
	# standard fetch function, given a metric name retrieves all the values
	# in the database associated with that metric

	cursor.execute(
		"SELECT value FROM Runs WHERE metricId = \
		(SELECT id FROM Metrics WHERE name=%s)", (metric,))
	res = [d[0] for d in cursor.fetchall()]
	return res


def q1(cursor):
	# Fetches metrics needed to calculate Q1 and save them in a json file
	results = {}
	results["M015"] = fetch_metric(cursor, "catalogueCoverage")
	results["M023"] = fetch_metric(cursor, "dataCoverage")
	results["M013"] = fetch_metric(cursor, "avgDataAvailabilityLatency")
	results["M024"] = fetch_metric(cursor, "dataOfferConsistency")

	with open("q1_data.json", "w") as output:
		json.dump(results, output)

def q2(cursor):
	# Fetches metrics needed to calculate Q2 and save them in a json file
	results = {}
	results["M001"] = fetch_metric(cursor, "avgResponseTime")
	results["M002"] = fetch_metric(cursor, "peakResponseTime")
	results["M003"] = fetch_metric(cursor, "errorRate")
	with open("q2_data.json", "w") as output:
		json.dump(results, output)
	

def q3(cursor):
	# Fetches metrics needed to calculate Q3 and save them in a json file
	results = {}
	results["M001"] = fetch_metric(cursor, "avgResponseTime")
	results["M002"] = fetch_metric(cursor, "peakResponseTime")
	results["M003"] = fetch_metric(cursor, "errorRate")
	results["M012"] = fetch_metric(cursor, "resultsErrorRate")
	with open("q3_data.json", "w") as output:
		json.dump(results, output)

def q4(cursor):
	# Fetches metrics needed to calculate Q4 and save them in a json file
	results = {}
	results["M001"] = fetch_metric(cursor, "avgResponseTime")
	results["M002"] = fetch_metric(cursor, "peakResponseTime")
	results["M003"] = fetch_metric(cursor, "errorRate")
	results["M005"] = fetch_metric(cursor, "throughput")
	results["M017"] = fetch_metric(cursor, "offlineDataAvailabilityLatency")
	with open("q4_data.json", "w") as output:
		json.dump(results, output)

def main():
	# Retrieves arguments, opens the config file, extracts the metrics
	args = get_args()
	
	with open(args.c) as f:
		config = json.load(f)

		cursor, db = open_db(config)

		if config["q1"]:
			q1(cursor)
		if config["q2"]:
			q2(cursor)
		if config["q3"]:
			q3(cursor)
		if config["q4"]:
			q4(cursor)	

	cursor.close()
	db.close()

if __name__ == '__main__':
	main()