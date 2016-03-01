using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using Autodesk.RevitAddIns;
using Dynamo.NUnit.Tests;
using Microsoft.Practices.Prism;
using NDesk.Options;

namespace RTF.Framework
{
    public delegate void TestCompleteHandler(IEnumerable<ITestData> data, string resultsPath);
    public delegate void TestTimedOutHandler(ITestData data);
    public delegate void TestFailedHandler(ITestData data, string message, string stackTrace);

    /// <summary>
    /// The Runner is responsible for setting up tests, running
    /// them, and interpreting the results.
    /// </summary>
    [Serializable]
    [XmlRoot]
    public partial class Runner : IRunner, IDisposable
    {
        #region events

        public event EventHandler TestRunsComplete;
        public event TestCompleteHandler TestComplete;
        public event TestTimedOutHandler TestTimedOut;
        public event TestFailedHandler TestFailed;
        public event EventHandler Initialized;

        #endregion

        #region private members

        private const string _pluginGuid = "487f9ff0-5b34-4e7e-97bf-70fbff69194f";
        private const string _pluginClass = "RTF.Applications.RevitTestFramework";
        private const string _clientStartPluginGuid = "8F372D6C-ECAA-4669-939E-2CD21C1F1E70";
        private const string _clientStartPluginClass = "RTF.Applications.RTFClientStartCmd";
        private const string _clientEndPluginGuid = "7CF281FA-D8C0-499E-AA60-7A1CF582129F";
        private const string _clientEndPluginClass = "RTF.Applications.RTFClientEndCmd";
        private const string _appGuid = "c950020f-3da0-4e48-ab82-5e30c3f4b345";
        private const string _appClass = "RTF.Applications.RevitTestFrameworkExternalApp";
        private string _workingDirectory;
        private bool _gui = true;
        private string _revitPath;
        private bool _copyAddins = true;
        private int _timeout = 120000;
        private bool _concat;
        private List<string> _copiedAddins = new List<string>();
        private string _assemblyPath;
        private int _selectedProduct;
        private ObservableCollection<IAssemblyData> _assemblies = new ObservableCollection<IAssemblyData>();
        private ObservableCollection<RevitProduct> _products = new ObservableCollection<RevitProduct>();
        private bool isRunning = false;
        private bool cancelRequested;
        private object cancelLock = new object();
        private bool dryRun;
        private bool cleanup = true;
        private bool continuous;
        private bool journalInitialized = false;
        private bool journalFinished;
        private GroupingType groupingType = GroupingType.Fixture;
        private List<SelectionHint> selectionHints = new List<SelectionHint>(); 
        private List<string> additionalResolutionDirectories = new List<string>();
        private List<ITestData> completedTestCases = new List<ITestData>();
 
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

        internal static string ClientStartPluginGuid
        {
            get { return _clientStartPluginGuid; }
        }

        internal static string ClientStartPluginClass
        {
            get { return _clientStartPluginClass; }
        }

        internal static string ClientEndPluginGuid
        {
            get { return _clientEndPluginGuid; }
        }

        internal static string ClientEndPluginClass
        {
            get { return _clientEndPluginClass; }
        }

        #endregion

        #region public properties

        /// <summary>
        /// The path of the RTF addin file.
        /// </summary>
        public string AddinPath 
        {
            get{return Path.Combine(WorkingDirectory, "RevitTestFramework.addin");}
        }

        /// <summary>
        /// The path to the batch journal file.
        /// </summary>
        public string BatchJournalPath
        {
            get { return Path.Combine(WorkingDirectory, "RTF_Batch_Test.txt"); }
        }

        /// <summary>
        /// This one records all the addins that are copied
        /// </summary>
        private List<string> CopiedAddins
        {
            get { return _copiedAddins; }
            set { _copiedAddins = value; }
        }

        /// <summary>
        /// The path of the selected assembly for testing.
        /// </summary>
        public string AssemblyPath { get; set; }

        /// <summary>
        /// A collection of assemblies available for testing.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<IAssemblyData> Assemblies
        {
            get { return _assemblies; }
            set
            {
                _assemblies = value;
            }
        }

        /// <summary>
        /// A collection of available Revit products for testing.
        /// </summary>
        [XmlIgnore]
        public ObservableCollection<RevitProduct> Products
        {
            get { return _products; }
            set
            {
                _products = value;
            }
        }

        /// <summary>
        /// The name of the test to run.
        /// </summary>
        public string Test { get; set; }

        /// <summary>
        /// The assembly containing the tests.
        /// </summary>
        public string TestAssembly { get; set; }

        /// <summary>
        /// The name of the fixture to run.
        /// </summary>
        public string Fixture { get; set; }

        /// <summary>
        /// The name of the category to run
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// The name of the category to exclude.
        /// </summary>
        public string ExcludedCategory { get; set; }

        /// <summary>
        /// A flag which, when set, allows you
        /// to attach to the debugger.
        /// </summary>
        public bool IsDebug { get; set; }

        /// <summary>
        /// The path to the results file.
        /// </summary>
        public string Results { get; set; }

        /// <summary>
        /// The path to the working directory.
        /// </summary>
        public string WorkingDirectory
        {
            get { return _workingDirectory; }
            set
            {
                _workingDirectory = value;

                // Delete any existing addins before resetting the addins path.
                DeleteAddins();
            }
        }

        /// <summary>
        /// The path to the version of Revit to be
        /// used for testing.
        /// </summary>
        public string RevitPath
        {
            get { return _revitPath;}
            set { _revitPath = value; }
        }

        /// <summary>
        /// Specified whether to copy addins from the 
        /// Revit addin folder to the current working directory
        /// </summary>
        public bool CopyAddins
        {
            get { return _copyAddins; }
            set { _copyAddins = value; }
        }

        /// <summary>
        /// A timeout value in milliseconds, after which
        /// any running test will be killed.
        /// </summary>
        public int Timeout
        {
            get { return _timeout; }
            set { _timeout = value; }
        }

        /// <summary>
        /// A flag to specify whether to concatenate test 
        /// results with those from a previous run.
        /// </summary>
        public bool Concat { get; set; }

