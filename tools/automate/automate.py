import docker
import os
import tarfile
import json
from sys import exit

class MissingConfigurationException(Exception):
    """
    Custom exception used to notify a missing configuration file
    """
    pass

def pull_image(client):
    """
    Verify if the image has already been pulled, do it otherwise
    """
    if "<Image: 'esacdab/testsuite:latest'>" not in client.images.list():
        client.images.pull("esacdab/testsuite:latest")


def copy_to(client, src, dst):
    """
    Helper function, takes a source file and a destination inside a container and copies the file in the container
    """
    name, dst = dst.split(':')
    container = client.containers.get(name)

    os.chdir(os.path.dirname(src))
    srcname = os.path.basename(src)
    tar = tarfile.open('temp.tar', mode='w')
    try:
        tar.add(srcname)
    finally:
        tar.close()

    data = open('temp.tar', 'rb').read()
    container.put_archive(os.path.dirname(dst), data)
    os.remove("temp.tar")


def copy_from(container, targetsite, scenario, execution_number):
    """
    Helper function, copies a Result file from the container
    """
    filename = targetsite + "_" + scenario + "_" + str(execution_number + 1) + ".tar"
    out = open(filename, "wb")
    bits, stat = container.get_archive(scenario + "Results.json")
    for chunk in bits:
        out.write(chunk)
    out.close()

def run_scenario(container, containername, targetsite, scenario, execution_number):
    """
    Run a scenario on a target site, copies the result file in the current directory and deletes it from the container
    """
    exit_code, _ = container.exec_run("cdab-client -v -tsn={} -tn={} {}".format(containername, targetsite, scenario))
    
    if exit_code != 0:
        print("An error occurred while running TestScenario {} on target site {}".format(scenario, targetsite))
    else:
        print("Copying results")
        copy_from(container, targetsite, scenario, execution_number)
        container.exec_run("rm {}Results.json".format(scenario))

def load_config(client, data):
    if data["config"]:
        copy_to(client, data["config"], "{}:/config.yaml".format(data["containername"]))
    else:
        cwd = os.getcwd()
        try:
             f = open("{}/config.yaml".format(cwd))
            # Do something with the file
        except FileNotFoundError:
            raise MissingConfigurationException

        copy_to(client, "{}/config.yaml".format(cwd), "{}:/config.yaml".format(data["containername"]))


def main():

    with open("config.json") as f:
        data = json.load(f)

    containername = data["containername"]

    try:
        client = docker.from_env()
    except docker.errors.DockerException:
        print("Make sure the docker daemon is running")
        exit(0)

    pull_image(client)

    container = client.containers.run('esacdab/testsuite:latest', detach=True, name=containername)

    try:
        load_config(client, data)
    except MissingConfigurationException:
        print("Missing config.yaml file, please provide a valid path in the configuration file")
        container.stop()
        container.remove()
        exit(0)

    for execution_number in range(data["repetitions"]):
        print("Starting execution number {}".format(str(execution_number+1)))
        for targetsite in data["targets"]:
            for scenario in data["scenarios"]:
                print("Running scenario {} on targetsite {}".format(scenario, targetsite))
                run_scenario(container, containername, targetsite, scenario, execution_number)
                print("Completed")

    container.stop()
    container.remove()

if __name__ == '__main__':
    main()
