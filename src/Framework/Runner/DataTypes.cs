using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace RTF.Framework
{
    [Serializable]
    public class AssemblyData : IAssemblyData
    {
        private bool? _shouldRun = true;
        private bool _isNodeExpanded;

        public virtual string Path { get; set; }
        public virtual string Name { get; set; }
        public ObservableCollection<ITestGroup> Fixtures { get; set; }
        public ObservableCollection<ITestGroup> Categories { get; set; }
        public GroupingType GroupingType { get; set; }

        public bool IsNodeExpanded
        {
            get
            {
                return _isNodeExpanded;
            }
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Version of Revit referenced by the assembly
        /// </summary>
        public string ReferencedRevitVersion { get; set; }

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

        public bool? ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;
                SetChildrenShouldRunWithoutRaise(_shouldRun);
                OnPropertyChanged("ShouldRun");
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

            Fixtures.CollectionChanged += Fixtures_CollectionChanged;
            Categories.CollectionChanged += Categories_CollectionChanged;
        }

        void Categories_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var cd in from object item in e.NewItems select item as CategoryData)
                    {
                        cd.PropertyChanged += fd_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var cd in from object item in e.OldItems select item as CategoryData)
                    {
                        cd.PropertyChanged -= fd_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var cd in from object item in e.OldItems select item as CategoryData)
                    {
                        cd.PropertyChanged -= fd_PropertyChanged;
                    }
                    break;
                default:
                    break;
            }
        }

        void Fixtures_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var fd in from object item in e.NewItems select item as FixtureData)
                    {
                        fd.PropertyChanged += fd_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var fd in from object item in e.OldItems select item as FixtureData)
                    {
                        fd.PropertyChanged -= fd_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var fd in from object item in e.OldItems select item as FixtureData)
                    {
                        fd.PropertyChanged -= fd_PropertyChanged;
                    }
                    break;
                default:
                    break;
            }
        }

        private void fd_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ShouldRun":
                    if (SortingGroup.All(f => ((IExcludable)f).ShouldRun == true))
                    {
                        _shouldRun = true;
                    }
                    else if (SortingGroup.All(f => ((IExcludable) f).ShouldRun == false))
                    {
                        _shouldRun = false;
                    }
                    else
                    {
                        _shouldRun = null;
                    }
                    
                    OnPropertyChanged("ShouldRun");
                    break;
            }
        }

        public void Dispose()
        {
            foreach (var fd in Fixtures.Select(item => item as FixtureData))
            {
                fd.PropertyChanged -= fd_PropertyChanged;
            }

            foreach (var f in Fixtures.Select(fd => fd as FixtureData))
            {
                f.Dispose();
            }

            foreach (var c in Categories.Select(cd => cd as CategoryData))
            {
                c.Dispose();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetChildrenShouldRunWithoutRaise(bool? shouldRun)
        {
            foreach (var ex in SortingGroup.Select(sg => sg as IExcludable))
            {
                ex.SetChildrenShouldRunWithoutRaise(shouldRun);
            }
        }
    }

    [Serializable]
    public class FixtureData : IFixtureData
    {
        private bool? _shouldRun = true;
        private bool _isNodeExpanded;
        public virtual string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }
        public FixtureStatus FixtureStatus { get; set; }
        public IAssemblyData Assembly { get; set; }

        public bool IsNodeExpanded
        {
            get
            {
                return _isNodeExpanded;
            }
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

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

        public bool? ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;
                SetChildrenShouldRunWithoutRaise(_shouldRun);
                OnPropertyChanged("ShouldRun");
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
                    foreach (var td in from object item in e.NewItems select item as TestData)
                    {
                        td.PropertyChanged += td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var td in from object item in e.OldItems select item as TestData)
                    {
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var td in from object item in e.OldItems select item as TestData)
                    {
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                default:
                    break;
            }
        }

        void td_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "TestStatus":
                case "ResultData":
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

                    OnPropertyChanged("FixtureStatus");
                    OnPropertyChanged("FixtureSummary");
                    break;
                case "ShouldRun":
                    
                    if (Tests.All(t => t.ShouldRun == true))
                    {
                        _shouldRun = true;
                    }
                    else if (Tests.All(t => t.ShouldRun == false))
                    {
                        _shouldRun = false;
                    }
                    else
                    {
                        _shouldRun = null; 
                    }
                    
                    OnPropertyChanged("ShouldRun");
                    break;
            }
        }

        public void Dispose()
        {
            foreach (var td in Tests.Select(item => item as TestData))
            {
                td.PropertyChanged -= td_PropertyChanged;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetChildrenShouldRunWithoutRaise(bool? shouldRun)
        {
            if (shouldRun == null) return;

            _shouldRun = shouldRun;
            foreach (var t in Tests)
            {
                t.SetChildrenShouldRunWithoutRaise(shouldRun);
            }

            OnPropertyChanged("ShouldRun");
        }

    }

    [Serializable]
    public class TestData : ITestData
    {
        private TestStatus _testStatus;
        private bool? _shouldRun = true;
        private bool _isNodeExpanded;

        public virtual string Name { get; set; }

        [XmlIgnore]
        public bool RunDynamo { get; set; }

        [XmlIgnore]
        public virtual string ModelPath { get; set; }

        [XmlIgnore]
        public bool IsNodeExpanded
        {
            get
            {
                return _isNodeExpanded;
            }
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        [XmlIgnore]
        public bool ModelExists
        {
            get
            {
                bool modelExists = false;

                try
                {
                    modelExists = (ModelPath != null) && File.Exists(ModelPath);
                }
                catch
                {
                    // Nothing to do, just say the model isn't there (probably a wildcard in the file name
                    // and no models were found in the give path)
                }

                return modelExists;
            }
        }

        [XmlIgnore]
        public string ModelPathMessage
        {
            get
            {
                return ModelExists ?
                    "The selected test model exists." :
                    "The selected test model does not exist. Check your working directory.";
            }
        }

        [XmlIgnore]
        public string ShortModelPath
        {
            get
            {
                if (string.IsNullOrEmpty(ModelPath))
                {
                    return string.Empty;
                }

                return $"[{ModelPath}]";
            }
        }

        [XmlIgnore]
        public virtual TestStatus TestStatus
        {
            get { return _testStatus; }
            set
            {
                _testStatus = value;
                OnPropertyChanged("TestStatus");
            }
        }

        public bool? ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;
                OnPropertyChanged("ShouldRun");
            }
        }

        [XmlIgnore]
        public ObservableCollection<IResultData> ResultData { get; set; }

        [XmlIgnore]
        public string JournalPath { get; set; }

        [XmlIgnore]
        public virtual IFixtureData Fixture { get; set; }

        [XmlIgnore]
        public bool Completed { get; set; }

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
            OnPropertyChanged("ResultData");
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetChildrenShouldRunWithoutRaise(bool? shouldRun)
        {
            if (shouldRun == null) return;
            _shouldRun = shouldRun;

            OnPropertyChanged("ShouldRun");
        }

        public override string ToString()
        {
            return string.Format("Name: {0}, Model Path: {1}", Name, ModelPath);
        }
    }

    [Serializable]
    public class CategoryData : ICategoryData
    {
        private bool? _shouldRun = true;
        private bool _isNodeExpanded;
        public virtual string Name { get; set; }
        public ObservableCollection<ITestData> Tests { get; set; }
        public IAssemblyData Assembly { get; set; }

        public bool IsNodeExpanded
        {
            get
            {
                return _isNodeExpanded;
            }
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool? ShouldRun
        {
            get { return _shouldRun; }
            set
            {
                _shouldRun = value;
                SetChildrenShouldRunWithoutRaise(_shouldRun);
                OnPropertyChanged();
            }
        }

        public void SetChildrenShouldRunWithoutRaise(bool? shouldRun)
        {
            if (shouldRun == null) return;

            foreach (var t in Tests)
            {
                t.SetChildrenShouldRunWithoutRaise(shouldRun);
            }

            OnPropertyChanged(nameof(ShouldRun));
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

            Tests.CollectionChanged += Tests_CollectionChanged;
        }

        void Tests_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (var td in from object item in e.NewItems select item as TestData)
                    {
                        td.PropertyChanged += td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    foreach (var td in from object item in e.OldItems select item as TestData)
                    {
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    foreach (var td in from object item in e.OldItems select item as TestData)
                    {
                        td.PropertyChanged -= td_PropertyChanged;
                    }
                    break;
                default:
                    break;
            }
        }

        void td_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "ShouldRun":

                    if (Tests.All(t => t.ShouldRun == true))
                    {
                        _shouldRun = true;
                    }
                    else if (Tests.All(t => t.ShouldRun == false))
                    {
                        _shouldRun = false;
                    }
                    else
                    {
                        _shouldRun = null;
                    }

                    OnPropertyChanged("ShouldRun");
                    break;
            }
        }


        public void Dispose()
        {
            // Nothing to do for categories yet.
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    [Serializable]
    public class ResultData : IResultData
    {
        private string _message = "";
        private string _stackTrace = "";
        private bool _isNodeExpanded;

        public bool IsNodeExpanded
        {
            get
            {
                return _isNodeExpanded;
            }
            set
            {
                if (_isNodeExpanded != value)
                {
                    _isNodeExpanded = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                OnPropertyChanged();
            }
        }

        public string StackTrace
        {
            get { return _stackTrace; }
            set
            {
                _stackTrace = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
