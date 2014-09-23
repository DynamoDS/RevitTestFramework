using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NDesk.Options;
using RTF.Framework;

namespace RTF.Applications
{
    class Program
    {
        private static Runner runner;
        
        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                var setupData = ParseArguments(args);

                runner = new Runner(setupData);

                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void Run()
        {
            var assData = runner.Assemblies.FirstOrDefault();
            if (assData == null)
            {
                return;
            }

            // Run by Fixture
            if (!string.IsNullOrEmpty(runner.Fixture))
            {
                var fixData = assData.Fixtures.FirstOrDefault(f => f.Name == runner.Fixture);
                if (fixData != null)
                {
                    ((IExcludable) fixData).ShouldRun = true;
                }
            }
            // Run by test.
            else if (!string.IsNullOrEmpty(runner.Test))
            {
                var testData = runner.GetAllTests()
                        .FirstOrDefault(t => t.Name == runner.Test);
                if (testData != null)
                {
                    testData.ShouldRun = true;
                }
            }
            // Run by category
            else if (!string.IsNullOrEmpty(runner.Category))
            {
                var catData = assData.Categories.
                    FirstOrDefault(c => c.Name == runner.Category);
                if (catData != null)
                {
                    ((IExcludable) catData).ShouldRun = true;
                }
            }
            // If the no fixture, test, or category is specified, run the whole assembly.
            else
            {
                // Only support one assembly for right now.
                assData.ShouldRun = true;
            }

            runner.SetupTests();
            runner.RunAllTests();
        }

        private static IRunnerSetupData ParseArguments(IEnumerable<string> args)
        {
            var showHelp = false;

            var setupData = new RunnerSetupData();

            var p = new OptionSet()
            {
                {"dir:","The path to the working directory.", v=> setupData.WorkingDirectory = Path.GetFullPath(v)},
                {"a:|assembly:", "The path to the test assembly.", v => setupData.TestAssembly = Path.GetFullPath(v)},
                {"r:|results:", "The path to the results file.", v=>setupData.Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of the test fixture.", v => setupData.Fixture = v},
                {"t:|testName:", "The name of a test to run", v => setupData.Test = v},
                {"category:", "The name of a test category to run.", v=> setupData.Category = v},
                {"exclude:", "The name of a test category to exclude.", v=> setupData.ExcludedCategory = v},
                {"c:|concatenate:", "Concatenate results with existing results file.", v=> setupData.Concat = v != null},
                {"revit:", "The path to Revit.", v=> setupData.RevitPath = v},
                {"copyAddins:", "Specify whether to copy the addins from the Revit folder to the current working directory",
                    v=> setupData.CopyAddins = v != null},
                {"dry:", "Conduct a dry run.", v=> setupData.DryRun = v != null},
                {"x:|clean:", "Cleanup journal files after test completion", v=> setupData.CleanUp = v != null},
                {"continuous:", "Run all selected tests in one Revit session.", v=> setupData.Continuous = v != null},
                {"d|debug", "Run in debug mode.", v=>setupData.IsDebug = v != null},
                {"h|help", "Show this message and exit.", v=> showHelp = v != null}
            };

            var notParsed = new List<string>();

            const string helpMessage = "Try 'DynamoTestFrameworkRunner --help' for more information.";

            try
            {
                notParsed = p.Parse(args);
            }
            catch (OptionException e)
            {
                string message = e.Message + "\n" + helpMessage;
                throw new Exception(message);
            }

            if (notParsed.Count > 0)
            {
                throw new ArgumentException(String.Join(" ", notParsed.ToArray()));
            }

            if (showHelp)
            {
                ShowHelp(p);
                throw new Exception();
            }

            return setupData;
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: DynamoTestFrameworkRunner [OPTIONS]");
            Console.WriteLine("Run a test or a fixture of tests from an assembly.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

    }
}
