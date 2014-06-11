using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Serialization;
using Dynamo.NUnit.Tests;
using Microsoft.Practices.Prism;
using RTF.Framework;

namespace RTF.Applications
{
    /// <summary>
    /// Interaction logic for View.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly RunnerViewModel vm;

        public MainWindow()
        {
            InitializeComponent();

            var runner = new Runner {Gui = true};
            runner.Products.AddRange(Framework.Runner.FindRevit());
            runner.TestComplete += GetTestResultStatus;

            vm = new RunnerViewModel(runner);

            DataContext = vm;

            Closing += View_Closing;
            Loaded += View_Loaded;
        }

        void View_Loaded(object sender, RoutedEventArgs e)
        {
            vm.LoadSettingsCommand.Execute();
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            vm.SaveSettingsCommand.Execute();
            vm.CleanupCommand.Execute();
        }

        private void TestDataTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            vm.SelectedItem = e.NewValue;
        }

        private resultType LoadResults(string resultsPath)
        {
            if (!File.Exists(resultsPath))
            {
                return null;
            }

            resultType results = null;

            //write to the file
            var x = new XmlSerializer(typeof(resultType));
            using (var reader = new StreamReader(resultsPath))
            {
                results = (resultType)x.Deserialize(reader);
            }

            return results;
        }

        private void GetTestResultStatus(ITestData td, string resultsPath)
        {
            Application.Current.Dispatcher.Invoke((() =>
                td.ResultData.Clear()));

            //set the test status
            var results = LoadResults(resultsPath);
            if (results != null)
            {
                //find our results in the results
                var mainSuite = results.testsuite;
                var ourSuite =
                    results.testsuite.results.Items
                        .Cast<testsuiteType>()
                        .FirstOrDefault(s => s.name == td.Fixture.Name);

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

                    Application.Current.Dispatcher.Invoke((() =>
                        td.ResultData.Add(
                            new ResultData()
                            {
                                StackTrace = failure.stacktrace,
                                Message = ourTest.name + ":" + failure.message
                            })));
                }

            }
        }
    }
}
