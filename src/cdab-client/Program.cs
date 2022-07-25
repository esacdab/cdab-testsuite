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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;
using cdabtesttools.Config;
using cdabtesttools.Data;
using cdabtesttools.Measurement;
using cdabtesttools.Target;
using cdabtesttools.TestCases;
using cdabtesttools.TestScenarios;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Mono.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;


namespace cdabtesttools
{
    class MainClass
    {
        private static ILog log = LogManager.GetLogger(typeof(MainClass));
        static int verbosity;
        static List<string> scenarios = new List<string>() { "TS01" };
        static string testsite_name = "Test";
        static string target_name = "SciHub";
        static string target_endpoint;
        static string target_auth;
        static List<IScenario> scenarios_handlers;
        static TaskScheduler test_scheduler;
        static int load_factor = 2;
        static ServicePoint target_service_point;
        static string groupid;

        static TargetSiteWrapper targetSiteWrapper;

        static FileInfo configFile = new FileInfo("config.yaml");


        public static void Main(string[] args)
        {
            // Fix for Newtonsoft.Json vulnerability issue: https://github.com/advisories/GHSA-5crp-9r3c-p9vr
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings { MaxDepth = 128 };

            bool show_help = false;

            var p = new OptionSet() {
                { "conf=", "YAML file containing the configuration.",
                    v => configFile = new FileInfo(v)
                },
                { "tu|target_url=", "target endpoint URL (default https://scihub.copernicus.eu/apihub). Overrides configuration file.",
                    v => target_endpoint = v
                },
                { "tc|target_credentials=", "the target credentials string (e.g. username:password). Overrides configuration file.",
                    v => target_auth = v
                },
                { "tn|target_name=", "the target identifier string. Mandatory to use the target site configuration from file.",
                    v => target_name = v
                },
                { "tsn|testsite_name=", "the test site identifier. Mandatory to use the test site configuration from file.",
                    v => testsite_name = v
                },
                { "gid|group_id=", "Test group ID used to identify concurrent tests. If not provided a uuid is generated.",
                    v => groupid = v
                },
                { "lf|load_factor=", "Load Factor. Mainly used as a constant to calculate the number of run to make per test cases",
                    v => load_factor = int.Parse(v)
                },
                { "sp|service_provider=", "Service Provider",
                    v => {}
                },
                { "vm|virtual_machine=", "Virtual Machine",
                    v => {}
                },
                { "i", "Container",
                    v => {}
                },
                { "v", "increase debug message verbosity.",
                    v => {
                        if (v != null)
                            ++verbosity;
                    }
                },
                { "h|help",  "show this message and exit.",
                    v => show_help = v != null
                },

            };


            try
            {
                var scenarios_strs = p.Parse(args);
                if (scenarios_strs.Count() > 0)
                    scenarios = scenarios_strs;
            }
            catch (OptionException e)
            {
                log.WarnFormat("cdab-client: ");
                log.WarnFormat(e.Message);
                log.WarnFormat("Try `cdab-client --help' for more information.");
                return;
            }

            if (show_help)
            {
                ShowHelp(p);
                return;
            }

            log = ConfigureLog();

            ValidateOptions();

            InitScheduler();

            try
            {
                if (scenarios_handlers.Count >= 1)
                    ExecuteScenarios();
                else
                    log.WarnFormat("No compatible scenarios to be executed. Exiting.");
            }
            catch (ThreadAbortException)
            {
                log.WarnFormat("Caught ThreadAbortException");
            }
            finally
            {
                Console.WriteLine("Bye");
            }

        }

        private static ILog ConfigureLog()
        {
            Hierarchy hierarchy = (Hierarchy)LogManager.GetRepository(Assembly.GetEntryAssembly());
            hierarchy.Root.RemoveAllAppenders();

            PatternLayout patternLayout = new PatternLayout();
            patternLayout.ConversionPattern = "%date [%thread] %-5level %logger - %message%newline";
            patternLayout.ActivateOptions();

            ConsoleAppender consoleErrAppender = new ConsoleAppender();
            consoleErrAppender.Layout = patternLayout;
            consoleErrAppender.ActivateOptions();
            consoleErrAppender.Target = "Console.Error";
            log4net.Filter.LevelRangeFilter errfilter = new log4net.Filter.LevelRangeFilter();
            errfilter.LevelMin = Level.Verbose;
            errfilter.LevelMax = Level.Emergency;
            consoleErrAppender.AddFilter(errfilter);
            hierarchy.Root.AddAppender(consoleErrAppender);

            hierarchy.Root.Level = Level.Info;
            if (verbosity >= 1)
            {
                hierarchy.Root.Level = Level.Debug;
            }
            hierarchy.Configured = true;

            BasicConfigurator.Configure(hierarchy, new ConsoleAppender[] { consoleErrAppender });

            return LogManager.GetLogger(typeof(MainClass));
        }

