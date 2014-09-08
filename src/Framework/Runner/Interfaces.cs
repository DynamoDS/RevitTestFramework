using System.Collections.ObjectModel;

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
        ICategoryData Category { get; set; }
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

}
