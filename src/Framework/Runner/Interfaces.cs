using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.RevitAddIns;

namespace RTF.Framework
{
    public enum TestStatus{None,Cancelled, Error, Failure, Ignored, Inconclusive, NotRunnable, Skipped, Success,TimedOut}
    public enum FixtureStatus{None, Success, Failure, Mixed}
    public enum GroupingType{Fixture, Category}

    public interface IAssemblyData
    {
        string Path { get; set; }
        string Name { get; set; }
        ObservableCollection<IGroupable> SortingGroup { get; set; }
        ObservableCollection<IGroupable> Fixtures { get; set; } 
        ObservableCollection<IGroupable> Categories { get; set; } 
    }

    public interface IGroupable
    {
        string Name { get; set; }
        ObservableCollection<ITestData> Tests { get; set; } 
    }

    public interface IFixtureData:IGroupable
    {
        IAssemblyData Assembly { get; set; }
        FixtureStatus FixtureStatus { get; set; }
    }

    public interface ITestData
    {
        IFixtureData Fixture { get; set; }
        string Name { get; set; }
        bool RunDynamo { get; set; }
        string ModelPath { get; set; }
        string ShortModelPath { get; }
        TestStatus TestStatus { get; set; }
        ObservableCollection<IResultData> ResultData { get; set; }
    }

    public interface IResultData
    {
        string Message { get; set; }
        string StackTrace { get; set; }
    }

    public interface ICategoryData : IGroupable{}

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
        bool Gui { get; set; }
        GroupingType GroupingType { get; set; }
        IList<RevitProduct> Products { get; set; }
        int Timeout { get; set; }
    }

    public interface IRunner
    {
        /// <summary>
        /// The path of the RTF addin file.
        /// </summary>
        string AddinPath { get; set; }

        /// <summary>
        /// A counter for the number of runs processed.
        /// </summary>
        int RunCount { get; set; }

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

        /// <summary>
        /// A flag which can be used to specifi
        /// </summary>
        bool Gui { get; set; }

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
        
        void Run(object parameter);

        /// <summary>
        /// Setup all tests in a selected assembly.
        /// </summary>
        /// <param name="ad"></param>
        /// <param name="continuous"></param>
        void SetupAssemblyTests(IAssemblyData ad, bool continuous = false);

        /// <summary>
        /// Setup all tests in a selected fixture.
        /// </summary>
        /// <param name="fd"></param>
        /// <param name="continuous"></param>
        void SetupFixtureTests(IFixtureData fd, bool continuous = false);

        /// <summary>
        /// Setup all tests in a selected category.
        /// </summary>
        /// <param name="cd">The category</param>
        /// <param name="continuous">Run continously</param>
        void SetupCategoryTests(ICategoryData cd, bool continuous = false);

        /// <summary>
        /// Setup the selected test.
        /// </summary>
        /// <param name="td"></param>
        /// <param name="continuous"></param>
        void SetupIndividualTest(ITestData td, bool continuous = false);

        /// <summary>
        /// Run all tests that have been set up.
        /// </summary>
        void RunAllTests();

        /// <summary>
        /// Re-read the selected assembly to find available tests.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Remove journal and addin files generated by the tests.
        /// </summary>
        void Cleanup();

        string ToString();
    }
}
