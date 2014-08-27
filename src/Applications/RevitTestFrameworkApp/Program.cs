using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Practices.Prism;
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
                runner = new Runner();

                if (!ParseArguments(args))
                {
                    return;
                }

                var products = Runner.FindRevit();
                if (products == null)
                {
                    return;
                }

                runner.Products.AddRange(products);

                Run();

                runner.Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void Run()
        {
            if (string.IsNullOrEmpty(runner.RevitPath))
            {
                runner.RevitPath = Path.Combine(runner.Products.First().InstallLocation, "revit.exe");
            }

            if (string.IsNullOrEmpty(runner.WorkingDirectory))
            {
                runner.WorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            }

            // In any case here, the test assembly cannot be null
            if (string.IsNullOrEmpty(runner.TestAssembly))
            {
                Console.WriteLine("You must specify at least a test assembly.");
                return;
            }

            var assemblyDatas = Runner.ReadAssembly(runner.TestAssembly, runner.WorkingDirectory);
            if (assemblyDatas == null)
            {
                return;
            }

            runner.Assemblies.Clear();
            runner.Assemblies.AddRange(assemblyDatas);

            if (File.Exists(runner.Results) && !runner.Concat)
            {
                File.Delete(runner.Results);
            }

            Console.WriteLine(runner.ToString());

            if (string.IsNullOrEmpty(runner.Fixture) && string.IsNullOrEmpty(runner.Test))
            {
                foreach (var ad in runner.Assemblies)
                {
                    runner.SetupAssemblyTests(ad, runner.Continuous);
                }
            }
            else if (string.IsNullOrEmpty(runner.Test) && !string.IsNullOrEmpty(runner.Fixture))
            {
                var fd = runner.Assemblies.SelectMany(x => x.Fixtures).FirstOrDefault(f => f.Name == runner.Fixture);
                if (fd != null)
                {
                    runner.SetupFixtureTests(fd as IFixtureData, runner.Continuous);
                }
            }
            else if (string.IsNullOrEmpty(runner.Fixture) && !string.IsNullOrEmpty(runner.Test))
            {
                var td =
                    runner.Assemblies.SelectMany(a => a.Fixtures.SelectMany(f => f.Tests))
                        .FirstOrDefault(t => t.Name == runner.Test);
                if (td != null)
                {
                    runner.SetupIndividualTest(td, runner.Continuous);
                }
            }

            runner.RunAllTests();
        }

        private static bool ParseArguments(IEnumerable<string> args)
        {
            var showHelp = false;

            var p = new OptionSet()
            {
                {"dir:","The path to the working directory.", v=> runner.WorkingDirectory = Path.GetFullPath(v)},
                {"a:|assembly:", "The path to the test assembly.", v => runner.TestAssembly = Path.GetFullPath(v)},
                {"r:|results:", "The path to the results file.", v=>runner.Results = Path.GetFullPath(v)},
                {"f:|fixture:", "The full name (with namespace) of the test fixture.", v => runner.Fixture = v},
                {"t:|testName:", "The name of a test to run", v => runner.Test = v},
                {"c:|concatenate:", "Concatenate results with existing results file.", v=> runner.Concat = v != null},
                {"revit:", "The path to Revit.", v=> runner.RevitPath = v},
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
                Console.WriteLine(e.Message);
                Console.WriteLine(helpMessage);
                return false;
            }

            if (notParsed.Count > 0)
            {
                Console.WriteLine(String.Join(" ", notParsed.ToArray()));
                return false;
            }

            if (showHelp)
            {
                ShowHelp(p);
                return false;
            }

            if (!String.IsNullOrEmpty(runner.TestAssembly) && !File.Exists(runner.TestAssembly))
            {
                Console.Write("The specified test assembly does not exist.");
                return false;
            }

            if (!String.IsNullOrEmpty(runner.WorkingDirectory) && !Directory.Exists(runner.WorkingDirectory))
            {
                Console.Write("The specified working directory does not exist.");
                return false;
            }

            return true;
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