        private static void ValidateOptions()
        {
            log.InfoFormat("===============================");

            log.InfoFormat("Validating Test Options...");

            // 1. Check configuration
            log.InfoFormat("[1] Loading & Checking configuration");
            LoadAndCheckConfiguration();

            // 2. Check endpoint
            log.InfoFormat("[2] Configuring target {0} ({1})", target_name, Configuration.Current.ServiceProviders[target_name].Data.Url);
            targetSiteWrapper = ConfigureTarget(Configuration.Current.ServiceProviders[target_name]);

            // 3.Check Scenarios Compatibility
            log.InfoFormat("[3] Check scenarios compatibility...");
            CheckScenariosCompatibility(targetSiteWrapper);

            log.InfoFormat("Test Options VALIDATED");

            log.InfoFormat("===============================");
        }

        private static void LoadAndCheckConfiguration()
        {
            if (string.IsNullOrEmpty(target_name))
                throw new ArgumentNullException("Target Name", "Target Name cannot be null or empty");

            Configuration.Current = Configuration.Load(configFile);

            if (Configuration.Current == null)
                Configuration.Current = new Config.Configuration();
            else
            {
                log.DebugFormat("Configuration found in {0}", configFile.FullName);
            }

            // Create or find existing target by name
            TargetSiteConfiguration _targetSite = new TargetSiteConfiguration();
            if (Configuration.Current.ServiceProviders.ContainsKey(target_name))
            {
                _targetSite = Configuration.Current.ServiceProviders[target_name];
            }
            else
            {
                Configuration.Current.ServiceProviders.Add(target_name, _targetSite);
            }

            // Overrides target endpoint url if set by arg
            if (!string.IsNullOrEmpty(target_endpoint))
                _targetSite.Data.Url = target_endpoint;

            // Overrides target credentialsif set by arg
            if (!string.IsNullOrEmpty(target_auth))
                _targetSite.Data.Credentials = target_auth;

        }

        private static TargetSiteWrapper ConfigureTarget(TargetSiteConfiguration targetSiteConfig)
        {
            bool enableDirectDataAccess = false;
            foreach (string scenario in scenarios) {
                if (scenario.StartsWith("TS1")) enableDirectDataAccess = true;
            }
            TargetSiteWrapper targetSiteWrapper = new TargetSiteWrapper(target_name, targetSiteConfig, enableDirectDataAccess);
            if (enableDirectDataAccess)
            {
                log.Info("Scenario is executed on target instance, internal target-specific data access will be used if possible");
            }
            else
            {
                log.Info("Scenario is executed on an instance outside the target, external target-specific data access will be used if possible");
            }
            return targetSiteWrapper;
        }



