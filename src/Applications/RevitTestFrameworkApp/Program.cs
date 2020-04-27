using System;
using System.Linq;
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
                var setupData = Runner.ParseCommandLineArguments(args);

                runner = new Runner(setupData);

                if(runner.IsExport)
                {
                    runner.ExportJournal();
                }
                else
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

            runner.StartServer();
            if (runner.SetupTests())
            {
                runner.RunAllTests();
            }
            else
            {
                Console.WriteLine("ERROR: No tests were run due to configuration problems");
            }
            runner.EndServer();
        }
    }
}
