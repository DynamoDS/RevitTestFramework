using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using System.Xml.Serialization;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Dynamo.NUnit.Tests;
using NUnit.Core;
using NUnit.Core.Filters;
using RevitServices.Persistence;
using RTF.Applications;
using RTF.Framework;

namespace RTF.Applications
{
    [Transaction(Autodesk.Revit.Attributes.TransactionMode.Automatic)]
    [Regeneration(RegenerationOption.Manual)]
    public class RevitTestFrameworkExternalApp : IExternalApplication
    {
        public static ControlledApplication ControlledApplication;

        public Result OnStartup(UIControlledApplication application)
        {
            

            //try
            //{
            //    ControlledApplication = application.ControlledApplication;
            //    IdlePromise.RegisterIdle(application);
            //    TransactionManager.SetupManager(new AutomaticTransactionStrategy());
                return Result.Succeeded;
            //}
            //catch
            //{
            //    return Result.Failed;
            //}
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