        private static void CheckScenariosCompatibility(TargetSiteWrapper target)
        {
            scenarios_handlers = new List<IScenario>();

            foreach (var scenario in scenarios)
            {
                switch (scenario)
                {
                    case "TS01":
                        if (TestScenario01.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario01(log, target, load_factor));
                        else
                            log.WarnFormat("TS01 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS02":
                        if (TestScenario02.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario02(log, target, load_factor));
                        else
                            log.WarnFormat("TS02 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS03":
                        if (TestScenario03.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario03(log, target, load_factor));
                        else
                            log.WarnFormat("TS03 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS04":
                        if (TestScenario04.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario04(log, target, load_factor));
                        else
                            log.WarnFormat("TS04 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS05":
                        if (TestScenario05.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario05(log, target, load_factor));
                        else
                            log.WarnFormat("TS05 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS06":
                        if (TestScenario06.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario06(log, target, load_factor));
                        else
                            log.WarnFormat("TS06 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS07":
                        if (TestScenario07.CheckCompatibility(target))
                        {
                            scenarios_handlers.Add(new TestScenario07(log, target, load_factor));
                        }
                        else
                            log.WarnFormat("TS07 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS11":
                        if (TestScenario11.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario11(log, target, load_factor));
                        else
                            log.WarnFormat("TS11 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    case "TS12":
                        if (TestScenario12.CheckCompatibility(target))
                            scenarios_handlers.Add(new TestScenario12(log, target, load_factor));
                        else
                            log.WarnFormat("TS12 is not compatible with target {0}. Skipping", target.Label);
                        break;
                    default:
                        log.WarnFormat("Unknown scenario '{0}'. Skipping", scenario);
                        break;
                }
            }

            log.InfoFormat("{0} Test Scenarios : {1}", scenarios_handlers.Count(), string.Join(",", scenarios_handlers.Select(s => s.Id)));
        }

        private static void InitScheduler()
        {
            int minWorker, minIOC, maxWorker, maxIOC, curWorker, curIOC;
            ThreadPool.GetMinThreads(out minWorker, out minIOC);
            ThreadPool.GetMaxThreads(out maxWorker, out maxIOC);
            ThreadPool.GetMaxThreads(out curWorker, out curIOC);
            log.InfoFormat("Setting ThreadPool Min/Max : {0}/{1}", minWorker, maxWorker);
            ThreadPool.SetMinThreads(minWorker * 2, minIOC);
            ThreadPool.SetMaxThreads(maxWorker * 10, maxIOC);

            test_scheduler = new MaxParallelismTaskScheduler(targetSiteWrapper.TargetSiteConfig.MaxCatalogueThread * 5);

            target_service_point = ServicePointManager.FindServicePoint(targetSiteWrapper.Wrapper.Settings.ServiceUrl);
            target_service_point.ConnectionLimit = targetSiteWrapper.TargetSiteConfig.MaxCatalogueThread + targetSiteWrapper.TargetSiteConfig.MaxDownloadThread;
        }

        private static void ExecuteScenarios()
        {
            log.InfoFormat("********************************");
            log.InfoFormat("*   EXECUTING TEST SCENARIOS   *");
            log.InfoFormat("********************************");

            LinkedList<TestCase> _allTestCases = new LinkedList<TestCase>();
            List<Task> _allTestUnits = new List<Task>();
            TaskFactory testFactory = new TaskFactory(test_scheduler);
            Task _lastTask = null;

            foreach (var scenario in scenarios_handlers)
            {
                Task _scenarioStarter = new Task(ScenarioStarter);
                Task _previousTask = _scenarioStarter;
                Dictionary<TestCase, Task<TestCaseResult>> _testCaseResults = new Dictionary<TestCase, Task<TestCaseResult>>();
                _lastTask = _scenarioStarter;

                log.InfoFormat("Creating Test Cases for Test Scenario [{0}] {1}", scenario.Id, scenario.Title);

                IEnumerable<TestCase> _scenarioTestCases = scenario.CreateTestCases();

                log.InfoFormat("{0} Test Case(s) for Test Scenario [{1}]", _scenarioTestCases.Count(), scenario.Id);

                foreach (var _testCase in _scenarioTestCases)
                {
                    log.InfoFormat("Queuing Tasks for Test Case [{0}] {1}", _testCase.Id, _testCase.Title);
                    Task _testCasePreparationTask = _previousTask.ContinueWith(task => _testCase.PrepareTest(), TaskContinuationOptions.ExecuteSynchronously);

                    Task<IEnumerable<TestUnitResult>> _testUnits = _testCasePreparationTask.ContinueWith(task => _testCase.RunTestUnits(testFactory, task), TaskContinuationOptions.ExecuteSynchronously);

                    Task<TestCaseResult> _testCaseCompletionTask = _testUnits.ContinueWith<TestCaseResult>(task => _testCase.CompleteTest(task), TaskContinuationOptions.ExecuteSynchronously);
                    _testCaseResults.Add(_testCase, _testCaseCompletionTask);
                    _previousTask = _testCaseCompletionTask;
                    _lastTask = _testCaseCompletionTask;
                }
                _scenarioStarter.Start();

                WriteTestScenarioResults(scenario, _testCaseResults);
            }

            try
            {
                _lastTask.Wait();
            }
            catch (Exception e)
            {
                log.ErrorFormat("Test Execution Error : {0}", e.InnerException.Message);
                log.Debug(e.InnerException.StackTrace);
                while (e.InnerException.InnerException != null)
                {
                    log.Debug(e.InnerException.InnerException.StackTrace);
                    e = e.InnerException;
                }
            }

            log.InfoFormat("********************************");
            log.InfoFormat("*   COMPLETED TEST SCENARIOS   *");
            log.InfoFormat("********************************");
        }



        static void ScenarioStarter()
        {
            log.Info("Tests Ignition ...");
            for (int i = 3; i >= 0; i--)
            {
                log.Info(i);
                Thread.Sleep(1000);
            }
            log.Info("Tests FIRED !!!");
        }

        private static void WriteTestScenarioResults(IScenario scenario, Dictionary<TestCase, Task<TestCaseResult>> testCaseResults)
        {
            TestScenarioResult _scenarioResult = new TestScenarioResult();
            _scenarioResult.JobName = Environment.GetEnvironmentVariable("JOB_NAME");
            _scenarioResult.BuildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER");
            _scenarioResult.TestScenario = scenario.Id;
            _scenarioResult.TestSite = testsite_name;
            _scenarioResult.TestTargetUrl = Configuration.Current.ServiceProviders[target_name].Data.Url;
            _scenarioResult.TestTarget = target_name;
            _scenarioResult.ZoneOffset = DateTime.Now.ToString("zz");
            try // fails on macOS if file sharing is disabled (macOS can't resolve its own hostname then)
            {
                _scenarioResult.HostName = Dns.GetHostName();
                _scenarioResult.HostAddress = Dns.GetHostAddresses(_scenarioResult.HostName)[0].ToString();
            }
            catch (Exception)
            {
                _scenarioResult.HostName = "localhost";
                _scenarioResult.HostAddress = "127.0.0.1";
            }
            _scenarioResult.TestCaseResults = new TestCaseResult[testCaseResults.Count];

            Schemas.testsuites _testSuites = new Schemas.testsuites();
            _testSuites.testsuite = new Schemas.testsuite[1];
            _testSuites.testsuite[0] = new Schemas.testsuite();
            _testSuites.testsuite[0].id = scenario.Id;
            _testSuites.testsuite[0].name = scenario.Title;
            _testSuites.testsuite[0].testcase = new Schemas.testcase[testCaseResults.Count];

            int _testCasesErrors = 0;

            for (int i = 0; i < testCaseResults.Count; i++)
            {
                try
                {
                    _scenarioResult.TestCaseResults[i] = testCaseResults.ElementAt(i).Value.Result;
                    _testSuites.testsuite[0].testcase[i] = new Schemas.testcase();
                    _testSuites.testsuite[0].testcase[i].name = _scenarioResult.TestCaseResults[i].TestName;
                    _testSuites.testsuite[0].testcase[i].status = "OK";
                    _testSuites.testsuite[0].testcase[i].classname = _scenarioResult.TestCaseResults[i].ClassName;
                }
                catch (AggregateException e)
                {
                    log.WarnFormat("Error reading Test Case {0} results: {1}", testCaseResults.ElementAt(i).Key.Id, e.InnerException.Message);
                    log.Debug(e.InnerException.StackTrace);
                    _testCasesErrors++;
                    _scenarioResult.TestCaseResults[i] = new TestCaseResult(testCaseResults.ElementAt(i).Key.Id, new List<IMetric>(), testCaseResults.ElementAt(i).Key.StartTime, testCaseResults.ElementAt(i).Key.EndTime);
                    _testSuites.testsuite[0].testcase[i] = new Schemas.testcase();
                    _testSuites.testsuite[0].testcase[i].name = testCaseResults.ElementAt(i).Key.Title;
                    _testSuites.testsuite[0].testcase[i].classname = testCaseResults.ElementAt(i).Key.GetType().ToString();
                    _testSuites.testsuite[0].testcase[i].status = "ERROR";
                    _testSuites.testsuite[0].testcase[i].error = new Schemas.error[1];
                    _testSuites.testsuite[0].testcase[i].error[0] = new Schemas.error();
                    _testSuites.testsuite[0].testcase[i].error[0].message = e.Message;
                    _testSuites.testsuite[0].testcase[i].error[0].type = e.GetType().ToString();
                    _testSuites.testsuite[0].testcase[i].classname = testCaseResults.ElementAt(i).Key.GetType().ToString();
                }
            }

            _testSuites.testsuite[0].errors = _testCasesErrors.ToString();

            File.WriteAllText(string.Format("{0}Results.json", scenario.Id), JsonConvert.SerializeObject(_scenarioResult, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }));

            XmlSerializer ser = new XmlSerializer(typeof(Schemas.testsuites));

            FileStream fs = new FileStream(string.Format("junit.xml"), FileMode.Create, FileAccess.Write);
            ser.Serialize(fs, _testSuites);
            fs.Close();
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: cdab-test [TEST SCENARIOS]+");
            Console.WriteLine("Launch one or more test scenarios to a target site");
            Console.WriteLine("If no test scenario is specified, the generic TS01 is executed.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}