        /// <summary>
        /// A flag to allow cancellation. Cancellation will occur
        /// after the running test is completed.
        /// </summary>
        public bool CancelRequested
        {
            get
            {
                lock (cancelLock)
                {
                    return cancelRequested;
                }
            }
            set
            {
                lock (cancelLock)
                {
                    cancelRequested = value;
                }
            }
        }

        /// <summary>
        /// A flag which allows the setup of tests and the creation
        /// of an addin file without actually running the tests.
        /// </summary>
        public bool DryRun
        {
            get{ return dryRun; }
            set { dryRun = value; }
        }

        /// <summary>
        /// A flag which controls whether journal files and addins
        /// generated by RTF are cleaned up upon test completion.
        /// </summary>
        public bool CleanUp
        {
            get { return cleanup; }
            set { cleanup = value; }
        }

        /// <summary>
        /// A flag which specifies whether all tests should be
        /// run from the same journal file.
        /// </summary>
        public bool Continuous
        {
            get { return continuous; }
            set { continuous = value; }
        }

        public List<SelectionHint> SelectionHints
        {
            get
            {
                return selectionHints;
            }
            set
            {
                selectionHints = value;
            }
        }

        public GroupingType GroupingType
        {
            get { return groupingType; }
            set
            {
                groupingType = value;
                switch (value)
                {
                    case GroupingType.Category:
                        foreach (var asm in Assemblies)
                        {
                            asm.GroupingType = GroupingType.Category;
                        }
                        break;
                    case GroupingType.Fixture:
                        foreach (var asm in Assemblies)
                        {
                            asm.GroupingType = GroupingType.Fixture;
                        }
                        break;
                }
            }
        }

        public bool IsTesting { get; set; }

        /// <summary>
        /// A list of folders to add to the assembly resolution step.
        /// </summary>
        public List<string> AdditionalResolutionDirectories
        {
            get { return additionalResolutionDirectories; }
            set { additionalResolutionDirectories = value; }
        } 

        #endregion

        #region constructors

        /// <summary>
        /// Default Runner constructor. This constructor does not initialize
        /// any properties. If you'd like to construct a runner with property
        /// values (ex. from parsing the command line), use the other constructor.
        /// </summary>
        public Runner()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

            Initialize();
        }

        /// <summary>
        /// Constructs a runner given a SetupData object containing property values.
        /// Use this constructor if you'd like to construct a runner and know some,
        /// or all, of the property values.
        /// </summary>
        /// <param name="setupData"></param>
        public Runner(IRunnerSetupData setupData)
        {
            if (!String.IsNullOrEmpty(setupData.TestAssembly) && !File.Exists(setupData.TestAssembly))
            {
                throw new ArgumentException("The specified test assembly does not exist.");
            }

            if (String.IsNullOrEmpty(setupData.TestAssembly))
            {
                setupData.TestAssembly = Assembly.GetExecutingAssembly().Location;
            }

            if (String.IsNullOrEmpty(setupData.AssemblyPath) || !File.Exists(setupData.AssemblyPath))
            {
                setupData.AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RTFRevit.dll");
            }

            if (!String.IsNullOrEmpty(setupData.WorkingDirectory) && !Directory.Exists(setupData.WorkingDirectory))
            {
                throw new ArgumentException("The specified working directory does not exist.");
            }

            if (String.IsNullOrEmpty(setupData.WorkingDirectory) || !Directory.Exists(setupData.WorkingDirectory))
            {
                setupData.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }

            if (!String.IsNullOrEmpty(setupData.Category))
            {
                setupData.GroupingType = GroupingType.Category;
            }

            WorkingDirectory = setupData.WorkingDirectory;
            AssemblyPath = setupData.AssemblyPath;
            TestAssembly = setupData.TestAssembly;
            Results = setupData.Results;
            Fixture = setupData.Fixture;
            Category = setupData.Category;
            Test = setupData.Test;
            Concat = setupData.Concat;
            DryRun = setupData.DryRun;
            RevitPath = setupData.RevitPath;
            CleanUp = setupData.CleanUp;
            Continuous = setupData.Continuous;
            IsDebug = setupData.IsDebug;
            GroupingType = setupData.GroupingType;
            Timeout = setupData.Timeout;
            IsTesting = setupData.IsTesting;
            ExcludedCategory = setupData.ExcludedCategory;

            if(!string.IsNullOrEmpty(setupData.AdditionalResolutionDirectories))
            {
                var splits = setupData.AdditionalResolutionDirectories.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                if (!splits.Any()) return;

                this.AdditionalResolutionDirectories.Clear();
                foreach (var split in splits)
                {
                    this.AdditionalResolutionDirectories.Add(split);
                }
            }
            var resolver = new DefaultAssemblyResolver(RevitPath, AdditionalResolutionDirectories);
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver.Resolve;

            Initialize();
        }

        #endregion

        #region public methods

        public void StartServer()
        {
            // If dryRun is true, the server won't be started
            if (continuous && !dryRun)
            {
                RevitTestServer.Instance.Start(Timeout);
            }
        }

        public void EndServer()
        {
            if (continuous && !dryRun)
            {
                RevitTestServer.Instance.End();
            }
        }

        /// <summary>
        /// Setup all tests, creating journal files, the addin file,
        /// and copying the addins from the addins folder on the
        /// testing machine.
        /// </summary>
        /// <param name="parameter"></param>
        public void SetupTests()
        {
            var runnable = GetRunnableTests();

            if (!File.Exists(AddinPath))
            {
                CreateAddin(AddinPath, AssemblyPath);
            }

            CreateJournalForTestCases(runnable);

            // Copy addins from the Revit addin folder to the current working directory
            // so that they can be loaded.
            if (CopyAddins)
            {
                var files = Directory.GetFiles(GetRevitAddinFolder());
                foreach (var file in files)
                {
                    if (file.EndsWith(".addin", StringComparison.OrdinalIgnoreCase))
                    {
                        var fileName = Path.GetFileName(file);
                        File.Copy(file, Path.Combine(WorkingDirectory, fileName), true);
                        CopiedAddins.Add(fileName);
                    }
                }
            }
        }

