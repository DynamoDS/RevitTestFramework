using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Dynamo.Utilities;
using RevitTestFrameworkRunner;

namespace RevitTestFramework
{
    class Program
    {
        private static ViewModel _vm;
        
        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyHelper.CurrentDomain_AssemblyResolve;

            try
            {
                var runner = new Runner();
                _vm = new ViewModel(runner);

                if (!runner.ParseArguments(args))
                {
                    return;
                }

                if (!runner.FindRevit(runner.Products))
                {
                    return;
                }
                
                if (runner.Gui)
                {
                    runner.LoadSettings();

                    if (!string.IsNullOrEmpty(runner.TestAssembly) && File.Exists(runner.TestAssembly))
                    {
                        runner.Refresh();
                    }

                    // Show the user interface
                    var view = new View(_vm);
                    view.ShowDialog();

                    runner.SaveSettings();
                }
                else
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

                    if (!runner.ReadAssembly(runner.TestAssembly, runner.Assemblies))
                    {
                        return;
                    }

                    if (File.Exists(runner.Results) && !runner.Concat)
                    {
                        File.Delete(runner.Results);
                    }

                    Console.WriteLine(runner.ToString());

                    if (string.IsNullOrEmpty(runner.Fixture) && string.IsNullOrEmpty(runner.Test))
                    {
                        runner.RunCount = runner.Assemblies.SelectMany(a => a.Fixtures.SelectMany(f => f.Tests)).Count();
                        foreach (var ad in runner.Assemblies)
                        {
                            runner.RunAssembly(ad);
                        }
                    }
                    else if (string.IsNullOrEmpty(runner.Test) && !string.IsNullOrEmpty(runner.Fixture))
                    {
                        var fd = runner.Assemblies.SelectMany(x => x.Fixtures).FirstOrDefault(f => f.Name == runner.Fixture);
                        if (fd != null)
                        {
                            runner.RunCount = fd.Tests.Count;
                            runner.RunFixture(fd);
                        }
                    }
                    else if (string.IsNullOrEmpty(runner.Fixture) && !string.IsNullOrEmpty(runner.Test))
                    {
                        var td =
                            runner.Assemblies.SelectMany(a => a.Fixtures.SelectMany(f => f.Tests))
                                .FirstOrDefault(t => t.Name == runner.Test);
                        if (td != null)
                        {
                            runner.RunCount = 1;
                            runner.RunTest(td);
                        }
                    }
                }

                runner.Cleanup();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
