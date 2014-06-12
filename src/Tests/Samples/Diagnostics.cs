using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    [TestFixture]
    class Diagnostics
    {
        [Test]
        [TestModel(@".\empty.rfa")]
        public void WillShutdownBeforeProcessFinishes()
        {
            System.Threading.Thread.Sleep(300000);
            Assert.Fail("If you've made it here, the timeout was not honored.");
        }

        [Test]
        [TestModel(@".\AModelThatDoesNotExist.rfa")]
        public void FailsWithBadModelPath()
        {
            Assert.Fail("If you've made it here, a bad model path was passed. ");
        }
    }
}
