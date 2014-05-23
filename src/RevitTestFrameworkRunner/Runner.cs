using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Threading;
using System.Xml.Serialization;
using Autodesk.RevitAddIns;
using Dynamo.NUnit.Tests;
using Dynamo.Tests;
using Microsoft.Practices.Prism.ViewModel;
using NDesk.Options;
using NUnit.Framework;
using RevitTestFrameworkRunner.Properties;

namespace RevitTestFrameworkRunner
{
    public class Runner : NotificationObject
    {
        #region events

        public static event EventHandler TestRunsComplete;

        #endregion

        #region private members

        private string _testAssembly;
        private string _test;
        private string _fixture;
        private bool _isDebug;
        private string _results;
        private const string _pluginGuid = "487f9ff0-5b34-4e7e-97bf-70fbff69194f";
        private const string _pluginClass = "Dynamo.Tests.RevitTestFramework";
        private string _workingDirectory;
        private bool _gui = true;
        private string _revitPath;
        private List<string> _journalPaths = new List<string>();
        private int _runCount = 0;
        private int _timeout = 120000;
        private bool _concat;
        private string _addinPath;
        private string _assemblyPath;
        private ObservableCollection<IAssemblyData> _assemblies = new ObservableCollection<IAssemblyData>();
        private ObservableCollection<RevitProduct> _products = new ObservableCollection<RevitProduct>();

        #endregion

        #region properties

        public ObservableCollection<IAssemblyData> Assemblies
        {
            get { return _assemblies; }
            set
            {
                _assemblies = value;
                RaisePropertyChanged("Assemblies");
            }
        }

        public ObservableCollection<RevitProduct> Products
        {
            get { return _products; }
            set
            {
                _products = value;
                RaisePropertyChanged("Products");
            }
        }

        public string Test
        {
            get { return _test; }
            set { _test = value; }
        }

        public string TestAssembly
        {
            get { return _testAssembly; }
            set { _testAssembly = value; }
        }

        public string Fixture
        {
            get { return _fixture; }
            set { _fixture = value; }
        }

        public bool IsDebug
        {
            get { return _isDebug; }
            set { _isDebug = value; }
        }

        public string Results
        {
            get { return _results; }
            set { _results = value; }
        }

        public static string PluginGuid
        {
            get { return _pluginGuid; }
        }

        public static string PluginClass
        {
            get { return _pluginClass; }
        }

        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set
            {
                _workingDirectory = value;
                AddinPath = Path.Combine(WorkingDirectory, "RevitTestFramework.addin");
                
            }
        }

        public bool Gui
        {
            get { return _gui; }
            set { _gui = value; }
        }

        public string RevitPath
        {
            get { return _revitPath; }
            set { _revitPath = value; }
        }

        public List<string> JournalPaths
        {
            get { return _journalPaths; }
            set { _journalPaths = value; }
        }

        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        public bool Concat
        {
            get { return _concat; }
            set { _concat = value; }
        }

        public string AddinPath
        {
            get { return _addinPath; }
            set { _addinPath = value; }
        }

        public string AssemblyPath
        {
            get { return _assemblyPath; }
            set { _assemblyPath = value; }
        }

        public int RunCount
        {
            get { return _runCount; }
            set { _runCount = value; }
        }

        #endregion

        #region constructors

