using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RevitTestFrameworkRunner
{
    public enum TestStatus{None,Cancelled, Error, Failure, Ignored, Inconclusive, NotRunnable, Skipped, Success,TimedOut}
    public enum FixtureStatus{None, Success, Failure, Mixed}

    public interface IAssemblyData
    {
        string Path { get; set; }
        string Name { get; set; }
        ObservableCollection<IFixtureData> Fixtures { get; set; } 
    }

    public interface IFixtureData
    {
        IAssemblyData Assembly { get; set; }
        string Name { get; set; }
        ObservableCollection<ITestData> Tests { get; set; } 
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
}
