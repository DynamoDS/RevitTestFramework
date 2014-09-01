using System.Collections.Generic;
using System.Collections.ObjectModel;
using Moq;
using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    [TestFixture]
    public class RunnerTests
    {
        private IAssemblyData data;

        [TestFixtureSetUp]
        public void Setup()
        {
            data = MockAssemblyData().Object;
        }

        [Test]
        public void RunCategory()
        {
            var runnerMock = new Mock<IRunner>();
            runnerMock.Setup(x => x.AddinPath).Returns(@"C:\MyAddin.addin");
            runnerMock.Setup(x => x.AssemblyPath).Returns(@"C:\TestAssembly.dll");
            runnerMock.Setup(x => x.Assemblies).Returns(new ObservableCollection<IAssemblyData>() {data});
            runnerMock.Setup(x => x.Category).Returns("Smoke");
            var cat = MockCategory("Smoke");
            runnerMock.Verify(x => x.SetupCategoryTests(cat.Object, false), Times.Once);
        }

        [Test]
        public void RunAssembly()
        {
            
        }

        [Test]
        public void RunFixture()
        {
            
        }

        [Test]
        public void RunTest()
        {
            
        }

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
            var test1 = MockTest("TestA", @"C:\modelA.rfa", fix1.Object);
            var test2 = MockTest("TestB", @"C:\modelB.rfa", fix1.Object);
            var test3 = MockTest("TestC", @"C:\modelC.rfa", fix1.Object);
            var test4 = MockTest("TestD", @"C:\modelC.rfa", fix2.Object);
            var test5 = MockTest("TestE", @"C:\modelC.rfa", fix2.Object);

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

            // Setup a mock assembly
            var mock = new Mock<IAssemblyData>();
            mock.Setup(x=>x.Name).Returns("RunnerTests");
            mock.Setup(x => x.Categories).Returns(cats);
            mock.Setup(x => x.Fixtures).Returns(fixes);
            mock.Setup(x => x.Path).Returns(@"C:\RunnerTests.dll");

            return mock;
        }

        private static Mock<IFixtureData> MockFixture(string fixtureName)
        {
            var fix1 = new Mock<IFixtureData>();
            fix1.Setup(x => x.Name).Returns(fixtureName);
            //fix1.Setup(x => x.Tests).Returns(tests);
            return fix1;
        }

        private static Mock<ICategoryData> MockCategory(string categoryName)
        {
            var cat1 = new Mock<ICategoryData>();
            cat1.Setup(x => x.Name).Returns(categoryName);
            //cat1.Setup(x => x.Tests).Returns(tests);
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
    }
}
