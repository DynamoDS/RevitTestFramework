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

        [Test]
        public void TestThree()
        {
            //this will pass.
            Assert.AreEqual(0,0);
        }

        [Test]
        public void LeaveAMessage()
        {
            Assert.Pass("This test passed. Hooray!");
        }

        [Test] 
        [TestCaseSource("SetupManyTests")]
        public void RunManyTests(object a, object b)
        {
            Assert.IsTrue((int)a+(int)b <= 4);
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
