using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.RevitAddIns;

namespace RTF.Framework
{
    public enum TestStatus{None,Cancelled, Error, Failure, Ignored, Inconclusive, NotRunnable, Skipped, Success,TimedOut}
    public enum FixtureStatus{None, Success, Failure, Mixed}

    public enum GroupingType
    {
        Fixture,
        Category
    }

    public interface ITestGroup
    {
        string Name { get; set; }
        ObservableCollection<ITestData> Tests { get; set; }
        IAssemblyData Assembly { get; set; }
    }

    public interface IExcludable
    {
        bool ShouldRun { get; set; }
    }

    public interface IAssemblyData:IExcludable
    {
        string Path { get; set; }
        string Name { get; set; }
        ObservableCollection<ITestGroup> SortingGroup { get; }
        ObservableCollection<ITestGroup> Fixtures { get; set; }
        ObservableCollection<ITestGroup> Categories { get; set; } 
        GroupingType GroupingType { get; set; }
    }

    public interface IFixtureData:ITestGroup,IExcludable
    {
        FixtureStatus FixtureStatus { get; set; }
    }

    public interface ITestData:IExcludable
    {
        IFixtureData Fixture { get; set; }
        string Name { get; set; }
        bool RunDynamo { get; set; }
        string ModelPath { get; set; }
        string ShortModelPath { get; }
        TestStatus TestStatus { get; set; }
        ObservableCollection<IResultData> ResultData { get; set; }
        string JournalPath { get; set; }
    }

    public interface IResultData
    {
        string Message { get; set; }
        string StackTrace { get; set; }
    }

    public interface ICategoryData : ITestGroup,IExcludable
    {}

    public interface IRunnerSetupData
    {
        string WorkingDirectory { get; set; }
        string AssemblyPath { get; set; }
        string TestAssembly { get; set; }
        string Results { get; set; }
        string Fixture { get; set; }
        string Category { get; set; }
        string Test { get; set; }
        bool Concat { get; set; }
        bool DryRun { get; set; }
        string RevitPath { get; set; }
        bool CleanUp { get; set; }
        bool Continuous { get; set; }
        bool IsDebug { get; set; }
        GroupingType GroupingType { get; set; }
        int Timeout { get; set; }
        bool IsTesting { get; set; }
        string ExcludedCategory { get; set; }
        int SelectedProduct { get; set; }
    }

    public interface IRunner
    {
        /// <summary>
        /// The path of the RTF addin file.
        /// </summary>
        string AddinPath { get; set; }

        /// <summary>
        /// The path of the selected assembly for testing.
        /// </summary>
        string AssemblyPath { get; set; }

        /// <summary>
        /// A collection of assemblies available for testing.
        /// </summary>
        ObservableCollection<IAssemblyData> Assemblies { get; set; }

        /// <summary>
        /// A collection of available Revit products for testing.
        /// </summary>
        ObservableCollection<RevitProduct> Products { get; set; }

        //Dictionary<ITestData, string> TestDictionary { get; }

        /// <summary>
        /// The selected Revit application against which
        /// to test.
        /// </summary>
        int SelectedProduct { get; set; }

        /// <summary>
        /// The name of the test to run.
        /// </summary>
        string Test { get; set; }

        /// <summary>
        /// The name of the assembly to run.
        /// </summary>
        string TestAssembly { get; set; }

        /// <summary>
        /// The name of the fixture to run.
        /// </summary>
        string Fixture { get; set; }

        /// <summary>
        /// The name of the category to run
        /// </summary>
        string Category { get; set; }

        /// <summary>
        /// A flag which, when set, allows you
        /// to attach to the debugger.
        /// </summary>
        bool IsDebug { get; set; }

        /// <summary>
        /// The path to the results file.
        /// </summary>
        string Results { get; set; }

        /// <summary>
        /// The path to the working directory.
        /// </summary>
        string WorkingDirectory { get; set; }

        /// <summary>
        /// The path to the version of Revit to be
        /// used for testing.
        /// </summary>
        string RevitPath { get; set; }

        /// <summary>
        /// A timeout value in milliseconds, after which
        /// any running test will be killed.
        /// </summary>
        int Timeout { get; set; }

        /// <summary>
        /// A flag to specify whether to concatenate test 
        /// results with those from a previous run.
        /// </summary>
        bool Concat { get; set; }

        /// <summary>
        /// A flag to allow cancellation. Cancellation will occur
        /// after the running test is completed.
        /// </summary>
        bool CancelRequested { get; set; }

        /// <summary>
        /// A flag which allows the setup of tests and the creation
        /// of an addin file without actually running the tests.
        /// </summary>
        bool DryRun { get; set; }

        /// <summary>
        /// A flag which controls whether journal files and addins
        /// generated by RTF are cleaned up upon test completion.
        /// </summary>
        bool CleanUp { get; set; }

        /// <summary>
        /// A flag which specifies whether all tests should be
        /// run from the same journal file.
        /// </summary>
        bool Continuous { get; set; }

        GroupingType GroupingType { get; set; }
        
        /// <summary>
        /// Run all tests. Must be preceded by a call to SetupTests.
        /// </summary>
        void RunAllTests();

        /// <summary>
        /// Setup tests. Precedes a call to RunAllTests.
        /// </summary>
        void SetupTests();

        /// <summary>
        /// Re-read the selected assembly to find available tests.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Remove journal and addin files generated by the tests.
        /// </summary>
        void Cleanup();

        IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory, GroupingType groupType, bool isTesting);

        string ToString();
    }
}
