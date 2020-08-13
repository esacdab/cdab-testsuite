import datetime
from enum import Enum
import json
import re
import subprocess
import sys
import time


ERR_CONFIG = 10
ERR_CREATE = 11
ERR_REMOTE = 12
ERR_DELETE = 13


def exit_client(exit_code, message):
    print("ERROR: {0}".format(message), file=sys.stderr)
    sys.exit(exit_code)



def await_vm_availability(compute_config, connect_retries, connect_interval, run):
    Logger.log(LogLevel.INFO, "Awaiting SSH availability ...", run=run)

    available = False
    max_retries = 3     # max_retries and retry refer to overall retries (each consisting of <connect_retries> SSH connection attempts)
    retry = 0

    while retry < max_retries and not available:
        count = 0       # count refers to single SSH connection attemts (in total there can be <max_retries> * <connect_retries>)
        retry_until_time = datetime.datetime.utcnow() + datetime.timedelta(seconds=connect_retries*connect_interval)

        while not available and count < connect_retries:
            try:
                response = execute_remote_command(compute_config, run, "ls", quiet=True, timeout=10)
                available = True
            except Exception as e:
                if datetime.datetime.utcnow() >= retry_until_time:
                    break
                else:
                    time.sleep(connect_interval)
                    count += 1
                    continue

        if not available:
            retry += 1
            if retry == max_retries:
                Logger.log(LogLevel.ERROR, "Failed to connect to virtual machine", run=run)
            else:
                Logger.log(LogLevel.WARN, "Virtual machine not available, retrying after 30 seconds", run=run)
                time.sleep(30)

    return available



def execute_local_command(run, options, json_output = False, exit_code = None, quiet = False, stdout = subprocess.PIPE, timeout=None):

    if run is None:
        stderr = sys.stderr
    else:
        stderr = run.stderr

    Logger.log(LogLevel.DEBUG, "Command: {0}".format(get_command_str(options)), run=run)

    cp = subprocess.run([ (o[0] if isinstance(o, tuple) else o) for o in options ], stdout=stdout, stderr=subprocess.PIPE, universal_newlines=True, timeout=timeout)

    if cp.returncode != 0:
        if not quiet:
            print("ERROR: Error executing command", file=stderr)
            print("Command:       {0}".format(get_command_str(options)), file=stderr)
            print("Return code:   {0}".format(cp.returncode), file=stderr)
            print("Error message: {0}".format(cp.stderr), file=stderr)

        if exit_code is None:
            raise Exception("Error during command execution: {0}".format(get_command_str(options)))
        else:
            if Logger.verbose:
                Logger.log(LogLevel.DEBUG, cp.stdout)
            Logger.log(LogLevel.ERROR, cp.stderr)
            exit_client(exit_code, "Error during command execution")

    if json_output:
        try:
            response = json.loads(cp.stdout)
            return response
        except:
            raise Exception("Invalid response (not JSON)")

    return cp.stdout




def execute_remote_command(compute_config, run, command, display_command = None, json_output = False, exit_code = None, quiet = False, timeout=None):

    if run is None:
        stderr = sys.stderr
    else:
        stderr = run.stderr

    if display_command:
        command = (command, display_command)

    options = [
        'ssh', '-i', compute_config['private_key_file'],
        "-o", "StrictHostKeyChecking=no", "-o", "UserKnownHostsFile=/dev/null",
        "{0}@{1}".format(compute_config['remote_user'], run.public_ip), command
    ]

    result = execute_local_command(run, options, json_output=json_output, exit_code=exit_code, quiet=quiet, timeout=timeout)

    return result




def copy_file(compute_config, run, local_file, remote_file, to_remote = True, exit_code = None, quiet = False):

    options = [
        "scp", '-i', compute_config['private_key_file'],
        "-o", "StrictHostKeyChecking=no", "-o", "UserKnownHostsFile=/dev/null"
    ]

    remote_url = "{0}@{1}:{2}".format(compute_config['remote_user'], run.public_ip, remote_file)

    if to_remote:
        options.extend([local_file, remote_url])
    else:
        options.extend([remote_url, local_file])

    execute_local_command(run, options, exit_code=exit_code, quiet=quiet)



def get_command_str(options):
    command = ""
    quote_last_arg = len(options) > 1 and options[0] == "ssh"
    last = len(options) - 1
    for i, o in enumerate(options):
        if isinstance(o, tuple):
            if len(o) > 1 and not Logger.show_passwords:
                o = o[1]
            else:
                o = o[0]
        if i == last and quote_last_arg:
            command += "\"{0}\"".format(re.sub('([\"])', r'\\\1', o))
        else:
            command += re.sub('([ \'\"])', r'\\\1', o) + " "

    return command




class Logger:

    def log(level, message, run=None):
        
        if run is None:
            stderr = sys.stderr
        else:
            stderr = run.stderr

        if level.value <= LogLevel.INFO.value or Logger.verbose:
            if run and Logger.mixed_logs:
                run_str = " [{0}]".format(run.short_name)
            else:
                run_str = ""
            level_str = " [{0}]".format(level.name).ljust(9)
            print("{0}{1}{2}{3}".format(
                    datetime.datetime.utcnow().strftime('%Y-%m-%dT%H:%M:%S.%fZ'),
                    run_str,
                    level_str,
                    message
                ),
                file=stderr
            )



class LogLevel(Enum):
    ERROR = 1
    WARN  = 2
    INFO  = 3
    DEBUG = 4




