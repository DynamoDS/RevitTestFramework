using System;
using System.IO;
using System.Xml.Serialization;
using Dynamo.NUnit.Tests;

namespace RTF.Framework
{
    public static class TestResultDeserializer
    {
        public static resultType DeserializeResults(string resultsPath)
        {
            if (!File.Exists(resultsPath))
            {
                return null;
            }

            resultType results = null;

            //write to the file
            var x = new XmlSerializer(typeof(resultType));
            using (var reader = new StreamReader(resultsPath))
            {
                results = (resultType)x.Deserialize(reader);
            }

            return results;
        }

        public static string TryGetFailureMessage(string resultsPath)
        {
            if (!File.Exists(resultsPath))
            {
                return "The RevitTestExecutive did write an error message. " +
                    "This is likely because of an internal Revit exception " +
                    "before the test was run.  Try debugging.";
            }

            try
            {
                using (var sr = new StreamReader(resultsPath))
                {
                    return sr.ReadToEnd();
                }
            }
            catch (Exception e)
            {
                return String.Format("Could not parse the result file at {0}.  There is something wrong with the " +
                                     "output file emitted by the RevitTestExecutive.", resultsPath);
            }
        }

    }
}
