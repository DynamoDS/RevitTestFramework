using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using NUnit.Framework;
using RTF.Applications;
using RTF.Framework;

namespace RTF.Tests
{
    [TestFixture]
    public class FixtureOne
    {
        [SetUp]
        public void Setup()
        {
            //startup logic executed before every test
        }

        [TearDown]
        public void Shutdown()
        {
            //shutdown logic executed after every test
        }

        /// <summary>
        /// This is the Hello World of Revit testing. Here we
        /// simply call the Revit API to create a new ReferencePoint
        /// in the default empty.rfa file.
        /// </summary>
        [Test]
        public void CanCreateAReferencePoint()
        {
            var doc = RevitTestExecutive.CommandData.Application.ActiveUIDocument.Document;

            using (var t = new Transaction(doc))
            {
                if (t.Start("Test one.") == TransactionStatus.Started)
                {
                    //create a reference point
                    var pt = doc.FamilyCreate.NewReferencePoint(new XYZ(5, 5, 5));

                    if (t.Commit() != TransactionStatus.Committed)
                    {
                        t.RollBack();
                    }
                }
                else
                {
                    throw new Exception("Transaction could not be started.");
                }
            }

            //verify that the point was created
            var collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof (ReferencePoint));

            Assert.AreEqual(1, collector.ToElements().Count);
        }

        /// <summary>
        /// Using the TestModel parameter, you can specify a Revit model
        /// to be opened prior to executing the test. The model path specified
        /// in this attribute is relative to the working directory.
        /// </summary>
        [Test]
        [TestModel(@"./bricks.rfa")]
        public void ModelHasTheCorrectNumberOfBricks()
        {
            var doc = RevitTestExecutive.CommandData.Application.ActiveUIDocument.Document;

            var fec = new FilteredElementCollector(doc);
            fec.OfClass(typeof(FamilyInstance));

            var bricks = fec.ToElements()
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Name == "brick");

            Assert.AreEqual(bricks.Count(), 4);
        }

        /// <summary>
        /// NUnit allows the creation of a parameterized test. The SetupManyTests
        /// method is responsible for creating sets of parameter that are then
        /// passed into this test method, one by one. This could be used, for example,
        /// to iterate over all the Revit files in a folder, and pass the path to the
        /// model into the test as a parameter.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        [Test] 
        [TestCaseSource("SetupManyTests")]
        public void RunManyTests(object a, object b)
        {
            Assert.IsTrue((int)a > 0);
            Assert.IsTrue((int)b > 0);
        }

        private static List<object[]> SetupManyTests()
        {
            var testParams = new List<object[]>
            {
                new object[] {1, 1}, 
                new object[] {2, 2}, 
                new object[] {3, 3}
            };

            return testParams;
        }

        /// <summary>
        /// Opens and activates a new model, and closes the old model.
        /// </summary>
        private void SwapCurrentModel(string modelPath)
        {
            var app = RevitTestExecutive.CommandData.Application;
            var doc = RevitTestExecutive.CommandData.Application.ActiveUIDocument.Document;

            app.OpenAndActivateDocument(modelPath);
            doc.Close(false);
        }
    }
}
