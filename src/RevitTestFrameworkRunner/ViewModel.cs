using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Autodesk.RevitAddIns;
using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.ViewModel;
using Runner;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace RevitTestFrameworkApp
{
    public class ViewModel : NotificationObject
    {
        #region private members

        private object _selectedItem;
        private global::Runner.Runner _runner;

        #endregion

        #region public properties
        public DelegateCommand SetAssemblyPathCommand { get; set; }
        public DelegateCommand SetResultsPathCommand { get; set; }
        public DelegateCommand SetWorkingPathCommand { get; set; }
        public DelegateCommand<object> RunCommand { get; set; }

        //public Dispatcher UiDispatcher { get; set; }

        public object SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                _selectedItem = value;
                RaisePropertyChanged("SelectedItem");
                RaisePropertyChanged("RunText");
                RunCommand.RaiseCanExecuteChanged();
            }
        }

        public int SelectedProductIndex
        {
            get { return _runner.SelectedProduct; }
            set
            {
                _runner.SelectedProduct = value;

                _runner.RevitPath = _runner.SelectedProduct == -1 ? 
                    string.Empty : 
                    Path.Combine(_runner.Products[value].InstallLocation, "revit.exe");

                RaisePropertyChanged("SelectedProductIndex");
            }
        }

        public string RunText
        {
            get
            {
                if (SelectedItem is IAssemblyData)
                {
                    return "Run All Tests in Selected Assembly.";
                }
                
                if (SelectedItem is IFixtureData)
                {
                    return "Run All Tests in Selected Fixture";
                }
                 
                if(SelectedItem is ITestData)
                {
                    return "Run Selected Test";
                }

                return "Nothing Selected";
            }
            set
            {
                RaisePropertyChanged("RunText");
            }
        }
        
        public string ResultsPath
        {
            get { return _runner.Results; }
            set
            {
                _runner.Results = value;
                RaisePropertyChanged("ResultsPath");
                RunCommand.RaiseCanExecuteChanged();
            }
        }

        public string AssemblyPath
        {
            get { return _runner.TestAssembly; }
            set
            {
                _runner.TestAssembly = value;
                _runner.Refresh();
                RaisePropertyChanged("AssemblyPath");
            }
        }

        public string WorkingPath
        {
            get { return _runner.WorkingDirectory; }
            set
            {
                _runner.WorkingDirectory = value;
                RaisePropertyChanged("WorkingPath");
            }
        }

        public bool IsDebug
        {
            get { return _runner.IsDebug; }
            set
            {
                _runner.IsDebug = value;
                RaisePropertyChanged("IsDebug");
            }
        }

        public int Timeout
        {
            get { return _runner.Timeout; }
            set { _runner.Timeout = value; }
        }

        public ObservableCollection<IAssemblyData> Assemblies
        {
            get { return _runner.Assemblies; }
        }

        public ObservableCollection<RevitProduct> Products
        {
            get { return _runner.Products; }
        }

        #endregion

        #region constructors

        internal ViewModel(global::Runner.Runner runner)
        {
            _runner = runner;
            
            SetAssemblyPathCommand = new DelegateCommand(SetAssemblyPath, CanSetAssemblyPath);
            SetResultsPathCommand = new DelegateCommand(SetResultsPath, CanSetResultsPath);
            SetWorkingPathCommand = new DelegateCommand(SetWorkingPath, CanSetWorkingPath);
            RunCommand = new DelegateCommand<object>(Run, CanRun);

            _runner.Products.CollectionChanged += Products_CollectionChanged;
        }

        #endregion

        void Products_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // When the products collection is changed, we want to set
            // the selected product index to the first in the list
            if (_runner.Products.Count > 0)
            {
                SelectedProductIndex = 0;
            }
            else
            {
                SelectedProductIndex = -1;
            }
        }

        private bool CanRun(object parameter)
        {
            return SelectedItem != null;
        }

        private void Run(object parameter)
        {
            if (string.IsNullOrEmpty(_runner.Results))
            {
                MessageBox.Show("Please select an output path for the results.");
                return;
            }

            if (File.Exists(_runner.Results) && !_runner.Concat)
            {
                File.Delete(_runner.Results);
            }

            var worker = new BackgroundWorker();

            worker.DoWork += TestThread;
            worker.RunWorkerAsync(parameter);   
        }

        private void TestThread(object sender, DoWorkEventArgs e)
        {
            if (e.Argument is IAssemblyData)
            {
                var ad = e.Argument as IAssemblyData;
                _runner.RunCount = ad.Fixtures.SelectMany(f => f.Tests).Count();
                _runner.RunAssembly(ad);
            }
            else if (e.Argument is IFixtureData)
            {
                var fd = e.Argument as IFixtureData;
                _runner.RunCount = fd.Tests.Count;
                _runner.RunFixture(fd);
            }
            else if (e.Argument is ITestData)
            {
                _runner.RunCount = 1;
                _runner.RunTest(e.Argument as ITestData);
            }
        }

        private bool CanSetWorkingPath()
        {
            return true;
        }

        private void SetWorkingPath()
        {
            var dirs = new FolderBrowserDialog();

            if (dirs.ShowDialog() == DialogResult.OK)
            {
                WorkingPath = dirs.SelectedPath;
            }
        }

        private bool CanSetResultsPath()
        {
            return true;
        }

        private void SetResultsPath()
        {
            var files = new SaveFileDialog()
            {
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Filter = "xml files (*.xml) | *.xml",
                RestoreDirectory = true,
                DefaultExt = ".xml"
            };

            var filesResult = files.ShowDialog();

            if (filesResult != null && filesResult == true)
            {
                ResultsPath = files.FileName;
            }
        }

        private bool CanSetAssemblyPath()
        {
            return true;
        }

        private void SetAssemblyPath()
        {
            var files = new OpenFileDialog
            {
                InitialDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                Filter = "assembly files (*.dll)|*.dll| executable files (*.exe)|*.exe",
                RestoreDirectory = true,
                DefaultExt = ".dll"
            };

            var filesResult = files.ShowDialog();

            if (filesResult != null && filesResult == true)
            {
                AssemblyPath = files.FileName;
            }
        }
    }
}
