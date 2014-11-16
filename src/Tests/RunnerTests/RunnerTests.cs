using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Moq;
using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    /// <summary>
    /// A sub-class of Runner for testing.
    /// </summary>
    public class TestRunner : Runner
    {
        public TestRunner(){}

        public TestRunner(IRunnerSetupData setupData) : base(setupData){}

        public override IList<IAssemblyData> ReadAssembly(string assemblyPath, string workingDirectory,
            GroupingType groupType, bool isTesting)
        {
            var dummyTestPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "RunnerTests.dll");
            var assData = new AssemblyData(dummyTestPath, "RunnerTests", groupType);

            var cat1 = new CategoryData(assData, "Smoke");
            var cat2 = new CategoryData(assData, "Integration");
            var cat3 = new CategoryData(assData, "Failure");
            var cat4 = new CategoryData(assData, "Mix");
            assData.Categories = new ObservableCollection<ITestGroup>() { cat1, cat2, cat3, cat4 };

            var fix1 = new FixtureData(assData, "FixtureA");
            var fix2 = new FixtureData(assData, "FixtureB");
            assData.Fixtures = new ObservableCollection<ITestGroup>() { fix1, fix2 };

            var testModelPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "empty.rfa");

            var test1 = new TestData(fix1, "TestA", testModelPath, false);
            var test2 = new TestData(fix1, "TestB", testModelPath, false);
            var test3 = new TestData(fix1, "TestC", testModelPath, false);
            var test4 = new TestData(fix2, "TestD", testModelPath, false);
            var test5 = new TestData(fix2, "TestE", @"C:\foo.rfa", false);

            cat1.Tests = new ObservableCollection<ITestData>() { test1, test2 };
            cat2.Tests = new ObservableCollection<ITestData>() { test3 };
            cat3.Tests = new ObservableCollection<ITestData>() { test4, test5 };
            cat4.Tests = new ObservableCollection<ITestData>() { test1, test3, test4};

            fix1.Tests = new ObservableCollection<ITestData>() { test1, test2, test3 };
            fix2.Tests = new ObservableCollection<ITestData>() { test4, test5 };

            fix1.Assembly = assData;
            fix2.Assembly = assData;
            cat1.Assembly = assData;
            cat2.Assembly = assData;
            cat3.Assembly = assData;

            return new List<IAssemblyData>{assData};
        }
    }

    [TestFixture]
    public class RunnerTests
    {
        private string workingDir;

        [SetUp]
        protected void TestSetup()
        {
            workingDir = Path.Combine(Path.GetTempPath(), "RTFTests" + Guid.NewGuid());
            if (!Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }
        }

        [TearDown]
        protected void TestTearDown()
        {
            if (Directory.Exists(workingDir))
            {
                Directory.Delete(workingDir, true);
            }
        }

        [Test]
        public void CanConstructDefaultRunner()
        {
            var setupData = new RunnerSetupData();
            Assert.DoesNotThrow(()=>new TestRunner(setupData));
        }

        //[Test]
        //public void CannotConstructRunnerWithoutRevit()
        //{
        //    var mockSetup = new Mock<RunnerSetupData>();
        //    mockSetup.Setup(x => x.Products).Returns(new List<RevitProduct>{});
        //    Assert.Throws(typeof(ArgumentException), () => new TestRunner(mockSetup.Object));
        //}

        [Test]
        public void CannotConstructRunnerWithBadWorkingDirectory()
        {
            var mockSetup = new Mock<RunnerSetupData>();
            mockSetup.Setup(x => x.WorkingDirectory).Returns("foo");
            Assert.Throws(typeof(ArgumentException), () => new TestRunner(mockSetup.Object));
        }

        [Test]
        public void CannotConstructRunnerWithBadTestAssembly()
        {
            var mockSetup = new Mock<RunnerSetupData>();
            mockSetup.Setup(x => x.TestAssembly).Returns("foo");
            Assert.Throws(typeof(ArgumentException), () => new TestRunner(mockSetup.Object));
        }

        [Test]
        public void RunByCategorySetup_Smoke()
        {
            var runner = new TestRunner(TestSetupData());
            
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var catData = runner.Assemblies.SelectMany(a => a.Categories).First(c => c.Name == "Smoke");
            ((IExcludable)catData).ShouldRun = true;

            Assert.AreEqual(runner.GetRunnableTests().Count(), catData.Tests.Count);
        }

        [Test]
        public void RunByCategorySetup_Integration()
        {
            var runner = new TestRunner(TestSetupData());
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var catData = runner.Assemblies.SelectMany(a => a.Categories).First(c => c.Name == "Integration");
            ((IExcludable)catData).ShouldRun = true;

            Assert.AreEqual(runner.GetRunnableTests().Count(), catData.Tests.Count);
        }

        [Test]
        public void RunByFixtureSetup_FixtureA()
        {
            var runner = new TestRunner(TestSetupData());
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var fixData = assData.Fixtures.First(c => c.Name == "FixtureA");
            ((IExcludable)fixData).ShouldRun = true;

            Assert.AreEqual(runner.GetRunnableTests().Count(), fixData.Tests.Count);
        }

        [Test]
        public void RunByFixtureSetup_FixtureB()
        {
            var runner = new TestRunner(TestSetupData());
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var fixData = assData.Fixtures.First(c => c.Name == "FixtureB");
            ((IExcludable)fixData).ShouldRun = true;

            Assert.AreEqual(runner.GetRunnableTests().Count(), fixData.Tests.Count);
        }

        [Test]
        public void RunByAssemblySetup()
        {
            var runner = new TestRunner(TestSetupData());
            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 5);
        }

        [Test]
        public void RunByTestSetup()
        {
            var runner = new TestRunner(TestSetupData());
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var testData = assData.Fixtures.First().Tests.First();
            testData.ShouldRun = true;
            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 1);  
        }

        [Test]
        public void BadModelPathReturnsNullJournal()
        {
            var runner = new TestRunner(TestSetupData());
            var testData = runner.Assemblies.SelectMany(a=>a.Fixtures).SelectMany(f => f.Tests).First(t => t.Name == "TestE");
            runner.SetupTests();
            Assert.IsNull(testData.JournalPath);
        }

        [Test]
        public void DoesNotRunCategoryThatIsExcluded()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true,
                ExcludedCategory = "Failure"
            };
            var runner = new TestRunner(setupData);
            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 3);
        }

        [Test]
        public void RunsTestIfSpecifiedInParameters()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true,
                Test = "TestA"
            };
            var runner = new TestRunner(setupData);
            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 1);
        }

        [Test]
        public void RunsFixtureIfSpecifiedInParameters()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true,
                Fixture = "FixtureA"
            };
            var runner = new TestRunner(setupData);
            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 3);
        }

        [Test]
        public void RunTwoSelectedCategories()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true,
            };
            var runner = new TestRunner(setupData);
            var assData = runner.Assemblies.First();
            assData.ShouldRun = false;

            var catData1 = runner.Assemblies.SelectMany(a => a.Categories).First(c => c.Name == "Smoke");
            ((IExcludable)catData1).ShouldRun = true;

            var catData2 = runner.Assemblies.SelectMany(a => a.Categories).First(c => c.Name == "Integration");
            ((IExcludable)catData2).ShouldRun = true;

            runner.SetupTests();
            Assert.AreEqual(runner.GetRunnableTests().Count(), 3);
        }

        [Test]
        public void TestInTwoCategories()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true,
            };
            var runner = new TestRunner(setupData);

            var test =
                runner.Assemblies.
                SelectMany(a => a.Categories).
                SelectMany(c => c.Tests).
                Where(t => t.Name == "TestA");

            Assert.AreEqual(test.Count(),2);
        }

        [Test]
        public void CanSerializeAndDeserialize()
        {
            var tmpResultsPath = Path.GetTempFileName();

            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = tmpResultsPath,
                Continuous = false,
                IsTesting = true,
                Fixture = "FixtureA",
                Timeout = 5,
            };

            var runner = new Runner(setupData);

            var testPath = Path.GetTempFileName();

            Runner.Save(testPath, runner);

            var newRunner = Runner.Load(testPath);

            Assert.AreEqual(newRunner.WorkingDirectory, workingDir);
            Assert.AreEqual(newRunner.DryRun, true);
            Assert.AreEqual(newRunner.Results, tmpResultsPath);
            Assert.AreEqual(newRunner.Continuous, false);
            Assert.AreEqual(newRunner.IsTesting, true);
            Assert.AreEqual(newRunner.Fixture, "FixtureA");
            Assert.AreEqual(newRunner.Timeout, 5);

            newRunner.Timeout = 10;

            var testPath2 = Path.GetTempFileName();

            Runner.Save(testPath2, newRunner);

            var newRunner2 = Runner.Load(testPath2);

            Assert.AreEqual(newRunner2.Timeout, 10);

        }

        #region private helper methods

        internal static Mock<AssemblyData> MockAssembly(string excludeCategory = null)
        {
            var dummyTestPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "RunnerTests.dll");

            // Setup a mock assembly
            var mock = new Mock<AssemblyData>();
            mock.Setup(x=>x.Name).Returns("RunnerTests");
            mock.Setup(x => x.Path).Returns(dummyTestPath);
            mock.CallBase = true;

            return mock;
        }

        private IRunnerSetupData TestSetupData()
        {
            var setupData = new RunnerSetupData
            {
                WorkingDirectory = workingDir,
                DryRun = true,
                Results = Path.GetTempFileName(),
                Continuous = false,
                IsTesting = true
            };

            return setupData;
        }

        #endregion
    }
}
