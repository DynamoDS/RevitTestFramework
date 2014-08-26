using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
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
            runner.TestFailed += runner_TestFailed;
            runner.TestTimedOut += runner_TestTimedOut;

            vm = new RunnerViewModel(runner);

            DataContext = vm;

            Closing += View_Closing;
            Loaded += View_Loaded;
        }

        void runner_TestTimedOut(ITestData data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                data.ResultData.Clear();
                data.ResultData.Add(new ResultData(){Message = "Test timed out."});
            });
        }

        void runner_TestFailed(ITestData data, string message, string stackTrace)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                data.ResultData.Clear();
                data.ResultData.Add(new ResultData() { Message = message, StackTrace = stackTrace});
            });
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

        private resultType TryParseResultsOrEmitError(string resultsPath)
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

        private void GetTestResultStatus(IList<ITestData> data, string resultsPath)
        {
            // Try to get the results, if fail, short-circuit
            var results = TryParseResultsOrEmitError(resultsPath);
            if (results == null) return;

            foreach (var td in data)
            {
                Application.Current.Dispatcher.Invoke((() =>
                    td.ResultData.Clear()));

                //find our results in the results
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
