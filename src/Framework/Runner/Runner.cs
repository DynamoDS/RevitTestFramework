﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Autodesk.RevitAddIns;
using Dynamo.NUnit.Tests;
using Microsoft.Practices.Prism;
using Microsoft.Practices.Prism.Logging;
using Microsoft.Practices.Prism.ViewModel;
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
    public class Runner : NotificationObject, IRunner
    {
        #region events

        public event EventHandler TestRunsComplete;
        public event TestCompleteHandler TestComplete;
        public event TestTimedOutHandler TestTimedOut;
        public event TestFailedHandler TestFailed;

        #endregion

        #region private members

        private const string _pluginGuid = "487f9ff0-5b34-4e7e-97bf-70fbff69194f";
        private const string _pluginClass = "RTF.Applications.RevitTestFramework";
        private const string _appGuid = "c950020f-3da0-4e48-ab82-5e30c3f4b345";
        private const string _appClass = "RTF.Applications.RevitTestFrameworkExternalApp";
        private string _workingDirectory;
        private bool _gui = true;
        private string _revitPath;
        private bool _copyAddins = true;
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
        private GroupingType groupingType = GroupingType.Fixture;

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

        #endregion

        #region public properties

        /// <summary>
        /// The path of the RTF addin file.
        /// </summary>
        public string AddinPath { get; set; }

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

                AddinPath = Path.Combine(WorkingDirectory, "RevitTestFramework.addin");
                batchJournalPath = Path.Combine(WorkingDirectory, "RTF_Batch_Test.txt");
            }
        }

        /// <summary>
        /// The path to the version of Revit to be
        /// used for testing.
        /// </summary>
        public string RevitPath { get; set; }

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

        #endregion

        #region private constructors

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
                setupData.AssemblyPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "RTFRevit.dll");
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

            if (setupData.Products == null || !setupData.Products.Any())
            {
                throw new ArgumentException("No appropriate Revit versions found on this machine for testing.");
            }

            if (String.IsNullOrEmpty(setupData.RevitPath))
            {
                setupData.RevitPath = Path.Combine(setupData.Products.First().InstallLocation, "revit.exe");
            }

            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += CurrentDomain_ReflectionOnlyAssemblyResolve;

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
            Products.Clear();
            Products.AddRange(setupData.Products);
            IsTesting = setupData.IsTesting;
            ExcludedCategory = setupData.ExcludedCategory;

            Products.Clear();
            Products.AddRange(setupData.Products);
            int count = Products.Count;
            SelectedProduct = -1;
            for (int i = 0; i < count; ++i)
            {
                var location = Path.GetDirectoryName(RevitPath);
                var locationFromProduct = Path.GetDirectoryName(Products[i].InstallLocation);
                if (String.Compare(locationFromProduct, location, true) == 0)
                    SelectedProduct = i;
            }

            if (SelectedProduct == -1)
            {
                throw new Exception("Can not find a proper application to start!");
            }

            Refresh();

            if (File.Exists(Results) && !Concat)
            {
                File.Delete(Results);
            }
        }

        #endregion

        #region public static constructors

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
            var revitCheck = Path.GetDirectoryName(Products[SelectedProduct].InstallLocation) + "\\" + name.Name + ".dll";
            if (File.Exists(revitCheck))
            {
                return Assembly.ReflectionOnlyLoadFrom(revitCheck);
            }

            return null;
        }

        #endregion

        #region public methods

        /// <summary>
        /// Setup all runnable tests.
        /// </summary>
        /// <param name="parameter"></param>
        public void SetupTests()
        {
            journalInitialized = false;
            journalFinished = false;

            var runnable = GetRunnableTests();
            foreach (var test in runnable)
            {
                SetupIndividualTest(test, Continuous);
            }

            if (continuous && !journalFinished)
            {
                FinishJournal(batchJournalPath);
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
        }

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
                ProcessBatchTests(batchJournalPath);
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

        public void Refresh()
        {
            Assemblies.Clear();
            var assData = ReadAssembly(TestAssembly, WorkingDirectory, GroupingType, false);
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

        }

        public void Cleanup()
        {
            if (!CleanUp)
                return;

            try
            {
                var runnable = GetRunnableTests();
                foreach (var test in runnable)
                {
                    if (File.Exists(test.JournalPath))
                    {
                        File.Delete(test.JournalPath);
                    }
                }

                foreach (var test in GetAllTests())
                {
                    test.JournalPath = null;
                }

                var journals = Directory.GetFiles(WorkingDirectory, "journal.*.txt");
                foreach (var journal in journals)
                {
                    File.Delete(journal);
                }

                if (File.Exists(batchJournalPath))
                {
                    File.Delete(batchJournalPath);
                }

                DeleteAddins();

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

        public IEnumerable<ITestData> GetAllTests()
        {
            var tests = Assemblies.
                SelectMany(a => a.Fixtures).
                SelectMany(f => f.Tests);

            return tests;
        }

        public IEnumerable<ITestData> GetRunnableTests()
        {
            var runnable = Assemblies.
                SelectMany(a => a.Fixtures.SelectMany(f=>f.Tests)).
                Where(t => t.ShouldRun);

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

        #endregion

        #region private methods

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

            process.WaitForExit();

            if (!timedOut)
            {
                OnTestComplete(GetRunnableTests());
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
                    throw new Exception(String.Format("Specified model path: {0} does not exist.", td.ModelPath));
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
                    journalPath = batchJournalPath;
                }
                td.JournalPath = journalPath;

                if (continuous)
                {
                    InitializeJournal(journalPath);
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

                    "</RevitAddIns>",
                    assemblyPath, _appGuid, _appClass, _pluginGuid, _pluginClass
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

                //find our results in the results
                var ourSuite =
                    results.testsuite.results.Items
                        .Cast<testsuiteType>()
                        .FirstOrDefault(s => s.name == td.Fixture.Name);

                if (ourSuite == null)
                {
                    return;
                }

                // parameterized tests will have multiple results
                var ourTests = ourSuite.results.Items
                    .Cast<testcaseType>().Where(t => t.name.Contains(td.Name));

                if (!ourTests.Any())
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

        public virtual IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory, GroupingType groupType, bool isTesting)
        {
            IList<IAssemblyData> data = new List<IAssemblyData>();

            try
            {
                AssemblyLoader loader;
                AssemblyData assData;

                if (!isTesting)
                {
                    // Create a temporary application domain to load the assembly.
                    var tempDomain = AppDomain.CreateDomain("RTF_Domain");
                    loader = (AssemblyLoader)tempDomain.CreateInstanceFromAndUnwrap(Assembly.GetExecutingAssembly().Location, "RTF.Framework.AssemblyLoader", false, 0, null, new object[] { assemblyPath }, CultureInfo.InvariantCulture, null);
                    assData = loader.ReadAssembly(assemblyPath, groupType, workingDirectory);
                    data.Add(assData);
                    AppDomain.Unload(tempDomain);
                }
                else
                {
                    loader = new AssemblyLoader(assemblyPath);
                    assData = loader.ReadAssembly(assemblyPath, groupType, workingDirectory);
                    data.Add(assData);
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
                {"dir=","The path to the working directory.", v=> setupData.WorkingDirectory = Path.GetFullPath(v)},
                {"a=|assembly=", "The path to the test assembly.", v => setupData.TestAssembly = Path.GetFullPath(v)},
                {"r=|results=", "The path to the results file.", v=>setupData.Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of the test fixture.", v => setupData.Fixture = v},
                {"t:|testName:", "The name of a test to run", v => setupData.Test = v},
                {"category:", "The name of a test category to run.", v=> setupData.Category = v},
                {"exclude:", "The name of a test category to exclude.", v=> setupData.ExcludedCategory = v},
                {"c|concatenate", "Concatenate results with existing results file.", v=> setupData.Concat = v != null},
                {"revit:", "The path to Revit.", v=> setupData.RevitPath = v},
                {"copyAddins", "Specify whether to copy the addins from the Revit folder to the current working directory",
                    v=> setupData.CopyAddins = v != null},
                {"dry", "Conduct a dry run.", v=> setupData.DryRun = v != null},
                {"x|clean", "Cleanup journal files after test completion", v=> setupData.CleanUp = v != null},
                {"continuous", "Run all selected tests in one Revit session.", v=> setupData.Continuous = v != null},
                {"d|debug", "Run in debug mode.", v=>setupData.IsDebug = v != null},
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
                string message = e.Message + "\n" + helpMessage;
                throw new Exception(message);
            }

            if (notParsed.Count > 0)
            {
                throw new ArgumentException(String.Join(" ", notParsed.ToArray()));
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

            if (showHelp)
            {
                ShowHelp(p);
                throw new Exception();
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

    [Serializable]
    public class AssemblyData : NotificationObject, IAssemblyData
    {
        private bool _shouldRun = true;
        private ObservableCollection<ITestGroup> _sortingGroup;
        public virtual string Path { get; set; }
        public virtual string Name { get; set; }
        public ObservableCollection<ITestGroup> Fixtures { get; set; }
        public ObservableCollection<ITestGroup> Categories { get; set; }
        public bool IsNodeExpanded { get; set; }
        public GroupingType GroupingType { get; set; }

        public ObservableCollection<ITestGroup> SortingGroup
        {
            get
            {
                switch (GroupingType)
                {
                    case GroupingType.Category:
                        return Categories;
                    case GroupingType.Fixture:
                        return Fixtures;
                    default:
                        return null;
                }
            }
        }

        public string Summary
        {
            get
            {
                return string.Format("{0} Fixtures with {1} Tests", Fixtures.Count,
                    Fixtures.SelectMany(f => f.Tests).Count());
            }
        }

        public bool ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;

                // Set nothing to run.
                foreach (var test in Fixtures.SelectMany(f => f.Tests))
                {
                    test.ShouldRun = false;
                }

                // Set all the categories or fixtures to run
                foreach (var item in SortingGroup)
                {
                    var group = item as IExcludable;
                    if (group != null)
                    {
                        group.ShouldRun = _shouldRun; 
                    }
                }

                RaisePropertyChanged("ShouldRun");
            }
        }

        public AssemblyData()
        {
            Categories = new ObservableCollection<ITestGroup>();
            Fixtures = new ObservableCollection<ITestGroup>();
        }

        public AssemblyData(string path, string name, GroupingType groupType)
        {
            Categories = new ObservableCollection<ITestGroup>();
            Fixtures = new ObservableCollection<ITestGroup>();
            IsNodeExpanded = true;
            GroupingType = groupType;

            Path = path;
            Name = name;
        }
    }

    [Serializable]
    public class FixtureData : NotificationObject, IFixtureData
    {
        private bool _shouldRun = true;
        public virtual string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }
        public FixtureStatus FixtureStatus { get; set; }
        public IAssemblyData Assembly { get; set; }
        public bool IsNodeExpanded { get; set; }
        
        public string Summary
        {
            get
            {
                return string.Format("{0} Tests", Tests.Count);
            }
        }

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

        public bool ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;

                foreach (var test in Tests)
                {
                    test.ShouldRun = _shouldRun;
                }

                RaisePropertyChanged("ShouldRun");
            }
        }

        public FixtureData(){}

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

    [Serializable]
    public class TestData : NotificationObject, ITestData
    {
        private TestStatus _testStatus;
        private IList<IResultData> _resultData;
        private bool _shouldRun = true;
        public virtual string Name { get; set; }
        public bool RunDynamo { get; set; }
        public virtual string ModelPath { get; set; }
        public bool IsNodeExpanded { get; set; }

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

        public virtual TestStatus TestStatus
        {
            get { return _testStatus; }
            set
            {
                _testStatus = value;
                RaisePropertyChanged("TestStatus");
            }
        }

        public bool ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                //Debug.WriteLine(value
                //    ? string.Format("{0} should run.", Name)
                //    : string.Format("{0} should not run.", Name));

                _shouldRun = value;
                RaisePropertyChanged("ShouldRun");
            }
        }

        public ObservableCollection<IResultData> ResultData { get; set; }
        
        public string JournalPath { get; set; }

        public virtual IFixtureData Fixture { get; set; }

        public TestData(){}

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

    [Serializable]
    public class CategoryData : NotificationObject, ICategoryData
    {
        private bool _shouldRun = true;
        public virtual string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }
        public IAssemblyData Assembly { get; set; }

        public bool IsNodeExpanded { get; set; }

        public bool ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;

                foreach(var test in Tests)
                {

                    test.ShouldRun = _shouldRun;
                }

                RaisePropertyChanged("ShouldRun");
            }
        }

        public string Summary
        {
            get
            {
                return string.Format("{0} Tests", Tests.Count);
            }
        }

        public CategoryData(){}

        public CategoryData(IAssemblyData assembly, string name)
        {
            Name = name;
            Tests = new ObservableCollection<ITestData>();
            Assembly = assembly;
        }
    }

    [Serializable]
    public class ResultData : NotificationObject, IResultData
    {
        private string _message = "";
        private string _stackTrace = "";

        public bool IsNodeExpanded { get; set; }

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