        /// <summary>
        /// Create journal files for a given set of test cases
        /// </summary>
        /// <param name="runnableTests"></param>
        private void CreateJournalForTestCases(IEnumerable<ITestData> runnableTests)
        {
            journalInitialized = false;
            journalFinished = false;
            foreach (var test in runnableTests)
            {
                SetupIndividualTest(test, Continuous);
            }

            if (continuous && !journalFinished)
            {
                FinishJournal(BatchJournalPath);
            }
        }

        /// <summary>
        /// Run all tests that are selected for running.
        /// </summary>
        public void RunAllTests()
        {
            if (dryRun) return;

            // Kill any senddmp.exe processes thrown off
            // by previous failed revit sessions
            var sendDmps = Process.GetProcessesByName("senddmp");
            if (sendDmps.Any())
            {
                sendDmps.ToList().ForEach(sd => sd.Kill());
            }

            if (continuous)
            {
                // If running in continous mode, there will only
                // be one journal file, as the value for every test
                ProcessBatchTests(BatchJournalPath);
            }
            else
            {
                var runnable = GetRunnableTests();
                foreach (var test in runnable)
                {
                    if (cancelRequested)
                    {
                        cancelRequested = false;
                        break;
                    }

                    try
                    {
                        // Don't attempt to run a test without a journal.
                        if (string.IsNullOrEmpty(test.JournalPath))
                        {
                            throw new Exception("Journal file is missing. This could be the result of a bad model path.");
                        }

                        ProcessTest(test, test.JournalPath);
                    }
                    catch (Exception ex)
                    {
                        if (test == null) continue;
                        test.TestStatus = TestStatus.Failure;
                        OnTestFailed(test, ex.Message, ex.StackTrace);
                    }
                }
            }

            Cleanup();

            OnTestRunsComplete();
        }

        /// <summary>
        /// Read the selected assembly and generate assembly data,
        /// fixture data, and test data.
        /// </summary>
        public void InitializeTests()
        {
            Assemblies.Clear();
            var assData = ReadAssembly(TestAssembly, WorkingDirectory, GroupingType, false);

            if (assData == null)
                return;

            Assemblies.AddRange(assData);

            // Clear running on all assemblies.
            foreach (var data in assData)
            {
                data.ShouldRun = false;
            }

            // Run by Fixture
            if (!String.IsNullOrEmpty(Fixture))
            {
                var fixData = assData.SelectMany(a=>a.Fixtures).FirstOrDefault(f => f.Name == Fixture);
                if (fixData != null)
                {
                    ((IExcludable)fixData).ShouldRun = true;
                }
            }
            // Run by test.
            else if (!String.IsNullOrEmpty(Test))
            {
                var testData = GetAllTests()
                        .FirstOrDefault(t => t.Name == Test);
                if (testData != null)
                {
                    testData.ShouldRun = true;
                }
            }
            // Run by category
            else if (!String.IsNullOrEmpty(Category))
            {
                var catData = assData.SelectMany(a=>a.Categories).
                    FirstOrDefault(c => c.Name == Category);
                if (catData != null)
                {
                    ((IExcludable)catData).ShouldRun = true;
                }
            }
            // If the no fixture, test, or category is specified,
            // run everything in all assemblies
            else
            {
                foreach (var data in assData)
                {
                    data.ShouldRun = true;
                }
            }

            MarkExclusions(ExcludedCategory, assData);

            if (File.Exists(Results) && !Concat)
            {
                File.Delete(Results);
            }

            OnInitialized();
        }