        public Runner()
        {
            AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "RevitTestFramework.dll");
        }

        #endregion

        #region private methods

        internal bool ParseArguments(IEnumerable<string> args)
        {
            var showHelp = false;

            var p = new OptionSet()
            {
                {"dir:","The path to the working directory.", v=> WorkingDirectory = Path.GetFullPath(v)},
                {"a:|assembly:", "The path to the test assembly.", v => TestAssembly = Path.GetFullPath(v)},
                {"r:|results:", "The path to the results file.", v=>Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of the test fixture.", v => Fixture = v},
                {"t:|testName:", "The name of a test to run", v => Test = v},
                {"c:|concatenate:", "Concatenate results with existing results file.", v=> Concat = v != null},
                {"gui:", "Show the revit test runner gui.", v=>Gui = v != null},
                {"d|debug", "Run in debug mode.", v=>IsDebug = v != null},
                {"h|help", "Show this message and exit.", v=> showHelp = v != null}
            };

            var notParsed = new List<string>();

            const string helpMessage = "Try 'DynamoTestFrameworkRunner --help' for more information.";

            try
            {
                notParsed = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(helpMessage);
                return false;
            }

            if (notParsed.Count > 0)
            {
                Console.WriteLine(String.Join(" ", notParsed.ToArray()));
                return false;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return false;
            }

            if (!String.IsNullOrEmpty(TestAssembly) && !File.Exists(TestAssembly))
            {
                Console.Write("The specified test assembly does not exist.");
                return false;
            }

            if (!String.IsNullOrEmpty(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
            {
                Console.Write("The specified working directory does not exist.");
                return false;
            }

            return true;
        }

        private void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: DynamoTestFrameworkRunner [OPTIONS]");
            Console.WriteLine("Run a test or a fixture of tests from an assembly.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private void OnTestRunsComplete()
        {
            if (TestRunsComplete != null)
            {
                TestRunsComplete(null, EventArgs.Empty);
            }
        }

        internal bool ReadAssembly(string assemblyPath, IList<IAssemblyData> data)
        {
            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);

                var assData = new AssemblyData(assemblyPath, assembly.GetName().Name);
                data.Add(assData);

                foreach (var fixtureType in assembly.GetTypes())
                {
                    if (!ReadFixture(fixtureType, assData))
                    {
                        //Console.WriteLine(string.Format("Journals could not be created for {0}", fixtureType.Name));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("The specified assembly could not be loaded for testing.");
                return false;
            }

            return true;
        }

        private bool ReadFixture(Type fixtureType, IAssemblyData data)
        {
            var fixtureAttribs = fixtureType.GetCustomAttributes(typeof(TestFixtureAttribute), true);
            if (!fixtureAttribs.Any())
            {
                //Console.WriteLine("Specified fixture does not have the required TestFixture attribute.");
                return false;
            }

            var fixData = new FixtureData(data, fixtureType.Name);
            data.Fixtures.Add(fixData);

            foreach (var test in fixtureType.GetMethods())
            {
                var testAttribs = test.GetCustomAttributes(typeof(TestAttribute), false);
                if (!testAttribs.Any())
                {
                    // skip this method
                    continue;
                }

                if (!ReadTest(test, fixData))
                {
                    //Console.WriteLine(string.Format("Journal could not be created for test:{0} in fixture:{1}", _test,_fixture));
                    continue;
                }
            }

            return true;
        }

        private bool ReadTest(MethodInfo test, IFixtureData data)
        {
            //set the default modelPath to the empty.rfa file that will live in the build directory
            string modelPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "empty.rfa");

            var testModelAttribs = test.GetCustomAttributes(typeof(TestModelAttribute), false);
            if (testModelAttribs.Any())
            {
                //overwrite the model path with the one
                //specified in the test model attribute
                modelPath = Path.GetFullPath(Path.Combine(WorkingDirectory, ((TestModelAttribute)testModelAttribs[0]).Path));
            }

            var runDynamoAttribs = test.GetCustomAttributes(typeof(RunDynamoAttribute), false);
            var runDynamo = false;
            if (runDynamoAttribs.Any())
            {
                runDynamo = ((RunDynamoAttribute)runDynamoAttribs[0]).RunDynamo;
            }

            var testData = new TestData(data, test.Name, modelPath, runDynamo);
            data.Tests.Add(testData);

            return true;
        }

        private void CreateJournal(string path, string testName, string fixtureName, string assemblyPath, string resultsPath, string modelPath)
        {
            using (var tw = new StreamWriter(path, false))
            {
                var journal = String.Format(@"'" +
                                            "Dim Jrn \n" +
                                            "Set Jrn = CrsJournalScript \n" +
                                            "Jrn.Command \"StartupPage\" , \"Open this project , ID_FILE_MRU_FIRST\" \n" +
                                            "Jrn.Data \"MRUFileName\"  , \"{0}\" \n" +
                                            "Jrn.RibbonEvent \"Execute external command:{1}:{2}\" \n" +
                                            "Jrn.Data \"APIStringStringMapJournalData\", 5, \"testName\", \"{3}\", \"fixtureName\", \"{4}\", \"testAssembly\", \"{5}\", \"resultsPath\", \"{6}\", \"debug\",\"{7}\" \n" +
                                            "Jrn.Command \"Internal\" , \"Flush undo and redo stacks , ID_FLUSH_UNDO\" \n" +
                                            "Jrn.Command \"SystemMenu\" , \"Quit the application; prompts to save projects , ID_APP_EXIT\"",
                    modelPath, PluginGuid, PluginClass, testName, fixtureName, assemblyPath, resultsPath, IsDebug);

                tw.Write(journal);
                tw.Flush();

                JournalPaths.Add(path);
            }
        }

        internal void CreateAddin(string addinPath, string assemblyPath)
        {
            using (var tw = new StreamWriter(addinPath, false))
            {
                var addin = String.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>\n" +
                    "<RevitAddIns>\n" +
                    "<AddIn Type=\"Command\">\n" +
                    "<Name>Dynamo Test Framework</Name>\n" +
                    "<Assembly>\"{0}\"</Assembly>\n" +
                    "<AddInId>{1}</AddInId>\n" +
                    "<FullClassName>{2}</FullClassName>\n" +
                    "<VendorId>ADSK</VendorId>\n" +
                    "<VendorDescription>Autodesk</VendorDescription>\n" +
                    "</AddIn>\n" +
                    "</RevitAddIns>",
                    assemblyPath, _pluginGuid, _pluginClass
                    );

                tw.Write(addin);
                tw.Flush();
            }
        }

        public void Refresh()
        {
            Assemblies.Clear();
            ReadAssembly(TestAssembly, Assemblies);

            Console.WriteLine(this.ToString());
        }

        public void RunAssembly(IAssemblyData ad)
        {
            foreach (var fix in ad.Fixtures)
            {
                RunFixture(fix);
            }
        }

        public void RunFixture(IFixtureData fd)
        {
            foreach (var td in fd.Tests)
            {
                RunTest(td);
            }
        }

        public void RunTest(ITestData td)
        {
            var journalPath = Path.Combine(WorkingDirectory, td.Name + ".txt");
            CreateJournal(journalPath, td.Name, td.Fixture.Name, td.Fixture.Assembly.Path, Results, td.ModelPath);

            var startInfo = new ProcessStartInfo()
            {
                FileName = RevitPath,
                WorkingDirectory = WorkingDirectory,
                Arguments = journalPath,
                UseShellExecute = false
            };

            Console.WriteLine("Running {0}", journalPath);
            var process = new Process { StartInfo = startInfo };
            process.Start();

            var timedOut = false;

            if (IsDebug)
            {
                process.WaitForExit();
            }
            else
            {
                var time = 0;
                while (!process.WaitForExit(1000))
                {
                    Console.Write(".");
                    time += 1000;
                    if (time > Timeout)
                    {
                        Console.WriteLine("Test timed out.");
                        td.TestStatus = TestStatus.TimedOut;
                        timedOut = true;
                        break;
                    }
                }
                if (timedOut)
                {
                    if (!process.HasExited)
                        process.Kill();
                }
            }

            if (!timedOut && Gui)
                GetTestResultStatus(td);

            RunCount--;
            if (RunCount == 0)
            {
                OnTestRunsComplete();
            }
        }

        private void GetTestResultStatus(ITestData td)
        {
            //set the test status
            var results = LoadResults(Results);
            if (results != null)
            {
                //find our results in the results
                var mainSuite = results.testsuite;
                var ourSuite =
                    results.testsuite.results.Items
                        .Cast<testsuiteType>()
                        .FirstOrDefault(s => s.name == td.Fixture.Name);
                var ourTest = ourSuite.results.Items
                    .Cast<testcaseType>().FirstOrDefault(t => t.name == td.Name);

                switch (ourTest.result)
                {
                    case "Cancelled":
                        td.TestStatus = TestStatus.Cancelled;
                        break;
                    case "Error":
                        td.TestStatus = TestStatus.Error;
                        break;
                    case "Failure":
                        td.TestStatus = TestStatus.Failure;
                        break;
                    case "Ignored":
                        td.TestStatus = TestStatus.Ignored;
                        break;
                    case "Inconclusive":
                        td.TestStatus = TestStatus.Inconclusive;
                        break;
                    case "NotRunnable":
                        td.TestStatus = TestStatus.NotRunnable;
                        break;
                    case "Skipped":
                        td.TestStatus = TestStatus.Skipped;
                        break;
                    case "Success":
                        td.TestStatus = TestStatus.Success;
                        break;
                }

                if (ourTest.Item == null) return;
                var failure = ourTest.Item as failureType;
                if (failure == null) return;

                //if (_vm != null && _vm.UiDispatcher != null)
                //{
                //    _vm.UiDispatcher.BeginInvoke((Action)(() => td.ResultData.Add(
                //        new ResultData()
                //        {
                //            StackTrace = failure.stacktrace,
                //            Message = failure.message
                //        })));
                //}

                Dispatcher.CurrentDispatcher.BeginInvoke((Action)(() => td.ResultData.Add(
                        new ResultData()
                        {
                            StackTrace = failure.stacktrace,
                            Message = failure.message
                        })));
            }
        }

        internal void Cleanup()
        {
            try
            {
                foreach (var path in JournalPaths)
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                JournalPaths.Clear();

                var journals = Directory.GetFiles(WorkingDirectory, "journal.*.txt");
                foreach (var journal in journals)
                {
                    File.Delete(journal);
                }

                var addinPath = Path.Combine(WorkingDirectory, "RevitTestFramework.addin");
                if (File.Exists(addinPath))
                {
                    File.Delete(addinPath);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("One or more journal files could not be deleted.");
            }
        }

        internal resultType LoadResults(string resultsPath)
        {
            if (!File.Exists(resultsPath))
            {
                return null;
            }

            resultType results = null;

            //write to the file
            var x = new XmlSerializer(typeof(resultType));
            using (var reader = new StreamReader(resultsPath))
            {
                results = (resultType)x.Deserialize(reader);
            }

            return results;
        }

        internal void SaveSettings()
        {
            Settings.Default.workingDirectory = WorkingDirectory;
            Settings.Default.assemblyPath = TestAssembly;
            Settings.Default.resultsPath = Results;
            Settings.Default.isDebug = IsDebug;
            Settings.Default.timeout = Timeout;
            Settings.Default.Save();
        }

        internal void LoadSettings()
        {
            WorkingDirectory = !String.IsNullOrEmpty(Settings.Default.workingDirectory)
                ? Settings.Default.workingDirectory
                : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            TestAssembly = !String.IsNullOrEmpty(Settings.Default.assemblyPath)
                ? Settings.Default.assemblyPath
                : null;

            Results = !String.IsNullOrEmpty(Settings.Default.resultsPath)
                ? Settings.Default.resultsPath
                : null;

            Timeout = Settings.Default.timeout;
            IsDebug = Settings.Default.isDebug;
        }

        internal bool FindRevit(IList<RevitProduct> productList)
        {
            var products = RevitProductUtility.GetAllInstalledRevitProducts();
            if (!products.Any())
            {
                Console.WriteLine("No versions of revit could be found");
                return false;
            }

            products.ForEach(productList.Add);
            return true;
        }

        #endregion
    
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("Assembly : {0}", TestAssembly));
            sb.AppendLine(string.Format("Fixture : {0}", Fixture));
            sb.AppendLine(string.Format("Test : {0}", Test));
            sb.AppendLine(string.Format("Results Path : {0}", Results));
            sb.AppendLine(string.Format("Timeout : {0}", Timeout));
            sb.AppendLine(string.Format("Debug : {0}", IsDebug ? "True" : "False"));
            sb.AppendLine(string.Format("Working Directory : {0}", WorkingDirectory));
            sb.AppendLine(string.Format("GUI : {0}", Gui ? "True" : "False"));
            sb.AppendLine(string.Format("Revit : {0}", RevitPath));
            sb.AppendLine(string.Format("Addin Path : {0}", AddinPath));
            sb.AppendLine(string.Format("Assembly Path : {0}", AssemblyPath));
            return sb.ToString();
        }
    }

    public class AssemblyData : IAssemblyData
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public ObservableCollection<IFixtureData> Fixtures { get; set; }

        public AssemblyData(string path, string name)
        {
            Fixtures = new ObservableCollection<IFixtureData>();
            Path = path;
            Name = name;
        }
    }

    public class FixtureData : NotificationObject, IFixtureData
    {
        public string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }
        public FixtureStatus FixtureStatus { get; set; }
        public IAssemblyData Assembly { get; set; }

        public string FixtureSummary
        {
            get
            {
                var successCount = Tests.Count(x => x.TestStatus == TestStatus.Success);
                var failCount = Tests.Count(x => x.TestStatus == TestStatus.Failure);
                var otherCount = Tests.Count - successCount - failCount;
                return string.Format("[Total: {0}, Success: {1}, Failure: {2}, Other: {3}]", Tests.Count, successCount, failCount, otherCount);
            }
        }

        public FixtureData(IAssemblyData assembly, string name)
        {
            Assembly = assembly;
            Tests = new ObservableCollection<ITestData>();
            Name = name;
            FixtureStatus = FixtureStatus.None;

            Tests.CollectionChanged += Tests_CollectionChanged;
        }

        void Tests_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var item in e.NewItems)
                    {
                        var td = item as TestData;
                        td.PropertyChanged += td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var item in e.OldItems)
                    {
                        var td = item as TestData;
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var item in e.OldItems)
                    {
                        var td = item as TestData;
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                default:
                    break;
            }
        }

        void td_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "TestStatus")
            {
                if (Tests.All(t => t.TestStatus == TestStatus.Success))
                {
                    FixtureStatus = FixtureStatus.Success;
                }
                else if (Tests.Any(t => t.TestStatus == TestStatus.Failure))
                {
                    FixtureStatus = FixtureStatus.Failure;
                }
                else
                {
                    FixtureStatus = FixtureStatus.Mixed;
                }

                RaisePropertyChanged("FixtureStatus");
                RaisePropertyChanged("FixtureSummary");
            }
        }
    }

    public class TestData : NotificationObject, ITestData
    {
        private TestStatus _testStatus;
        private IList<IResultData> _resultData;
        public string Name { get; set; }
        public bool RunDynamo { get; set; }
        public string ModelPath { get; set; }

        public string ShortModelPath
        {
            get
            {
                if (string.IsNullOrEmpty(ModelPath))
                {
                    return string.Empty;
                }

                var info = new FileInfo(ModelPath);
                return string.Format("[{0}]", info.Name);
            }
        }

        public TestStatus TestStatus
        {
            get { return _testStatus; }
            set
            {
                _testStatus = value;
                RaisePropertyChanged("TestStatus");
            }
        }

        public ObservableCollection<IResultData> ResultData { get; set; }

        public IFixtureData Fixture { get; set; }

        public TestData(IFixtureData fixture, string name, string modelPath, bool runDynamo)
        {
            Fixture = fixture;
            Name = name;
            ModelPath = modelPath;
            RunDynamo = runDynamo;
            _testStatus = TestStatus.None;
            ResultData = new ObservableCollection<IResultData>();

            ResultData.CollectionChanged += ResultDataOnCollectionChanged;
        }

        private void ResultDataOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs notifyCollectionChangedEventArgs)
        {
            RaisePropertyChanged("ResultData");
        }
    }

    public class ResultData : NotificationObject, IResultData
    {
        private string _message = "";
        private string _stackTrace = "";

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                RaisePropertyChanged("Message");
            }
        }

        public string StackTrace
        {
            get { return _stackTrace; }
            set
            {
                _stackTrace = value;
                RaisePropertyChanged("StackTrace");
            }
        }
    }

}
