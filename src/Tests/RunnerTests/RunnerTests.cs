using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.RevitAddIns;
using Moq;
using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    [TestFixture]
    public class RunnerTests
    {
        private string workingDir;
        private IAssemblyData assemblyData;

        [SetUp]
        protected void TestSetup()
        {
            workingDir = Path.Combine(Path.GetTempPath(), "RTFTests" + Guid.NewGuid());
            if (!Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }
            assemblyData = MockAssemblyData().Object;
        }

        [TearDown]
        protected void TestTearDown()
        {
            if (Directory.Exists(workingDir))
            {
                Directory.Delete(workingDir, true);
            }
            assemblyData = null;
        }

        [Test]
        public void CanConstructDefaultRunner()
        {
            var setupData = new RunnerSetupData();
            Assert.DoesNotThrow(()=>Runner.Initialize(setupData));
        }

        [Test]
        public void CannotConstructRunnerWithoutRevit()
        {
            var mockSetup = new Mock<IRunnerSetupData>();
            mockSetup.Setup(x => x.Products).Returns(new List<RevitProduct>{});
            Assert.Throws(typeof(ArgumentException), () => Runner.Initialize(mockSetup.Object));
        }

        [Test]
        public void CannotConstructRunnerWithBadWorkingDirectory()
        {
            var mockSetup = new Mock<IRunnerSetupData>();
            mockSetup.Setup(x => x.WorkingDirectory).Returns("foo");
            Assert.Throws(typeof(ArgumentException), () => Runner.Initialize(mockSetup.Object));
        }

        [Test]
        public void CannotConstructRunnerWithBadTestAssembly()
        {
            var mockSetup = new Mock<IRunnerSetupData>();
            mockSetup.Setup(x => x.TestAssembly).Returns("foo");
            Assert.Throws(typeof(ArgumentException), () => Runner.Initialize(mockSetup.Object));
        }

        [Test]
        public void RunByCategorySetup_Smoke()
        {
            var catData = assemblyData.Categories.First(c => c.Name == "Smoke");
            var runner = SetupToRun();
            runner.SetupTests(catData);
            Assert.AreEqual(runner.RunCount, 2);
            Assert.AreEqual(runner.TestDictionary.Count, 2);
        }

        [Test]
        public void RunByCategorySetup_Integration()
        {
            var catData = assemblyData.Categories.First(c => c.Name == "Integration");
            var runner = SetupToRun();
            runner.SetupTests(catData);
            Assert.AreEqual(runner.RunCount, 1);
            Assert.AreEqual(runner.TestDictionary.Count, 1);
        }

        [Test]
        public void RunByFixtureSetup_FixtureA()
        {
            var fixData = assemblyData.Fixtures.First(c => c.Name == "FixtureA");
            var runner = SetupToRun();
            runner.SetupTests(fixData);
            Assert.AreEqual(runner.RunCount, 3);
            Assert.AreEqual(runner.TestDictionary.Count, 3);
        }

        [Test]
        public void RunByFixtureSetup_FixtureB()
        {
            var fixData = assemblyData.Fixtures.First(c => c.Name == "FixtureB");
            var runner = SetupToRun();
            runner.SetupTests(fixData);
            Assert.AreEqual(runner.RunCount, 2);
            Assert.AreEqual(runner.TestDictionary.Count, 2);
        }

        [Test]
        public void RunByAssemblySetup()
        {
            var runner = SetupToRun();
            runner.SetupTests(assemblyData);
            Assert.AreEqual(runner.RunCount, 5);
            Assert.AreEqual(runner.TestDictionary.Count, 5);
        }

        [Test]
        public void RunByTestSetup()
        {
            var runner = SetupToRun();
            runner.SetupTests(assemblyData.Fixtures.First().Tests.First());
            Assert.AreEqual(runner.RunCount, 1);
            Assert.AreEqual(runner.TestDictionary.Count, 1);  
        }

        [Test]
        public void BadModelPathReturnsNullJournal()
        {
            var testData = assemblyData.Fixtures.SelectMany(f=>f.Tests).First(t=>t.Name == "TestE");
            var runner = SetupToRun();
            runner.SetupTests(testData);
            Assert.IsNull(runner.TestDictionary[testData]);
        }

        #region private helper methods

        private Mock<IAssemblyData> MockAssemblyData()
        {
            // Setup some mock categories
            var cat1 = MockCategory("Smoke");
            var cat2 = MockCategory("Integration");
            var cat3 = MockCategory("Failure");

            var cats = new ObservableCollection<IGroupable>(){cat1.Object, cat2.Object, cat3.Object};

            // Setup some mock fixtures
            var fix1 = MockFixture("FixtureA");
            var fix2 = MockFixture("FixtureB");

            var fixes = new ObservableCollection<IGroupable>() { fix1.Object, fix2.Object};

            // Setup some mock tests
            var testModelPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "empty.rfa");

            var test1 = MockTest("TestA", testModelPath, fix1.Object);
            var test2 = MockTest("TestB", testModelPath, fix1.Object);
            var test3 = MockTest("TestC", testModelPath, fix1.Object);
            var test4 = MockTest("TestD", testModelPath, fix2.Object);
            var test5 = MockTest("TestE", @"C:\foo.rfa", fix2.Object);

            test1.Object.TestStatus = Framework.TestStatus.Failure;
            test2.Object.TestStatus = Framework.TestStatus.Failure;
            test3.Object.TestStatus = Framework.TestStatus.Success;
            test4.Object.TestStatus = Framework.TestStatus.Success;
            test5.Object.TestStatus = Framework.TestStatus.Inconclusive;

            var catTests1 = new ObservableCollection<ITestData>() { test1.Object, test2.Object };
            var catTests2 = new ObservableCollection<ITestData>() { test3.Object };
            var catTests3 = new ObservableCollection<ITestData>() { test4.Object, test5.Object };

            var fixTests1 = new ObservableCollection<ITestData>() { test1.Object, test2.Object, test3.Object };
            var fixTests2 = new ObservableCollection<ITestData>() { test4.Object, test5.Object };

            fix1.Setup(x => x.Tests).Returns(fixTests1);
            fix2.Setup(x => x.Tests).Returns(fixTests2);
            cat1.Setup(x => x.Tests).Returns(catTests1);
            cat2.Setup(x => x.Tests).Returns(catTests2);
            cat3.Setup(x => x.Tests).Returns(catTests3);

            var dummyTestPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "RunnerTests.dll");

            // Setup a mock assembly
            var mock = new Mock<IAssemblyData>();
            mock.Setup(x=>x.Name).Returns("RunnerTests");
            mock.Setup(x => x.Categories).Returns(cats);
            mock.Setup(x => x.Fixtures).Returns(fixes);
            mock.Setup(x => x.Path).Returns(dummyTestPath);

            // Link the fixture back to the assembly
            fix1.Setup(f => f.Assembly).Returns(mock.Object);
            fix2.Setup(f => f.Assembly).Returns(mock.Object);

            return mock;
        }

        private static Mock<IFixtureData> MockFixture(string fixtureName)
        {
            var fix1 = new Mock<IFixtureData>();
            fix1.Setup(x => x.Name).Returns(fixtureName);
            return fix1;
        }

        private static Mock<ICategoryData> MockCategory(string categoryName)
        {
            var cat1 = new Mock<ICategoryData>();
            cat1.Setup(x => x.Name).Returns(categoryName);
            return cat1;
        }

        private static Mock<ITestData> MockTest(string testName, string modelPath, IFixtureData fixture)
        {
            var test1 = new Mock<ITestData>();
            test1.Setup(x => x.Name).Returns(testName);
            test1.Setup(x => x.ModelPath).Returns(modelPath);
            test1.Setup(x => x.Fixture).Returns(fixture);
            return test1;
        }

        private Runner SetupToRun()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
            };

            var runner = Runner.Initialize(setupData);
            runner.Assemblies.Clear();
            runner.Assemblies.Add(assemblyData);
            return runner;
        }

        #endregion
    }
}
