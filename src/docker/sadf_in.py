import sys, json; 

def find(key, dictionary):
    if isinstance(dictionary, dict) or isinstance(dictionary, list):
#        if key in dictionary:
#            yield key
        for k, v in dictionary.iteritems():
            if k == key:
                yield v
            elif isinstance(v, dict):
                for result in find(key, v):
                    yield result
            elif isinstance(v, list):
                for d in v:
                    for result in find(key, d):
                        yield result
    else:
        if key == dictionary:
            yield key

def main():
    ts_result = json.load(sys.stdin);
    min_startTime = min(list(find('startedAt', ts_result)))
    max_endTime = max(list(find('endedAt', ts_result)))
    sum_duration = sum(list(find('duration', ts_result)))
    print min_startTime, max_endTime, sum_duration
        
if __name__ == '__main__':
    main()
