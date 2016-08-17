using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RTF.Applications
{
    [Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RevitTestFrameworkExternalApp : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.UsingCommandData)]
    public class RevitTestFramework : IExternalCommand
    {
        public Result Execute(ExternalCommandData cmdData, ref string message, ElementSet elements)
        {
            Setup(cmdData);

            var exe = new RevitTestExecutive();
            return exe.Execute(cmdData, ref message, elements);
        }

        private void Setup(ExternalCommandData cmdData)
        {
            IDictionary<string, string> dataMap = cmdData.JournalData;

            if (dataMap.ContainsKey("debug") && dataMap["debug"].ToLower() == "true")
            {
                Debugger.Launch();
            }

            NUnitFrameworkResolver.Setup();
        }
    }
}
