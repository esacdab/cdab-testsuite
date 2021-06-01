import argparse 
import mysql.connector
import json

def get_args():
	# Using argparse to obtain the arguments, thresholds and weights are required,
	# everything else is optional

	parser = argparse.ArgumentParser(description="This script takes data files produced\
		by the extract_q script and calculates the appropriate QoE.\
		Further information can be obtained from the official GitHub repository \
		https://github.com/esa-cdab/cdab-testsuite")

	parser.add_argument(
		"-t",
		metavar="Thresholds",
		required=True,
		help="Path of the file containing thresholds \
		information"
		)
	
	parser.add_argument(
		"-w",
		metavar="Weights",
		required=True,
		help="Path of the file containing weights \
		information"
		)

	parser.add_argument(
		"-q1",
		metavar="QoE 1",
		help="Path of the file containing the data to calculate q1"
		)

	parser.add_argument(
		"-q2",
		metavar="QoE 2",
		help="Path of the file containing the data to calculate q2"
		)

	parser.add_argument(
		"-q3",
		metavar="QoE 3",
		help="Path of the file containing the data to calculate q3"
		)

	parser.add_argument(
		"-q4",
		metavar="QoE 4",
		help="Path of the file containing the data to calculate q4"
		)

	parser.add_argument("n")
	return parser.parse_args()

def calculate_apdex(metric, satisf_tresh, tolerating_tresh):
	# Given a metric and two thresholds calculates the appropriate apdex
	if len(metric) == 0:
		return 0

	total = 0
	if satisf_tresh > tolerating_tresh:
		for val in metric:
			if val >= satisf_tresh:
				total += 1
			elif val >= tolerating_tresh and val < satisf_tresh:
				total += 0.5
	else:
		for val in metric:
			if val <= satisf_tresh:
				total += 1
			elif val <= tolerating_tresh and val > satisf_tresh:
				total += 0.5

	return total/len(metric)


def q1(q1_data, thresholds, weights):
	# Calculate Q1, since M015 and M023 should be calculated just once the first element
	# of the array is considered
	with open(q1_data) as f:
		data = json.load(f)

	apdexm13 = calculate_apdex(data["M013"], thresholds["M013"]["satisfied"], thresholds["M013"]["frustrated"])
	apdexm24 = calculate_apdex(data["M024"], thresholds["M024"]["satisfied"], thresholds["M024"]["frustrated"])
	
	result = {}
	result["APDEXM013"] = apdexm13
	result["APDEXM024"] = apdexm24
	result["QoE"] = float(weights["APDEXM013"])*apdexm13 + float(weights["APDEXM024"])*apdexm24
	if len(data["M015"]) > 0:
	 result["QoE"] += data["M015"][0]*float(weights["M015"]) 
	if len(data["M023"]) > 0:
	 result["QoE"] += data["M023"][0]*float(weights["M023"])

	with open("q1.json", "w") as output:
		json.dump(result, output)


def q2(q2_data, thresholds, weights, samples=0):
	with open(q2_data) as f:
		data = json.load(f)

	apdexm1 = calculate_apdex(data["M001"], thresholds["M001"]["satisfied"], thresholds["M001"]["frustrated"])
	apdexm2 = calculate_apdex(data["M002"], thresholds["M002"]["satisfied"], thresholds["M002"]["frustrated"])
	apdexm3 = calculate_apdex(data["M003"], thresholds["M003"]["satisfied"], thresholds["M003"]["frustrated"])

	result = {}
	result["APDEXM001"] = apdexm1
	result["APDEXM002"] = apdexm2
	result["APDEXM003"] = apdexm3
	result["QoE"] = weights["APDEXM001"]*apdexm1 + weights["APDEXM002"]*apdexm2 + weights["APDEXM003"]*apdexm3 

	with open("q2_{}.json".format(samples), "w") as output:
		json.dump(result, output)

def q3(q3_data, thresholds, weights):
	with open(q3_data) as f:
		data = json.load(f)

	apdexm1 = calculate_apdex(data["M001"], thresholds["M001"]["satisfied"], thresholds["M001"]["frustrated"])
	apdexm2 = calculate_apdex(data["M002"], thresholds["M002"]["satisfied"], thresholds["M002"]["frustrated"])
	apdexm3 = calculate_apdex(data["M003"], thresholds["M003"]["satisfied"], thresholds["M003"]["frustrated"])
	apdexm12 = calculate_apdex(data["M012"], thresholds["M012"]["satisfied"], thresholds["M012"]["frustrated"])


	result = {}
	result["APDEXM001"] = apdexm1
	result["APDEXM002"] = apdexm2
	result["APDEXM003"] = apdexm3
	result["APDEXM012"] = apdexm12
	result["QoE"] = weights["APDEXM001"]*apdexm1 + weights["APDEXM002"]*apdexm2 + weights["APDEXM003"]*apdexm3 + weights["APDEXM012"]*apdexm12

	with open("q3.json", "w") as output:
		json.dump(result, output)


def q4(q4_data, thresholds, weights):
	with open(q4_data) as f:
		data = json.load(f)

	apdexm1 = calculate_apdex(data["M001"], thresholds["M001"]["satisfied"], thresholds["M001"]["frustrated"])
	apdexm2 = calculate_apdex(data["M002"], thresholds["M002"]["satisfied"], thresholds["M002"]["frustrated"])
	apdexm3 = calculate_apdex(data["M003"], thresholds["M003"]["satisfied"], thresholds["M003"]["frustrated"])
	apdexm5 = calculate_apdex(data["M005"], thresholds["M005"]["satisfied"], thresholds["M005"]["frustrated"])
	apdexm17 = calculate_apdex(data["M017"], thresholds["M017"]["satisfied"], thresholds["M017"]["frustrated"])


	result = {}
	result["APDEXM001"] = apdexm1
	result["APDEXM002"] = apdexm2
	result["APDEXM003"] = apdexm3
	result["APDEXM005"] = apdexm5
	result["APDEXM017"] = apdexm17
	result["QoE"] = weights["APDEXM001"]*apdexm1 + weights["APDEXM002"]*apdexm2 + weights["APDEXM003"]*apdexm3 + weights["APDEXM005"]*apdexm5 + weights["APDEXM017"]*apdexm17

	with open("q4.json", "w") as output:
		json.dump(result, output)


def main():
	# Retrieves the arguments, opens the thresholds and weights files, calculates
	# the QoEs
	args = get_args()
	
	with open(args.w) as f:
		weights = json.load(f)

	with open(args.t) as f:
		thresholds = json.load(f)

	if args.q1 is not None:
		q1(args.q1, thresholds, weights["Q1"])
	if args.q2 is not None:
		q2(args.q2, thresholds, weights["Q2"], args.n)
	if args.q3 is not None:
		q3(args.q3, thresholds, weights["Q3"])
	if args.q4 is not None:
		q4(args.q4, thresholds, weights["Q4"])


if __name__ == '__main__':
	main()