        /// <summary>
        /// Cleanup after a run. This method handles deletion of 
        /// journal files and addins created for a test run.
        /// </summary>
        public void Cleanup()
        {
            if (!CleanUp)
                return;

            try
            {
                var runnable = GetRunnableTests();
                if (runnable != null)
                {
                    foreach (var test in runnable.Where(test => File.Exists(test.JournalPath)))
                    {
                        File.Delete(test.JournalPath);
                    } 
                }

                var allTests = GetAllTests();
                if (allTests != null)
                {
                    foreach (var test in allTests)
                    {
                        test.JournalPath = null;
                    }
                }

                DeleteAddins();

                if (WorkingDirectory == null) return;

                var journals = Directory.GetFiles(WorkingDirectory, "journal.*.txt");
                foreach (var journal in journals)
                {
                    File.Delete(journal);
                }

                if (File.Exists(BatchJournalPath))
                {
                    File.Delete(BatchJournalPath);
                }

                if (File.Exists(AddinPath))
                {
                    File.Delete(AddinPath);
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine("One or more journal files could not be deleted.");
            }
        }

        /// <summary>
        /// Get all tests.
        /// </summary>
        /// <returns>A list of ITestDataObjects</returns>
        public IEnumerable<ITestData> GetAllTests()
        {
            var tests = Assemblies.
                SelectMany(a => a.Fixtures).
                SelectMany(f => f.Tests);

            return tests;
        }

        /// <summary>
        /// Get all tests that are selected to run.
        /// </summary>
        /// <returns>A list of ITestData objects.</returns>
        public IEnumerable<ITestData> GetRunnableTests()
        {
            var runnable = Assemblies.
                SelectMany(a => a.Fixtures.SelectMany(f=>f.Tests)).
                Where(t => t.ShouldRun == true);

            return runnable;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(String.Format("Assembly : {0}", TestAssembly));
            sb.AppendLine(String.Format("Fixture : {0}", Fixture));
            sb.AppendLine(String.Format("Category : {0}", Category));
            sb.AppendLine(String.Format("Excluded Category : {0}", ExcludedCategory));
            sb.AppendLine(String.Format("Test : {0}", Test));
            sb.AppendLine(String.Format("Results Path : {0}", Results));
            sb.AppendLine(String.Format("Timeout : {0}", Timeout));
            sb.AppendLine(String.Format("Debug : {0}", IsDebug ? "True" : "False"));
            sb.AppendLine(String.Format("Working Directory : {0}", WorkingDirectory));
            sb.AppendLine(String.Format("Revit : {0}", RevitPath));
            sb.AppendLine(String.Format("Addin Path : {0}", AddinPath));
            sb.AppendLine(String.Format("Assembly Path : {0}", AssemblyPath));
            return sb.ToString();
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= CurrentDomain_ReflectionOnlyAssemblyResolve;

            foreach (var a in Assemblies.Select(ad => ad as AssemblyData))
            {
                a.Dispose();
            }
        }

        public static void Save(string filePath, Runner runner)
        {
            var selectedTests =
                runner.Assemblies.SelectMany(
                    a =>
                        a.Fixtures.Cast<FixtureData>()
                            .SelectMany(f => f.Tests.Cast<TestData>().Where(t => t.ShouldRun == true))).ToList();
            
            runner.SelectionHints = selectedTests.Select(t => new SelectionHint(t.Fixture.Assembly.Name, t.Fixture.Name, t.Name)).ToList();

            using (var writer = new StreamWriter(filePath))
            {
                var serializer = new XmlSerializer(typeof(Runner));
                serializer.Serialize(writer, runner);
            }
        }

        /// <summary>
        /// Load a test runner from a file.
        /// </summary>
        /// <param name="filePath">The path to the saved test runner.</param>
        /// <returns>The test runner or null if initialization fails.</returns>
        public static Runner Load(string filePath)
        {
            try
            {
                Runner runner;

                using (var reader = new StreamReader(filePath))
                {
                    var serializer = new XmlSerializer(typeof (Runner));
                    runner = (Runner) serializer.Deserialize(reader);
                }

                runner.Initialize();

                runner.SetSelectionsFromHints();

                return runner;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to initialize test runner.");
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        #endregion

        #region private methods

        private void Initialize()
        {
            ConductInitializationChecks();

            InitializeProducts();

            InitializeTests();
        }

        private void SetSelectionsFromHints()
        {
            if (!SelectionHints.Any())
            {
                return;
            }

            Assemblies.ToList().ForEach(a=>a.ShouldRun = false);

            // The SelectionHints that are deserialized are only hints.
            // There's a chance the assembly no longer contains a fixture
            // or a test that is specified to run. So we walk over
            // the selection hints by groups, attempting to find the
            // test which we want to enable.

            var asmHints = SelectionHints.GroupBy(x => x.AssemblyName);
            foreach (var asmHintGroup in asmHints)
            {
                var foundAsm = Assemblies.FirstOrDefault(a => a.Name == asmHintGroup.Key);
                if(foundAsm == null)
                    continue;

                var fixHints = asmHintGroup.GroupBy(g => g.FixtureName);
                foreach (var fixHintGroup in fixHints)
                {
                    var foundFix = foundAsm.Fixtures.FirstOrDefault(f => f.Name == fixHintGroup.Key);
                    if (foundFix == null)
                        continue;

                    foreach (var fixHint in fixHintGroup)
                    {
                        var foundTest = foundFix.Tests.FirstOrDefault(t => t.Name == fixHint.TestName);
                        if (foundTest == null)
                            continue;

                        foundTest.ShouldRun = true;
                    }
                }
            }

            SelectionHints.Clear();
        }

        private void ConductInitializationChecks()
        {
            if (!String.IsNullOrEmpty(TestAssembly) && !File.Exists(TestAssembly))
            {
                throw new ArgumentException("The specified test assembly does not exist.");
            }

            if (String.IsNullOrEmpty(TestAssembly))
            {
                TestAssembly = Assembly.GetExecutingAssembly().Location;
            }

            if (String.IsNullOrEmpty(AssemblyPath) || !File.Exists(AssemblyPath))
            {
                AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "RTFRevit.dll");
            }

            if (!String.IsNullOrEmpty(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
            {
                throw new ArgumentException("The specified working directory does not exist.");
            }

            if (String.IsNullOrEmpty(WorkingDirectory) || !Directory.Exists(WorkingDirectory))
            {
                WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }

            if (!String.IsNullOrEmpty(Category))
            {
                GroupingType = GroupingType.Category;
            }
        }

        private void InitializeProducts()
        {
            Products.Clear();
            Products.AddRange(RunnerSetupData.FindRevit());

            if (Products == null || !Products.Any())
            {
                throw new ArgumentException("No appropriate Revit versions found on this machine for testing.");
            }

            if (String.IsNullOrEmpty(RevitPath))
            {
                RevitPath = Path.Combine(Products.First().InstallLocation, "revit.exe");
            }
        }

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
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
                var folder = Path.GetFullPath(Path.Combine(prevFolder, @"..\"));
                asmToCheck = folder + "\\" + name.Name + ".dll";
                if (File.Exists(asmToCheck))
                {
                    return Assembly.ReflectionOnlyLoadFrom(asmToCheck);
                }

                // If we can't find it in this directory, search
                // all sub-directories that aren't the previous folder
                var di = new DirectoryInfo(folder);
                foreach (var d in di.GetDirectories("*", SearchOption.AllDirectories).Where(d => d.FullName != folder))
                {
                    var subfolderCheck = d.FullName + "\\" + name.Name + ".dll";
                    if (File.Exists(subfolderCheck))
                    {
                        return Assembly.ReflectionOnlyLoadFrom(subfolderCheck);
                    }
                }
            }

            // Finally, check the runtime directory
            var runtime = RuntimeEnvironment.GetRuntimeDirectory();
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
            var revitCheck = Path.GetDirectoryName(RevitPath) + "\\" + name.Name + ".dll";
            if (File.Exists(revitCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(revitCheck);
            }

            return null;
        }

        private void SetupIndividualTests(IEnumerable<ITestData> data, bool continuous)
        {
            foreach (var td in data)
            {
                if (cancelRequested)
                {
                    cancelRequested = false;
                    break;
                }

                SetupIndividualTest(td, continuous);
            }
        }

        private void ProcessTest(ITestData td, string journalPath)
        {
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
                        td.TestStatus = TestStatus.Failure;
                        OnTestTimedOut(td);

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

            if (!timedOut)
            {
                OnTestComplete(td);
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Run tests through only on journal file.
        /// There may be one test case to cause the running process hang.
        /// Then the journal file will be recreated based on the remaining test cases
        /// and this function will be called for the new journaling file.
        /// </summary>
        /// <param name="journalPath"></param>
        /// <param name="firstCall"></param>
        private void ProcessBatchTests(string journalPath, bool firstCall = true)
        {
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
            
            if (!WaitForTestsToComplete(process))
            {
                var tests = GetRunnableTests();
                var remainingTests = tests.Where(x => !x.Completed);
                if (remainingTests.Any())
                {
                    CreateJournalForTestCases(remainingTests);
                    ProcessBatchTests(journalPath, false);
                }
            }

            if (firstCall)
            {
                // If there are any test cases which are timed out, write them to the result file
                var runnableTests = GetRunnableTests();
                var timedoutTests = runnableTests.Where(x => x.TestStatus == TestStatus.TimedOut);
                if (timedoutTests.Any())
                {
                    WriteTestResultForTestCases(timedoutTests, Results, TestAssembly);
                }

                OnTestComplete(GetRunnableTests());
            }
        }

        /// <summary>
        /// Wait for the process to complete the test cases.
        /// If the result is true, this means that all the tests have completed or the test has been cancelled.
        /// If the result is false, this means tests have not completed and need to start a new process to
        /// continue running the remaining test cases.
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private bool WaitForTestsToComplete(Process process)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            List<string> completedTestCaseNames = new List<string>();
            string runningTestCaseName = string.Empty;
            string runningFixtureName = string.Empty;
            ITestData runningTestCase = null;
            while (true)
            {
                MessageResult msgResult = RevitTestServer.Instance.GetNextMessageResult();
                if (watch.ElapsedMilliseconds > Timeout || msgResult.Status != MessageStatus.Success)
                {
                    // If the test case has timed out or there are errors when getting the next message
                    if (process != null && !(process.HasExited))
                    {
                        process.Kill();
                    }
                    RevitTestServer.Instance.ResetWorkingSocket();
                    if (runningTestCase != null)
                    {
                        runningTestCase.Completed = true;
                        runningTestCase.TestStatus = TestStatus.TimedOut;
                        OnTestTimedOut(runningTestCase);
                    }
                    return false;
                }
                else if (cancelRequested)
                {
                    // If the cancel button has been clicked
                    cancelRequested = false;
                    if (process != null && !(process.HasExited))
                    {
                        process.Kill();
                    }
                    RevitTestServer.Instance.ResetWorkingSocket();
                    Console.WriteLine("Test is cancelled");
                    return true;
                }

                var ctrlMessage = msgResult.Message as ControlMessage;
                var dataMessage = msgResult.Message as DataMessage;

                if (ctrlMessage != null || dataMessage != null)
                {
                    if (!string.IsNullOrEmpty(runningTestCaseName))
                    {
                        completedTestCaseNames.Add(runningTestCaseName);
                        if (runningTestCase != null)
                        {
                            runningTestCase.Completed = true;
                        }
                    }
                }

                if (ctrlMessage != null)
                {
                    if (ctrlMessage.Type == ControlType.NotificationOfEnd)
                    {
                        // Wait for the process to exit so that the temporary journaling files
                        // can be deleted successfully in the cleanup stage later
                        process.WaitForExit();
                        return true;
                    }
                }

                if (dataMessage != null)
                {
                    runningTestCaseName = dataMessage.TestCaseName;
                    runningFixtureName = dataMessage.FixtureName;
                    runningTestCase = FindTestCase(runningTestCaseName, runningFixtureName);
                    Console.WriteLine("Running {0} in {1}", runningTestCaseName, runningFixtureName);

                    watch.Reset();
                    watch.Start();
                }
            }
        }

        private ITestData FindTestCase(string testCaseName, string fixtureName)
        {
            var tests = GetRunnableTests().Where(x => string.CompareOrdinal(x.Name, testCaseName) == 0 &&
                            string.CompareOrdinal(x.Fixture.Name, fixtureName) == 0);
            return tests.First();
        }

        private static testsuiteType CreateTestSuiteType(string suiteName)
        {
            return new testsuiteType
            {
                name = suiteName,
                description = "Unit tests in Revit.",
                time = "0.0",
                type = "TestFixture",
                result = "Success",
                executed = "True",
                results = new resultsType { Items = new object[] { } }
            };
        }

        private static resultType GetInitializedResultType(string testAssembly)
        {
            var result = new resultType
                                {
                                    name = testAssembly,
                                    testsuite = CreateTestSuiteType("DynamoTestFrameworkTests")
                                };

            result.date = DateTime.Now.ToString("yyyy-MM-dd");
            result.time = DateTime.Now.ToString("HH:mm:ss");
            result.failures = 0;
            result.ignored = 0;
            result.notrun = 0;
            result.errors = 0;
            result.skipped = 0;
            result.inconclusive = 0;
            result.invalid = 0;

            return result;
        }

        private void OnInitialized()
        {
            if (Initialized != null)
            {
                Initialized(this, EventArgs.Empty);
            }
        }

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
                TestComplete(new List<ITestData>(){data}, Results);
            }
        }

        private void OnTestComplete(IEnumerable<ITestData> data)
        {
            if (TestComplete != null)
            {
                TestComplete(data, Results);
            }
        }

        private void OnTestTimedOut(ITestData data)
        {
            if (TestTimedOut != null)
            {
                TestTimedOut(data);
            }
        }

        private void OnTestFailed(ITestData data, string message, string stackTrace)
        {
            if (TestFailed != null)
            {
                TestFailed(data, message, stackTrace);
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

                tw.Write(journal);
                tw.Flush();
            }
        }

        private void InitializeJournal(string path, int port)
        {
            if (journalInitialized) return;

            using (var tw = new StreamWriter(path, false))
            {
                var journal = String.Format(@"'" +
                                             "Dim Jrn \n" +
                                             "Set Jrn = CrsJournalScript \n" +
                                             "Jrn.RibbonEvent \"Execute external command:{0}:{1}\" \n" +
                                             "Jrn.Data \"APIStringStringMapJournalData\", 1, \"Port\", \"{2}\" \n",
                                             ClientStartPluginGuid, ClientStartPluginClass, port);

                tw.Write(journal);
                tw.Flush();
            }

            journalInitialized = true;
        }

        private void AddToJournal(string path, string testName, string fixtureName, string assemblyPath, string resultsPath, string modelPath)
        {
            using (var tw = new StreamWriter(path, true))
            {
                var journal = String.Format("Jrn.Command \"StartupPage\" , \"Open this project , ID_FILE_MRU_FIRST\" \n" +
                                            "Jrn.Data \"MRUFileName\"  , \"{0}\" \n" +
                                            "Jrn.RibbonEvent \"Execute external command:{1}:{2}\" \n" +
                                            "Jrn.Data \"APIStringStringMapJournalData\", 6, \"testName\", \"{3}\", \"fixtureName\", \"{4}\", \"testAssembly\", \"{5}\", \"resultsPath\", \"{6}\", \"debug\",\"{7}\",\"workingDirectory\",\"{8}\" \n" +
                                            "Jrn.Command \"Internal\" , \"Flush undo and redo stacks , ID_FLUSH_UNDO\" \n" +
                                            "Jrn.Command \"Internal\" , \"Close the active project , ID_REVIT_FILE_CLOSE\" \n",
                    modelPath, PluginGuid, PluginClass, testName, fixtureName, assemblyPath, resultsPath, IsDebug, WorkingDirectory);

                tw.Write(journal);
                tw.Flush();
            }

        }

        private void FinishJournal(string path)
        {
            if (journalFinished) return;

            using (var tw = new StreamWriter(path, true))
            {
                var journal = String.Format("Jrn.RibbonEvent \"Execute external command:{0}:{1}\" \n" +
                                            "Jrn.Command \"SystemMenu\" , \"Quit the application; prompts to save projects , ID_APP_EXIT\"",
                              ClientEndPluginGuid, ClientEndPluginClass);

                tw.Write(journal);
                tw.Flush();
            }
            journalFinished = true;
        }

        /// <summary>
        /// Setup all tests in a selected assembly.
        /// </summary>
        /// <param name="ad"></param>
        /// <param name="continuous"></param>
        private void 
            SetupAssemblyTests(IAssemblyData ad, bool continuous = false)
        {
            if (ad.ShouldRun == false)
                return;

            foreach (var fix in ad.Fixtures)
            {
                if (cancelRequested)
                {
                    cancelRequested = false;
                    break;
                }

                SetupFixtureTests(fix as IFixtureData, continuous);
            }
        }

        /// <summary>
        /// Setup all tests in a selected fixture.
        /// </summary>
        /// <param name="fd"></param>
        /// <param name="continuous"></param>
        private void SetupFixtureTests(IFixtureData fd, bool continuous = false)
        {
            if (fd.ShouldRun == false)
                return;

            SetupIndividualTests(fd.Tests.ToList(), continuous);
        }

        /// <summary>
        /// Setup all tests in a selected category.
        /// </summary>
        /// <param name="cd">The category</param>
        /// <param name="continuous">Run continously</param>
        private void SetupCategoryTests(ICategoryData cd, bool continuous = false)
        {
            if (cd.ShouldRun == false)
                return;

            SetupIndividualTests(cd.Tests, continuous);
        }

        /// <summary>
        /// Setup the selected test.
        /// </summary>
        /// <param name="td"></param>
        /// <param name="continuous"></param>
        private void SetupIndividualTest(ITestData td, bool continuous = false)
        {
            if (td.ShouldRun == false)
                return;

            try
            {
                if (!File.Exists(td.ModelPath))
                {
                    var message = String.Format("Specified model path: {0} does not exist.", td.ModelPath);
                    throw new Exception(message);
                }

                if (!File.Exists(td.Fixture.Assembly.Path))
                {
                    throw new Exception(String.Format("The specified assembly: {0} does not exist.",
                        td.Fixture.Assembly.Path));
                }

                if (!File.Exists(AssemblyPath))
                {
                    throw new Exception(
                        String.Format("The specified RTF assembly does not exist: {0} does not exist.",
                            td.Fixture.Assembly.Path));
                }

                var journalPath = Path.Combine(WorkingDirectory, td.Name + ".txt");

                // If we're running in continuous mode, then all tests will share
                // the same journal path
                if (continuous)
                {
                    journalPath = BatchJournalPath;
                }
                td.JournalPath = journalPath;

                if (continuous)
                {
                    InitializeJournal(journalPath, RevitTestServer.Instance.Port);
                    AddToJournal(journalPath, td.Name, td.Fixture.Name, td.Fixture.Assembly.Path, Results, td.ModelPath);
                }
                else
                {
                    CreateJournal(journalPath, td.Name, td.Fixture.Name, td.Fixture.Assembly.Path, Results, td.ModelPath);
                }

            }
            catch (Exception ex)
            {
                if (td == null) return;
                td.TestStatus = TestStatus.Failure;
            }
        }

        /// <summary>
        /// This function returns the current Revit addin folder
        /// </summary>
        /// <returns></returns>
        private string GetRevitAddinFolder()
        {
            var prod =
                    Products.FirstOrDefault(
                        x =>
                            System.String.CompareOrdinal(
                            Path.GetDirectoryName(x.InstallLocation), Path.GetDirectoryName(RevitPath)) ==
                            0);
            return prod.AllUsersAddInFolder;
        }

        /// <summary>
        /// This function deletes the addin files in the current working directory
        /// </summary>
        private void DeleteAddins()
        {
            if (!CopiedAddins.Any() || CopiedAddins == null)
                return;

            foreach (var file in CopiedAddins.Select(addin => Path.Combine(WorkingDirectory, addin)))
            {
                File.Delete(file);
            }
            CopiedAddins.Clear();
        }

        #endregion

        #region private static methods

        private static void CreateAddin(string addinPath, string assemblyPath)
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

                    "<AddIn Type=\"Command\">\n" +
                    "<Name>Start Test Framework Client</Name>\n" +
                    "<Assembly>\"{0}\"</Assembly>\n" +
                    "<AddInId>{5}</AddInId>\n" +
                    "<FullClassName>{6}</FullClassName>\n" +
                    "<VendorId>Dynamo</VendorId>\n" +
                    "<VendorDescription>Dynamo</VendorDescription>\n" +
                    "</AddIn>\n" +

                    "<AddIn Type=\"Command\">\n" +
                    "<Name>End Test Framework Client</Name>\n" +
                    "<Assembly>\"{0}\"</Assembly>\n" +
                    "<AddInId>{7}</AddInId>\n" +
                    "<FullClassName>{8}</FullClassName>\n" +
                    "<VendorId>Dynamo</VendorId>\n" +
                    "<VendorDescription>Dynamo</VendorDescription>\n" +
                    "</AddIn>\n" +

                    "</RevitAddIns>",
                    assemblyPath, _appGuid, _appClass, _pluginGuid, _pluginClass,
                    _clientStartPluginGuid, _clientStartPluginClass,
                    _clientEndPluginGuid, _clientEndPluginClass
                    );

                tw.Write(addin);
                tw.Flush();
            }
        }

        public static void Runner_TestTimedOut(ITestData data)
        {
            data.ResultData.Clear();
            data.ResultData.Add(new ResultData() { Message = "Test timed out." });
        }

        public static void Runner_TestFailed(ITestData data, string message, string stackTrace)
        {
            data.ResultData.Clear();
            data.ResultData.Add(new ResultData() { Message = message, StackTrace = stackTrace });
        }

        #region XML reading and writing
        public static resultType TryParseResultsOrEmitError(string resultsPath)
        {
            try
            {
                return TestResultDeserializer.DeserializeResults(resultsPath);
            }
            catch (InvalidOperationException e) // xml parser failure
            {
                //td.TestStatus = TestStatus.Error;
                //runner_TestFailed(td, "RevitTestExecutive failed to complete the test!", TestResultDeserializer.TryGetFailureMessage(resultsPath));
                return null;
            }
        }

        public static void GetTestResultStatus(IEnumerable<ITestData> data, string resultsPath)
        {
            // Try to get the results, if fail, short-circuit
            var results = TryParseResultsOrEmitError(resultsPath);
            if (results == null) return;

            foreach (var td in data)
            {
                td.ResultData.Clear();

                var ourTests = FindOutTestCasesRelatedToTestData(results, td);

                if (ourTests==null || !ourTests.Any())
                {
                    return;
                }

                foreach (var ourTest in ourTests)
                {
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
                        case "Timedout":
                            td.TestStatus = TestStatus.TimedOut;
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

                    if (ourTest.Item == null) continue;

                    var failure = ourTest.Item as failureType;
                    if (failure == null) return;

                    td.ResultData.Add(
                            new ResultData()
                            {
                                StackTrace = failure.stacktrace,
                                Message = ourTest.name + ":" + failure.message
                            });
                }
            }

        }

        public static void WriteTestResultForTestCases(IEnumerable<ITestData> data, string resultsPath, string testAssembly)
        {
            var results = TryParseResultsOrEmitError(resultsPath);
            if (results == null)
            {
                // In case, there is no other test case which has run yet
                results = GetInitializedResultType(testAssembly);
            }

            List<ITestData> timedoutTests = new List<ITestData>();
            foreach (var td in data)
            {
                if (td.TestStatus == TestStatus.TimedOut)
                {
                    timedoutTests.Add(td);
                }
            }

            foreach (var td in timedoutTests)
            {
                var tests = FindOutTestCasesRelatedToTestData(results, td);
                if (tests != null && tests.Any())
                {
                    // update the test result
                    foreach (var test in tests)
                    {
                        test.executed = "True";
                        test.success = "False";
                        test.result = "Timedout";
                    }
                }
                else
                {
                    var suite =
                        results.testsuite.results.Items
                            .Cast<testsuiteType>()
                            .FirstOrDefault(s => s.name == td.Fixture.Name);

                    // To expand the existing array, for now it is to create a new array.
                    // We need to find a way to replace array with list.
                    if (suite == null)
                    {
                        var subSuite = CreateTestSuiteType(td.Fixture.Name);
                        int newSize = results.testsuite.results.Items.Length + 1;
                        object[] newArray = new object[newSize];
                        Array.Copy(results.testsuite.results.Items, newArray, newSize - 1);
                        newArray[newSize - 1] = subSuite;
                        results.testsuite.results.Items = newArray;
                        suite = subSuite;
                    }

                    int size = suite.results.Items.Length + 1;
                    object[] array = new object[size];
                    Array.Copy(suite.results.Items, array, size - 1);
                    array[size - 1] = new testcaseType() { name = td.Name, executed = "True", success = "False", result = "Timedout" };
                    suite.results.Items = array;
                }
            }

            //update the file
            var serializer = new XmlSerializer(typeof(resultType));
            var dir = Path.GetDirectoryName(resultsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            using (var tw = XmlWriter.Create(resultsPath, new XmlWriterSettings() { Indent = true }))
            {
                tw.WriteComment("This file represents the results of running a test suite");
                var ns = new XmlSerializerNamespaces();
                ns.Add("", "");
                serializer.Serialize(tw, results, ns);
            }
        }

        private static IEnumerable<testcaseType> FindOutTestCasesRelatedToTestData(resultType results, ITestData td)
        {
            var ourSuite =
                results.testsuite.results.Items
                    .Cast<testsuiteType>()
                    .FirstOrDefault(s => s.name == td.Fixture.Name);

            if (ourSuite == null)
            {
                return null;
            }

            // parameterized tests will have multiple results
            return ourSuite.results.Items.Cast<testcaseType>()
                .Where(t => string.CompareOrdinal(t.name, td.Name) == 0);
        }

        #endregion

        public virtual IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory, GroupingType groupType, bool isTesting)
        {
            Console.WriteLine("Reading assembly: {0}", assemblyPath);

            IList<IAssemblyData> data = new List<IAssemblyData>();

            try
            {
                AssemblyLoader loader;
                AssemblyData assData;

                var product = _products[_selectedProduct];
                var revitDirectory = product.InstallLocation;
                var resolver = new DefaultAssemblyResolver(revitDirectory, AdditionalResolutionDirectories);

                if (!isTesting)
                {
                    // Create a temporary application domain to load the assembly.
                    var tempDomain = AppDomain.CreateDomain("RTF_Domain");
                    loader = (AssemblyLoader)tempDomain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location, "RTF.Framework.AssemblyLoader", false, 0, null, new object[] { resolver }, CultureInfo.InvariantCulture, null);
                    assData = loader.ReadAssembly(assemblyPath, groupType, workingDirectory);
                    data.Add(assData);
                    AppDomain.Unload(tempDomain);
                }
                else
                {
                    loader = new AssemblyLoader(resolver);
                    assData = loader.ReadAssembly(assemblyPath, groupType, workingDirectory);
                    data.Add(assData);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("The specified assembly could not be loaded for testing. Try adding some additonal resolution paths so RTF can find referenced assemblies.");
                
                return null;
            }

            return data;
        }

        /// <summary>
        /// Flag tests to be excluded.
        /// </summary>
        /// <param name="excludeCategory"></param>
        /// <param name="assData"></param>
        private static void MarkExclusions(string excludeCategory, IEnumerable<IAssemblyData> assData)
        {
            if (String.IsNullOrEmpty(excludeCategory))
                return;

            var excludeCat = assData.SelectMany(x => x.Categories).Where(c => c.Name == excludeCategory).Cast<IExcludable>();
            foreach (var cat in excludeCat)
            {
                cat.ShouldRun = false;
            }
        }

        #endregion

        #region public static methods

        public static IRunnerSetupData ParseCommandLineArguments(IEnumerable<string> args)
        {
            var showHelp = false;

            var setupData = new RunnerSetupData();

            var p = new OptionSet()
            {
                {"dir=","The full path to the working directory. The working directory is the directory in which RTF will generate the journal and the addin to Run Revit. Revit's run-by-journal capability requires that all addins which need to be loaded are in the same directory as the journal file. So, if you're testing other addins on top of Revit using RTF, you'll need to put those addins in whatever directory you specify as the working directory.", v=> setupData.WorkingDirectory = Path.GetFullPath(v)},
                {"a=|assembly=", "The full path to the assembly containing your tests.", v => setupData.TestAssembly = Path.GetFullPath(v)},
                {"r=|results=", "The full path to an .xml file that will contain the results. ", v=>setupData.Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of a test fixture to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly.(OPTIONAL)", v => setupData.Fixture = v},
                {"t:|testName:", "The name of a test to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly. (OPTIONAL)", v => setupData.Test = v},
                {"category:", "The name of a test category to run. If no fixture, no category and no test names are specified, RTF will run all tests in the assembly. (OPTIONAL)", v=> setupData.Category = v},
                {"exclude:", "The name of a test category to exclude. This has a higher priortiy than other settings. If a specified category is set here, any test cases that belongs to that category will not be run. (OPTIONAL)", v=> setupData.ExcludedCategory = v},
                {"c|concatenate", "Concatenate the results from this run of RTF with an existing results file if one exists at the path specified. The default behavior is to replace the existing results file. (OPTIONAL)", v=> setupData.Concat = v != null},
                {"revit:", "The Revit executable to be used for testing. If no executable is specified, RTF will use the first version of Revit that is found on the machine using the RevitAddinUtility. (OPTIONAL)", v=> setupData.RevitPath = v},
                {"add:", @"Additional path resolution directories as a string separated by ';'", v=>setupData.AdditionalResolutionDirectories=v},
                {"copyAddins", "Specify whether to copy the addins from the Revit folder to the current working directory. Copying the addins from the Revit folder will cause the test process to simulate the typical setup on your machine. (OPTIONAL)",
                    v=> setupData.CopyAddins = v != null},
                {"dry", "Conduct a dry run. (OPTIONAL)", v=> setupData.DryRun = v != null},
                {"x|clean", "Cleanup journal files after test completion. (OPTIONAL)", v=> setupData.CleanUp = v != null},
                {"continuous", "Run all selected tests in one Revit session. (OPTIONAL)", v=> setupData.Continuous = v != null},
                {"time", "The time, in milliseconds, after which RTF will close the testing process automatically. (OPTIONAL)", v=>setupData.Timeout = Int32.Parse(v)},
                {"d|debug", "Should RTF attempt to attach to a debugger?. (OPTIONAL)", v=>setupData.IsDebug = v != null},
                {"h|help", "Show this message and exit. (OPTIONAL)", v=> showHelp = v != null}
            };

            var notParsed = new List<string>();

            const string helpMessage = "Try 'RevitTestFrameworkConsole --help' for more information.";

            try
            {
                notParsed = p.Parse(args);
            }
            catch (OptionException e)
            {
                string message = e.Message + "\n" + helpMessage;
                throw new Exception(message);
            }

            if (notParsed.Count > 0)
            {
                throw new ArgumentException(String.Join(" ", notParsed.ToArray()));
            }

            if (showHelp)
            {
                ShowHelp(p);
                throw new Exception();
            }

            if (string.IsNullOrEmpty(setupData.WorkingDirectory))
            {
                throw new Exception("You must specify a working directory.");
            }

            if (string.IsNullOrEmpty(setupData.TestAssembly))
            {
                throw new Exception("You must specify a test assembly.");
            }

            if (string.IsNullOrEmpty(setupData.Results))
            {
                throw new Exception("You must specify a results file.");
            }

            return setupData;
        }

        public static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: DynamoTestFrameworkRunner [OPTIONS]");
            Console.WriteLine("Run a test or a fixture of tests from an assembly.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        #endregion
    }

    public class SelectionHint
    {
        public string AssemblyName { get; set; }
        public string FixtureName { get; set; }
        public string TestName { get; set; }

        public SelectionHint()
        {
            // used for serialization
        }

        public SelectionHint(string asmName, string fixName, string testName)
        {
            AssemblyName = asmName;
            FixtureName = fixName;
            TestName = testName;
        }
    }
}
