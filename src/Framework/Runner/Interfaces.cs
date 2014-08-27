using System.Collections.ObjectModel;

namespace RTF.Framework
{
    public enum TestStatus{None,Cancelled, Error, Failure, Ignored, Inconclusive, NotRunnable, Skipped, Success,TimedOut}
    public enum FixtureStatus{None, Success, Failure, Mixed}
    public enum GroupingType{Fixture, Category}

    public interface IAssemblyData
    {
        GroupingType GroupingType { get; set; }
        string Path { get; set; }
        string Name { get; set; }
        ObservableCollection<IFixtureData> Fixtures { get; set; } 
        ObservableCollection<ICategoryData> Categories { get; set; } 
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

    public interface ICategoryData
    {
        string Name { get; set; }
        ObservableCollection<ITestData> Tests { get; set; } 
    }
}
