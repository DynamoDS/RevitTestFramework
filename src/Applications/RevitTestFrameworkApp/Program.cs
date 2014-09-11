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

                runner = Runner.Initialize(setupData);

                Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void Run()
        {
            object data = null;

            // If the no fixture, test, or category is specified, run the whole assembly.
            if (string.IsNullOrEmpty(runner.Fixture) && 
                string.IsNullOrEmpty(runner.Test) && 
                string.IsNullOrEmpty(runner.Category))
            {
                // Only support one assembly for right now.
                data = runner.Assemblies.FirstOrDefault();
            }
            // Run by Category
            if (!string.IsNullOrEmpty(runner.Category))
            {
                data = runner.Category;
            }
            // Run by Fixture
            else if (!string.IsNullOrEmpty(runner.Fixture))
            {
                data = runner.Assemblies.SelectMany(x => x.Fixtures).FirstOrDefault(f => f.Name == runner.Fixture);
            }
            // Run by test.
            else if (!string.IsNullOrEmpty(runner.Test))
            {
                data = runner.Assemblies.SelectMany(a => a.Fixtures.SelectMany(f => f.Tests))
                        .FirstOrDefault(t => t.Name == runner.Test);
            }
            else if (!string.IsNullOrEmpty(runner.Category))
            {
                data = runner.Assemblies.SelectMany(a => a.Categories).
                    FirstOrDefault(c => string.Compare(c.Name, runner.Category, true) == 0) as ICategoryData;
            }

            if (data == null)
            {
                throw new Exception("Running mode could not be determined from the inputs provided.");
            }

            runner.SetupTests(data);
            runner.RunAllTests();
        }

        private static IRunnerSetupData ParseArguments(IEnumerable<string> args)
        {
            var showHelp = false;

            var setupData = new RunnerSetupData();

            var p = new OptionSet()
            {
                {"dir:","The path to the working directory.", v=> runner.WorkingDirectory = Path.GetFullPath(v)},
                {"a:|assembly:", "The path to the test assembly.", v => runner.TestAssembly = Path.GetFullPath(v)},
                {"r:|results:", "The path to the results file.", v=>runner.Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of the test fixture.", v => runner.Fixture = v},
                {"t:|testName:", "The name of a test to run", v => runner.Test = v},
                {"category:", "The name of a test category to run.", v=> runner.Category = v},
                {"exclude:", "The name of a test category to exclude.", v=> runner.ExcludedCategory = v},
                {"c:|concatenate:", "Concatenate results with existing results file.", v=> runner.Concat = v != null},
                {"revit:", "The path to Revit.", v=> runner.RevitPath = v},
                {"copyAddins:", "Specify whether to copy the addins from the Revit folder to the current working directory",
                    v=> runner.CopyAddins = v != null},
                {"dry:", "Conduct a dry run.", v=> runner.DryRun = v != null},
                {"x:|clean:", "Cleanup journal files after test completion", v=> runner.CleanUp = v != null},
                {"continuous:", "Run all selected tests in one Revit session.", v=> runner.Continuous = v != null},
                {"d|debug", "Run in debug mode.", v=>runner.IsDebug = v != null},
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
