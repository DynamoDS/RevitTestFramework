using System;

namespace RTF.Framework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class TestModelAttribute : Attribute
    {
        public string Path { get; private set; }
        public bool IsWildcard => (Path.ToLowerInvariant().EndsWith("*.rvt") || Path.ToLowerInvariant().EndsWith("*.rfa"));

        public TestModelAttribute(string path)
        {
            Path = path;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RunDynamoAttribute : Attribute
    {
        public bool RunDynamo { get; private set; }
        public RunDynamoAttribute(bool run)
        {
            RunDynamo = run;
        }
    }
}
