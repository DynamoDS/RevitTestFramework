using System;
using NUnit.Framework;
using RTF.Framework;

namespace RTF.Tests
{
    class ParsingTests
    {
        [Test]
        public void CanParseRequiredFlags()
        {
            var args = new []
            {
                @"-dir=C:\foo",
                @"-a=C:\foo\bar.dll",
                @"-r=C:\results.txt"
            };

            var setupData = Runner.ParseCommandLineArguments(args);
            Assert.AreEqual(setupData.WorkingDirectory, @"C:\foo");
            Assert.AreEqual(setupData.TestAssembly, @"C:\foo\bar.dll");
            Assert.AreEqual(setupData.Results, @"C:\results.txt");
        }

        [Test]
        public void CanParseRequiredFlagsSemicolon()
        {
            var args = new[]
            {
                @"-dir:C:\foo",
                @"-a:C:\foo\bar.dll",
                @"-r:C:\results.txt"
            };

            var setupData = Runner.ParseCommandLineArguments(args);
            Assert.AreEqual(setupData.WorkingDirectory, @"C:\foo");
            Assert.AreEqual(setupData.TestAssembly, @"C:\foo\bar.dll");
            Assert.AreEqual(setupData.Results, @"C:\results.txt");
        }

        [Test]
        public void ThrowsIfResultsPathUnset()
        {
            // Leave out the results path
            var args = new[]
            {
                @"-dir=C:\foo",
                @"-a=C:\foo\bar.dll",
            };

            Assert.Throws<Exception>(()=>Runner.ParseCommandLineArguments(args));
        }

        [Test]
        public void ThrowsIfWorkingDirectoryPathUnset()
        {
            // Leave out the results path
            var args = new[]
            {
                @"-a=C:\foo\bar.dll",
                @"-r=C:\results.txt"
            };

            Assert.Throws<Exception>(() => Runner.ParseCommandLineArguments(args));
        }

        [Test]
        public void ThrowsIfTestAssemblyPathUnset()
        {
            // Leave out the results path
            var args = new[]
            {
                @"-dir=C:\foo",
                @"-r=C:\results.txt"
            };

            Assert.Throws<Exception>(() => Runner.ParseCommandLineArguments(args));
        }

        [Test]
        public void ParsesDebugFlags()
        {
            var args = new[]
            {
                @"-dir=C:\foo",
                @"-a=C:\foo\bar.dll",
                @"-r=C:\results.txt",
                "-c+",
                "-dry",
                "-x-",
                "-continuous+"
            };

            var setupData = Runner.ParseCommandLineArguments(args);
            Assert.True(setupData.Concat);
            Assert.True(setupData.DryRun);
            Assert.False(setupData.CleanUp);
            Assert.True(setupData.Continuous);
        }

        [Test]
        public void ParseExclude()
        {
            var args = new[]
            {
                @"-dir=C:\foo",
                @"-a=C:\foo\bar.dll",
                @"-r=C:\results.txt",
                "-exclude:Failure"
            };

            var setupData = Runner.ParseCommandLineArguments(args);
            Assert.AreEqual("Failure", setupData.ExcludedCategory);
        }

    }
}
