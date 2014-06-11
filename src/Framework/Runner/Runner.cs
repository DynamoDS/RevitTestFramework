using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Autodesk.RevitAddIns;
using Microsoft.Practices.Prism;
using Microsoft.Practices.Prism.ViewModel;

namespace RTF.Framework
{
    public delegate void TestCompleteHandler(ITestData data, string resultsPath);

    /// <summary>
    /// The Runner model.
    /// </summary>
    public class Runner : NotificationObject
    {
        #region events

        public event EventHandler TestRunsComplete;
        public event TestCompleteHandler TestComplete;

        #endregion

        #region private members

        private string _testAssembly;
        private string _test;
        private string _fixture;
        private bool _isDebug;
        private string _results;
        private const string _pluginGuid = "487f9ff0-5b34-4e7e-97bf-70fbff69194f";
        private const string _pluginClass = "RTF.Applications.RevitTestFramework";
        private const string _appGuid = "c950020f-3da0-4e48-ab82-5e30c3f4b345";
        private const string _appClass = "RTF.Applications.RevitTestFrameworkExternalApp";
        private string _workingDirectory;
        private bool _gui = true;
        private string _revitPath;
        private List<string> _journalPaths = new List<string>();
        private int _runCount = 0;
        private int _timeout = 120000;
        private bool _concat;
        private string _addinPath;
        private string _assemblyPath;
        private int _selectedProduct;
        private ObservableCollection<IAssemblyData> _assemblies = new ObservableCollection<IAssemblyData>();
        private ObservableCollection<RevitProduct> _products = new ObservableCollection<RevitProduct>();
        private bool isRunning = false;

        #endregion

        #region internal properties

        internal static string PluginGuid
        {
            get { return _pluginGuid; }
        }

        internal static string PluginClass
        {
            get { return _pluginClass; }
        }

        internal string AddinPath
        {
            get { return _addinPath; }
            set { _addinPath = value; }
        }

        internal List<string> JournalPaths
        {
            get { return _journalPaths; }
            set { _journalPaths = value; }
        }

        public int RunCount
        {
            get { return _runCount; }
            set { _runCount = value; }
        }

        internal string AssemblyPath
        {
            get { return _assemblyPath; }
            set { _assemblyPath = value; }
        }

        #endregion

        #region public properties

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

        public bool Gui
        {
            get { return _gui; }
            set { _gui = value; }
        }

