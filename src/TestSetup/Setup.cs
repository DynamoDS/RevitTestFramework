using System.Diagnostics;
using RTF.Framework;

namespace TestSetup
{
    public class TestSetup : IRTFSetup
    {
        public void Setup()
        {
            Debug.WriteLine("Setup method hit.");
        }
    }
}
