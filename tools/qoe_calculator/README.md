# QoE calculator plugin
The following plugin is responsible for the whole process of parsing the results of the [cdab_testsuite](https://github.com/esa-cdab/cdab-testsuite), creating a database to store them, upload and produce the metrics necessary to calculate Quality of Experience indicators Q1, Q2, Q3 and Q4. 


## Usage
To use this toolchain clone the repository in a local folder, inside the `config` folder there are examples of the 3 configuration files needed, modify them with the thresholds and weights needed and provide the correct host and password for the mySQL database in the `config.json` file.
Execute the first script to create the database 
```
mysql -u root -p < create_cdab_db_stats.sql
```

Then execute the parsing 

```
python3 import_cdab_data.py -i folder/with/results/ -c pathTo/config.json

```

Now that the database is filled with all the metrics needed you can extract them with the appropriate script

```
python3 exctract_q.py -c pathTo/config.json
```

And finally you can produce the QoE 

```
python3 calculate_q.py -t Thresholds -w Weights [-q1 QoE 1] [-q2 QoE 2] [-q3 QoE 3] [-q4 QoE 4]
``` 

# Scripts
## create_cdab_db_stats.sql
The first script that has to be executed, it creates a database to contain the metrics, the following tables are used

### TestCase
| PK(id)  | name    | target  | startedAt |
|-----|---------|---------|-----------|
| int | varchar | varchar | datetime  |

This table is used to store informations about the testcase executed, the primary key is an auto increment id allowing multiple copies of the same test case to be inserted.

### Metrics

| PK(id)  | name    |
|-----|---------|
| int | varchar |

A static table filled during the execution of the script, stores all the possible raw metrics assigning one unique id to each of them

### Runs

| PK(id)  | metricId | testCaseId | value |
|-----|----------|------------|-------|
| int | int      | int        | float |

This is the core table in the database, for each metric produced in a single testCase a row is added with the value, the metricId and the testCaseId. Having a single table for all the results keeps the parsing procedure fast and easy.

## import_cdab_data.py
This python script is responsible for parsing the results and updating the database. When executed it takes two arguments:
* -i, should be the path to the folder containing all the jsons produced by the benchmarking software.
* -c, the path to the `config.json` file with the informations necessary to open a connection with the database.


## exctract_q.py
This python script queries the database in order to obtain the values necessary to produce the QoEs, it takes the following argument
* -c, the path to the `config.json` file where is specified which QoE's data should be extracted.

## cdab_qoe_comp.py
The final script in the process, given a data file as input it calculates the corresponding QoE. The arguments it takes are the following:
* Required
    * -t, path to the `thresholds.json` file that contains the thresholds for the various metrics
    * -w, path to the `weights.json` file that contains the weights that will be used to calculate the QoE
* Optional
    * -q1, path to the `q1_data.json` file produced by `exctract_q.py`, if specified Q1 will be calculated.
    * -q2, path to the `q2_data.json` file produced by `exctract_q.py`, if specified Q2 will be calculated.
    * -q3, path to the `q3_data.json` file produced by `exctract_q.py`, if specified Q3 will be calculated.
    * -q4, path to the `q4_data.json` file produced by `exctract_q.py`, if specified Q4 will be calculated.

