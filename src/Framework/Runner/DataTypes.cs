using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Practices.Prism.ViewModel;

namespace RTF.Framework
{
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

        public FixtureData() { }

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
            if (e.PropertyName == "TestStatus" || e.PropertyName == "ResultData")
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
            get { return ModelPath != null && File.Exists(ModelPath); }
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

        public TestData() { }

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

                foreach (var test in Tests)
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

        public CategoryData() { }

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
