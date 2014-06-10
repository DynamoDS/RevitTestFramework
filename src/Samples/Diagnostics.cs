using Dynamo.Tests;
using NUnit.Framework;
using RevitTestFramework;

namespace RevitTestFrameworkRunner
{
    [TestFixture]
    class Diagnostics
    {
        [Test]
        [TestModel(@".\empty_2015.rfa")]
        public void WillShutdownBeforeProcessFinishes()
        {
            System.Threading.Thread.Sleep(300000);
            Assert.Fail("If you've made it here, the timeout was not honored.");
        }

        [Test]
        [TestModel(@".\AModelThatDoesNotExist.rfa")]
        public void WillShutdownIfJournalCompletionFails()
        {
            Assert.Fail("If you've made it here, the timeout was not honored.");
        }
    }
}