        public int SelectedProduct
        {
            get { return _selectedProduct; }
            set
            {
                _selectedProduct = value;
                RaisePropertyChanged("SelectedProduct");
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

        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set
            {
                _workingDirectory = value;

                // Delete any existing addins before resetting the addins path.
                if (!string.IsNullOrEmpty(AddinPath) && File.Exists(AddinPath))
                {
                    File.Delete(AddinPath);
                }
                AddinPath = Path.Combine(WorkingDirectory, "RevitTestFramework.addin");
            }
        }

        public string RevitPath
        {
            get { return _revitPath; }
            set { _revitPath = value; }
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

        #endregion

        #region constructors

        public Runner()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

            AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "RTFRevit.dll");
        }

        Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            // Check the assembly location
            var asmToCheck = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\" + name.Name + ".dll";
            if (File.Exists(asmToCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(asmToCheck);
            }

            // Check same directory as assembly
            asmToCheck = Path.GetDirectoryName(TestAssembly) + "\\" + name.Name + ".dll";
            if (File.Exists(asmToCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(asmToCheck);
            }

            // Check several levels above directory
            for (int i = 0; i < 3; i++)
            {
                var prevFolder = Path.GetDirectoryName(asmToCheck);
                var folder = Path.GetFullPath(Path.Combine(prevFolder, @"..\" ));
                asmToCheck = folder + "\\" + name.Name + ".dll";
                if (File.Exists(asmToCheck))
                {
                    return Assembly.ReflectionOnlyLoadFrom(asmToCheck);
                }

                // If we can't find it in this directory, search
                // all sub-directories that aren't the previous folder
                var di = new DirectoryInfo(folder);
                foreach (var d in di.GetDirectories("*",SearchOption.AllDirectories) .Where(d => d.FullName != folder))
                {
                    var subfolderCheck = d.FullName + "\\" + name.Name + ".dll";
                    if (File.Exists(subfolderCheck))
                    {
                        return Assembly.ReflectionOnlyLoadFrom(subfolderCheck);
                    }
                }
            }

            // Finally, check the runtime directory
            var runtime = System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory();
            var systemCheck = runtime + "\\" + name.Name + ".dll";
            if (File.Exists(systemCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(systemCheck);
            }

            // Check WPF
            var wpfCheck = runtime + "\\WPF\\" + name.Name + ".dll";
            if (File.Exists(wpfCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(wpfCheck);
            }

            // Check the Revit API
            var revitCheck = Path.GetDirectoryName(Products[SelectedProduct].InstallLocation) + "\\" + name.Name + ".dll";
            if (File.Exists(revitCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(revitCheck);
            }

            return null;
        }

        public static Runner BySetupPaths(string workingDirectory, string testAssembly,
            string resultsPath,string testName="", string fixtureName="")
        {
            var products = FindRevit();

            var runner = new Runner
            {
                WorkingDirectory = workingDirectory,
                TestAssembly = testAssembly,
                Test = testName,
                Fixture = fixtureName,
                Results = resultsPath,
                Gui = false,
                RevitPath = Path.Combine(products.First().InstallLocation, "revit.exe")
            };

            return runner;
        }

        #endregion

        #region public methods

        public void RunAssembly(IAssemblyData ad)
        {
            if (!File.Exists(AddinPath))
            {
                CreateAddin(AddinPath, AssemblyPath);
            }

            foreach (var fix in ad.Fixtures)
            {
                RunFixture(fix);
            }
        }

        public void RunFixture(IFixtureData fd)
        {
            if (!File.Exists(AddinPath))
            {
                CreateAddin(AddinPath, AssemblyPath);
            }

            foreach (var td in fd.Tests)
            {
                RunTest(td);
            }
        }

        public void RunTest(ITestData td)
        {
            CreateAddin(AddinPath, AssemblyPath);

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
            {
                OnTestComplete(td);
                //GetTestResultStatus(td);
            }

            RunCount--;
            if (RunCount == 0)
            {
                OnTestRunsComplete();
            }
        }

        public void Refresh()
        {
            Assemblies.Clear();
            Assemblies.AddRange(ReadAssembly(TestAssembly, _workingDirectory));

            Console.WriteLine(ToString());
        }

        public void Cleanup()
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

                if (!string.IsNullOrEmpty(AddinPath) && File.Exists(AddinPath))
                {
                    File.Delete(AddinPath);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("One or more journal files could not be deleted.");
            }
        }

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

        #endregion

        #region private methods

        private void OnTestRunsComplete()
        {
            if (TestRunsComplete != null)
            {
                TestRunsComplete(null, EventArgs.Empty);
            }
        }

        private void OnTestComplete(ITestData data)
        {
            if (TestComplete != null)
            {
                TestComplete(data, Results);
            }
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
                                            "Jrn.Data \"APIStringStringMapJournalData\", 6, \"testName\", \"{3}\", \"fixtureName\", \"{4}\", \"testAssembly\", \"{5}\", \"resultsPath\", \"{6}\", \"debug\",\"{7}\",\"workingDirectory\",\"{8}\" \n" +
                                            "Jrn.Command \"Internal\" , \"Flush undo and redo stacks , ID_FLUSH_UNDO\" \n" +
                                            "Jrn.Command \"SystemMenu\" , \"Quit the application; prompts to save projects , ID_APP_EXIT\"",
                    modelPath, PluginGuid, PluginClass, testName, fixtureName, assemblyPath, resultsPath, IsDebug, WorkingDirectory);

                //var journal = String.Format(@"'" +
                //                            "Dim Jrn \n" +
                //                            "Set Jrn = CrsJournalScript \n" +
                //                            "Jrn.Command \"StartupPage\" , \"Open this project , ID_FILE_MRU_FIRST\" \n" +
                //                            "Jrn.Data \"MRUFileName\"  , \"{0}\" \n" +
                //                            "Jrn.RibbonEvent \"Execute external command:{1}:{2}\" \n" +
                //                            "Jrn.Data \"APIStringStringMapJournalData\", 6, \"testName\", \"{3}\", \"fixtureName\", \"{4}\", \"testAssembly\", \"{5}\", \"resultsPath\", \"{6}\", \"debug\",\"{7}\",\"workingDirectory\",\"{8}\" \n",
                //    modelPath, PluginGuid, PluginClass, testName, fixtureName, assemblyPath, resultsPath, IsDebug, WorkingDirectory);

                tw.Write(journal);
                tw.Flush();

                JournalPaths.Add(path);
            }
        }

        private void CreateAddin(string addinPath, string assemblyPath)
        {
            using (var tw = new StreamWriter(addinPath, false))
            {
                var addin = String.Format(
                    "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"no\"?>\n" +
                    "<RevitAddIns>\n" +

                    "<AddIn Type=\"Application\">\n" +
                    "<Name>Dynamo Test Framework App</Name>\n" +
                    "<Assembly>\"{0}\"</Assembly>\n" +
                    "<AddInId>{1}</AddInId>\n" +
                    "<FullClassName>{2}</FullClassName>\n" +
                    "<VendorId>Dynamo</VendorId>\n" +
                    "<VendorDescription>Dynamo</VendorDescription>\n" +
                    "</AddIn>\n" +

                    "<AddIn Type=\"Command\">\n" +
                    "<Name>Dynamo Test Framework</Name>\n" +
                    "<Assembly>\"{0}\"</Assembly>\n" +
                    "<AddInId>{3}</AddInId>\n" +
                    "<FullClassName>{4}</FullClassName>\n" +
                    "<VendorId>Dynamo</VendorId>\n" +
                    "<VendorDescription>Dynamo</VendorDescription>\n" +
                    "</AddIn>\n" +

                    "</RevitAddIns>",
                    assemblyPath,_appGuid, _appClass, _pluginGuid, _pluginClass
                    );

                tw.Write(addin);
                tw.Flush();
            }
        }

        #endregion

        #region public static methods

        public static IList<RevitProduct> FindRevit()
        {
            var products = RevitProductUtility.GetAllInstalledRevitProducts()
                .Where(x=>x.Version == RevitVersion.Revit2015).ToList();

            if (!products.Any())
            {
                Console.WriteLine("No versions of revit could be found");
                return null;
            }

            return products;
        }

        public static IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory)
        {
            IList<IAssemblyData> data = new List<IAssemblyData>();

            try
            {
                var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);

                var assData = new AssemblyData(assemblyPath, assembly.GetName().Name);
                data.Add(assData);
                
                foreach (var fixtureType in assembly.GetTypes())
                {
                    if (!ReadFixture(fixtureType, assData, workingDirectory))
                    {
                        //Console.WriteLine(string.Format("Journals could not be created for {0}", fixtureType.Name));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("The specified assembly could not be loaded for testing.");
                return null;
            }
       
            return data;
        }

        public static bool ReadFixture(Type fixtureType, IAssemblyData data, string workingDirectory)
        {
            var fixtureAttribs = CustomAttributeData.GetCustomAttributes(fixtureType);

            if (!fixtureAttribs.Any(x => x.Constructor.DeclaringType.Name == "TestFixtureAttribute"))
            {
                //Console.WriteLine("Specified fixture does not have the required TestFixture attribute.");
                return false;
            }

            var fixData = new FixtureData(data, fixtureType.Name);
            data.Fixtures.Add(fixData);

            foreach (var test in fixtureType.GetMethods())
            {
                var testAttribs = CustomAttributeData.GetCustomAttributes(test);

                if (!testAttribs.Any(x => x.Constructor.DeclaringType.Name == "TestAttribute"))
                {
                    // skip this method
                    continue;
                }

                if (!ReadTest(test, fixData, workingDirectory))
                {
                    //Console.WriteLine(string.Format("Journal could not be created for test:{0} in fixture:{1}", _test,_fixture));
                    continue;
                }
            }

            return true;
        }

        public static bool ReadTest(MethodInfo test, IFixtureData data, string workingDirectory)
        {
            //set the default modelPath to the empty.rfa file that will live in the build directory
            string modelPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "empty.rfa");

            var testAttribs = CustomAttributeData.GetCustomAttributes(test);

            var testModelAttrib =
                testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == "TestModelAttribute");

            if (testModelAttrib != null)
            {
                
                //overwrite the model path with the one
                //specified in the test model attribute
                var relModelPath = testModelAttrib.ConstructorArguments.FirstOrDefault().Value.ToString();
                modelPath = Path.GetFullPath(Path.Combine(workingDirectory, relModelPath));
            }

            var runDynamoAttrib = 
                testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == "RunDynamoAttribute");

            var runDynamo = false;
            if (runDynamoAttrib != null)
            {
                runDynamo = bool.Parse(runDynamoAttrib.ConstructorArguments.FirstOrDefault().Value.ToString());
            }

            var testData = new TestData(data, test.Name, modelPath, runDynamo);
            data.Tests.Add(testData);

            return true;
        }

        #endregion
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
            if (e.PropertyName == "TestStatus" || e.PropertyName=="ResultData")
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
