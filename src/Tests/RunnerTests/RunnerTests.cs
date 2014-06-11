using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security.AccessControl;
using Moq;
using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    [TestFixture]
    public class RunnerTests
    {
        [Test]
        public void RunnerSetup()
        {
            var runner = new Runner(){Gui=false};

            Assert.IsNull(runner.WorkingDirectory);
            Assert.IsNull(runner.TestAssembly);
            Assert.IsNull(runner.RevitPath);
            Assert.AreEqual(runner.SelectedProduct, 0);
            Assert.IsFalse(runner.Gui);
            Assert.IsFalse(runner.IsDebug);
            Assert.AreEqual(runner.Assemblies.Count, 0);
        }

        [Test]
        public void CanFindRevit()
        {
            var products = Runner.FindRevit();
            Assert.GreaterOrEqual(products.Count, 1, "No Revit products could be found, or Revit is not installed on this machine.");
        }
    }
}
