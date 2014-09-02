using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.RevitAddIns;
using Microsoft.Practices.Prism;
using Microsoft.Practices.Prism.ViewModel;

namespace RTF.Framework
{
    public delegate void TestCompleteHandler(IList<ITestData> data, string resultsPath);
    public delegate void TestTimedOutHandler(ITestData data);
    public delegate void TestFailedHandler(ITestData data, string message, string stackTrace);

    /// <summary>
    /// The Runner model.
    /// </summary>
    public class Runner : NotificationObject
    {
        #region events

        public event EventHandler TestRunsComplete;
        public event TestCompleteHandler TestComplete;
        public event TestTimedOutHandler TestTimedOut;
        public event TestFailedHandler TestFailed;

        #endregion

        #region private members

        private string _testAssembly;
        private string _test;
        private string _fixture;
        private string _category;
        private bool _isDebug;
        private string _results;
        private const string _pluginGuid = "487f9ff0-5b34-4e7e-97bf-70fbff69194f";
        private const string _pluginClass = "RTF.Applications.RevitTestFramework";
        private const string _appGuid = "c950020f-3da0-4e48-ab82-5e30c3f4b345";
        private const string _appClass = "RTF.Applications.RevitTestFrameworkExternalApp";
        private string _workingDirectory;
        private bool _gui = true;
        private string _revitPath;
        private bool _copyAddins = false;
        private Dictionary<ITestData, string> testDictionary = new Dictionary<ITestData, string>();
        private int _runCount = 0;
        private int _timeout = 120000;
        private bool _concat;
        private string _addinPath;
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
        private string batchJournalPath;
        private bool continuous;
        private bool journalInitialized = false;
        private bool journalFinished;
        private GroupingType groupingType;

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

        /// <summary>
        /// The path of the RTF addin file.
        /// </summary>
        internal string AddinPath
        {
            get { return _addinPath; }
            set { _addinPath = value; }
        }

        /// <summary>
        /// A dictionary which stores a test data object
        /// and a journal path.
        /// </summary>
        internal Dictionary<ITestData, string> TestDictionary
        {
            get { return testDictionary; }
            set { testDictionary = value; }
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
        /// A counter for the number of runs processed.
        /// </summary>
        public int RunCount
        {
            get { return _runCount; }
            set { _runCount = value; }
        }

        /// <summary>
        /// The path of the selected assembly for testing.
        /// </summary>
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

        /// <summary>
        /// A collection of available Revit products for testing.
        /// </summary>
        public ObservableCollection<RevitProduct> Products
        {
            get { return _products; }
            set
            {
                _products = value;
                RaisePropertyChanged("Products");
            }
        }

        /// <summary>
        /// A flag which can be used to specifi
        /// </summary>
        public bool Gui
        {
            get { return _gui; }
            set { _gui = value; }
        }

        /// <summary>
        /// The selected Revit application against which
        /// to test.
        /// </summary>
        public int SelectedProduct
        {
            get { return _selectedProduct; }
            set
            {
                _selectedProduct = value;
                RaisePropertyChanged("SelectedProduct");
            }
        }

        /// <summary>
        /// The name of the test to run.
        /// </summary>
        public string Test
        {
            get { return _test; }
            set { _test = value; }
        }

        /// <summary>
        /// The name of the assembly to run.
        /// </summary>
        public string TestAssembly
        {
            get { return _testAssembly; }
            set { _testAssembly = value; }
        }

        /// <summary>
        /// The name of the fixture to run.
        /// </summary>
        public string Fixture
        {
            get { return _fixture; }
            set { _fixture = value; }
        }

        /// <summary>
        /// The name of the category to run.
        /// </summary>
        public string Category
        {
            get { return _category; }
            set { _category = value; }
        }

        /// <summary>
        /// A flag which, when set, allows you
        /// to attach to the debugger.
        /// </summary>
        public bool IsDebug
        {
            get { return _isDebug; }
            set { _isDebug = value; }
        }

        /// <summary>
        /// The path to the results file.
        /// </summary>
        public string Results
        {
            get { return _results; }
            set { _results = value; }
        }

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

                AddinPath = Path.Combine(WorkingDirectory, "RevitTestFramework.addin");
                batchJournalPath = Path.Combine(WorkingDirectory, "RTF_Batch_Test.txt");
            }
        }

