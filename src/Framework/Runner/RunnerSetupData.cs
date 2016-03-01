using System.Collections.Generic;
using System.Linq;
using Autodesk.RevitAddIns;

namespace RTF.Framework
{
    /// <summary>
    /// The RunnerSetupData class is used to convey
    /// required setup information to the Runner constructor.
    /// </summary>
    public class RunnerSetupData : IRunnerSetupData
    {
        public virtual string WorkingDirectory { get; set; }
        public string AssemblyPath { get; set; }
        public virtual string TestAssembly { get; set; }
        public string Results { get; set; }
        public string Fixture { get; set; }
        public string Category { get; set; }
        public string Test { get; set; }
        public bool Concat { get; set; }
        public bool DryRun { get; set; }
        public string RevitPath { get; set; }
        public bool CleanUp { get; set; }
        public bool Continuous { get; set; }
        public bool IsDebug { get; set; }
        public GroupingType GroupingType { get; set; }
        public int Timeout { get; set; }
        public string ExcludedCategory { get; set; }
        public bool CopyAddins { get; set; }
        public bool IsTesting { get; set; }
        public int SelectedProduct { get; set; }
        public string AdditionalResolutionDirectories { get; set; }

        public RunnerSetupData()
        {
            CleanUp = true;
            GroupingType = GroupingType.Fixture;
            Timeout = 120000;
        }

        public static IList<RevitProduct> FindRevit()
        {
            var products = RevitProductUtility.GetAllInstalledRevitProducts();

            //For now let's return all the installed products. Sometimes the products in development
            //might return Unknown version.
            //if (products.Any())
            //{
            //    products = products.Where(x => x.Version == RevitVersion.Revit2015 || x.Version==RevitVersion.Revit2016 || x.Version==RevitVersion.Revit2017).ToList();
            //}

            return products;
        }
    }
}