        /// <summary>
        /// The path to the version of Revit to be
        /// used for testing.
        /// </summary>
        public string RevitPath
        {
            get { return _revitPath; }
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
        public bool Concat
        {
            get { return _concat; }
            set { _concat = value; }
        }

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

        public GroupingType GroupingType
        {
            get { return groupingType; }
            set
            {
                groupingType = value;
                if (value == GroupingType.Category)
                {
                    foreach (var asm in Assemblies)
                    {
                        asm.SortingGroup = asm.Categories;
                    }
                }
                else if (value == GroupingType.Fixture)
                {
                    foreach (var asm in Assemblies)
                    {
                        asm.SortingGroup = asm.Fixtures;
                    }
                }
            }
        }

        #endregion

        #region constructors

        public Runner()
        {
            GroupingType = GroupingType.Fixture;
            
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

            AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"RTFRevit.dll");
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

        public void SetupAssemblyTests(IAssemblyData ad, bool continuous = false)
        {
            foreach (var fix in ad.Fixtures)
            {
                if (cancelRequested)
                {
                    cancelRequested = false;
                    break;
                }

                SetupFixtureTests(fix as IFixtureData);
            }
        }

        public void SetupFixtureTests(IFixtureData fd, bool continuous = false)
        {
            foreach (var td in fd.Tests)
            {
                if (cancelRequested)
                {
                    cancelRequested = false;
                    break;
                }

                SetupIndividualTest(td, continuous);
            }
        }

        public void SetupCategoryTests(ICategoryData cd, bool continuous = false)
        {
            foreach (var td in cd.Tests)
            {
                if (cancelRequested)
                {
                    cancelRequested = false;
                    break;
                }

                SetupIndividualTest(td, continuous);
            }
        }

        public void SetupIndividualTest(ITestData td, bool continuous = false)
        {
            try
            {
                if (!File.Exists(td.ModelPath))
                {
                    throw new Exception(string.Format("Specified model path: {0} does not exist.", td.ModelPath));
                }

                if (!File.Exists(td.Fixture.Assembly.Path))
                {
                    throw new Exception(string.Format("The specified assembly: {0} does not exist.",
                        td.Fixture.Assembly.Path));
                }

                if (!File.Exists(AssemblyPath))
                {
                    throw new Exception(
                        string.Format("The specified revit app assembly does not exist: {0} does not exist.",
                            td.Fixture.Assembly.Path));
                }

                var journalPath = Path.Combine(WorkingDirectory, td.Name + ".txt");
                
                // If we're running in continuous mode, then all tests will share
                // the same journal path
                if (continuous)
                {
                    journalPath = batchJournalPath;
                }
                testDictionary.Add(td, journalPath);

                if (continuous)
                {
                    InitializeJournal(journalPath);
                    //var resultsTmp = Path.GetFileNameWithoutExtension(Results);
                    //var resultsDir = Path.GetDirectoryName(Results);
                    //var resultsPath = Path.Combine(resultsDir, resultsTmp + "_" + Guid.NewGuid() + ".xml");
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

                // Write a null journal path to the dictionary
                // for failed tests.
                if (TestDictionary.ContainsKey(td))
                {
                    TestDictionary[td] = null;
                }
                else
                {
                    TestDictionary.Add(td, null);
                }
            }
        }

        public void RunAllTests()
        {
            if (continuous && !journalFinished)
            {
                FinishJournal(batchJournalPath);
            }

            // Kill any senddmp.exe processes thrown off
            // by previous failed revit sessions
            var sendDmps = Process.GetProcessesByName("senddmp");
            if (sendDmps.Any())
            {
                sendDmps.ToList().ForEach(sd => sd.Kill());
            }

            if (!File.Exists(AddinPath))
            {
                CreateAddin(AddinPath, AssemblyPath);
            }

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

            if (dryRun) return;

            RunCount = continuous ? 1 : testDictionary.Count;

            if (continuous)
            {
                // If running in continous mode, there will only
                // be one journal file, as the value for every test
                ProcessBatchTests(batchJournalPath);
            }
            else
            {
                foreach (var kvp in TestDictionary)
                {
                    if (kvp.Value == null) continue;

                    var td = kvp.Key;
                    try
                    {
                        ProcessTest(kvp.Key, kvp.Value);
                        RunCount--;
                    }
                    catch (Exception ex)
                    {
                        if (td == null) continue;
                        td.TestStatus = TestStatus.Failure;
                        OnTestFailed(td, ex.Message, ex.StackTrace);
                    }
                }
            }

            OnTestRunsComplete();
        }

        public void Refresh()
        {
            Assemblies.Clear();
            if (File.Exists(TestAssembly))
            {
                Assemblies.AddRange(ReadAssembly(TestAssembly, _workingDirectory, groupingType));
            }
           
            Console.WriteLine(ToString());
        }

        public void Cleanup()
        {
            if (!CleanUp)
                return;

            try
            {
                foreach (var kvp in TestDictionary)
                {
                    var path = kvp.Value;
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                TestDictionary.Clear();

                var journals = Directory.GetFiles(WorkingDirectory, "journal.*.txt");
                foreach (var journal in journals)
                {
                    File.Delete(journal);
                }

                DeleteAddins();
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

            if (!timedOut && Gui)
            {
                OnTestComplete(td);
            }
        }

        private void ProcessBatchTests(string journalPath)
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
                OnTestComplete(TestDictionary.Keys.ToList());
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

        private void OnTestComplete(IList<ITestData> data)
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

        private void InitializeJournal(string path)
        {
            if (journalInitialized) return;

            using (var tw = new StreamWriter(path, false))
            {
                var journal = @"'" +
                              "Dim Jrn \n" +
                              "Set Jrn = CrsJournalScript \n";

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
                var journal = "Jrn.Command \"SystemMenu\" , \"Quit the application; prompts to save projects , ID_APP_EXIT\"";

                tw.Write(journal);
                tw.Flush();
            }
            journalFinished = true;
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
                .Where(x => x.Version == RevitVersion.Revit2014).ToList();

            if (!products.Any())
            {
                Console.WriteLine("No versions of revit could be found");
                return null;
            }

            return products;
        }

        /// <summary>
        /// This function returns the current Revit addin folder
        /// </summary>
        /// <returns></returns>
        private string GetRevitAddinFolder()
        {
            return Products[SelectedProduct].AllUsersAddInFolder;
        }

        /// <summary>
        /// This function deletes the addin files in the current working directory
        /// </summary>
        private void DeleteAddins()
        {
            foreach (var addin in CopiedAddins)
            {
                var file = Path.Combine(WorkingDirectory, addin);
                File.Delete(file);
            }
            CopiedAddins.Clear();
        }

        public static IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory, GroupingType groupType)
        {
            IList<IAssemblyData> data = new List<IAssemblyData>();

            try
            {
                var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);

                var assData = new AssemblyData(assemblyPath, assembly.GetName().Name, groupType);
                data.Add(assData);
                
                foreach (var fixtureType in assembly.GetTypes())
                {
                    if (!ReadFixture(fixtureType, assData, workingDirectory))
                    {
                        //Console.WriteLine(string.Format("Journals could not be created for {0}", fixtureType.Name));
                    }
                }

                assData.Fixtures = assData.Fixtures.Sorted(x => x.Name);
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

            // sort the collection
            fixData.Tests = fixData.Tests.Sorted(x => x.Name);

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

            var category = "";
            var categoryAttrib =
                testAttribs.FirstOrDefault(
                    x => x.Constructor.DeclaringType.Name == "CategoryAttribute");
            if (categoryAttrib != null)
            {
                category = categoryAttrib.ConstructorArguments.FirstOrDefault().Value.ToString();
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

            if (!string.IsNullOrEmpty(category))
            {
                var cat = data.Assembly.Categories.FirstOrDefault(x => x.Name == category);
                if (cat != null)
                {
                    cat.Tests.Add(testData);
                }
                else
                {
                    var catData = new CategoryData(category);
                    catData.Tests.Add(testData);
                    data.Assembly.Categories.Add(catData);
                }
            }

            return true;
        }

        #endregion
    }

    public class AssemblyData : IAssemblyData
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public ObservableCollection<IGroupable> SortingGroup { get; set; }
        public ObservableCollection<IGroupable> Fixtures { get; set; }
        public ObservableCollection<IGroupable> Categories { get; set; }

        public AssemblyData(string path, string name, GroupingType groupType)
        {
            Fixtures = new ObservableCollection<IGroupable>();
            Categories = new ObservableCollection<IGroupable>();

            switch (groupType)
            {
                case GroupingType.Category:
                    SortingGroup = Categories;
                    break;
                case GroupingType.Fixture:
                    SortingGroup = Fixtures;
                    break;
            }

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

        public bool ModelExists
        {
            get { return ModelPath != null &&  File.Exists(ModelPath); }
        }

        public string ModelPathMessage
        {
            get
            {
                return ModelExists ? 
                    "The selected test model exists." : 
                    "The selected test model does not exist. Check your working directory.";
            }
        }

        public string ShortModelPath
        {
            get
            {
                if (string.IsNullOrEmpty(ModelPath))
                {
                    return string.Empty;
                }

                var info = new FileInfo(ModelPath);
                //return string.Format("[{0}]", info.Name);
                return string.Format("[{0}]", info.FullName);
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

    public class CategoryData : NotificationObject, ICategoryData
    {
        public string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }

        public CategoryData(string name)
        {
            Name = name;
            Tests = new ObservableCollection<ITestData>();
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
