using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Practices.Prism.Modularity;

namespace RTF.Framework
{

    /// <summary>
    /// The AssemblyLoader class is used during reflection only load
    /// in an RTF app domain to allow non-locking loading of assemblies.
    /// </summary>
    [Serializable]
    public class AssemblyLoader : MarshalByRefObject
    {
        public AssemblyLoader(string assemblyPath, RTFAssemblyResolver resolver)
        {  
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve +=
                    resolver.Resolve;
        }

        public AssemblyData ReadAssembly(string assemblyPath, GroupingType groupType, string workingDirectory)
        {
            var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);

            var data = new AssemblyData(assemblyPath, assembly.GetName().Name, groupType);

            foreach (var fixtureType in assembly.GetTypes())
            {
                if (!ReadFixture(fixtureType, data, workingDirectory))
                {
                    //Console.WriteLine(string.Format("Journals could not be created for {0}", fixtureType.Name));
                }
            }

            data.Fixtures = data.Fixtures.Sorted(x => x.Name);
            return data;
        }

        public static bool ReadFixture(Type fixtureType, IAssemblyData data, string workingDirectory)
        {
            var fixtureAttribs = CustomAttributeData.GetCustomAttributes(fixtureType);

            if (!fixtureAttribs.Any(x => x.Constructor.DeclaringType.Name == "TestFixtureAttribute"))
            {
                //Console.WriteLine("Specified fixture does not have the required TestFixture attribute.");
                return false;
            }

            var fixData = new FixtureData(data, fixtureType.Name);
            data.Fixtures.Add(fixData);

            foreach (var test in fixtureType.GetMethods())
            {
                var testAttribs = CustomAttributeData.GetCustomAttributes(test);

                if (!testAttribs.Any(x => x.Constructor.DeclaringType.Name == "TestAttribute"))
                {
                    // skip this method
                    continue;
                }

                if (!ReadTest(test, fixData, workingDirectory))
                {
                    //Console.WriteLine(string.Format("Journal could not be created for test:{0} in fixture:{1}", _test,_fixture));
                    continue;
                }
            }

            // sort the collection
            fixData.Tests = fixData.Tests.Sorted(x => x.Name);

            return true;
        }

        public static bool ReadTest(MethodInfo test, IFixtureData data, string workingDirectory)
        {
            //set the default modelPath to the empty.rfa file that will live in the build directory
            string modelPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "empty.rfa");

            var testAttribs = CustomAttributeData.GetCustomAttributes(test);

            var testModelAttrib =
                testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == "TestModelAttribute");

            if (testModelAttrib != null)
            {
                //overwrite the model path with the one
                //specified in the test model attribute
                var relModelPath = testModelAttrib.ConstructorArguments.FirstOrDefault().Value.ToString();
                if (workingDirectory == null)
                {
                    // If the working directory is not specified.
                    // Add the relative path to the assembly's path.
                    modelPath =
                        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                            relModelPath));
                }
                else
                {
                    modelPath = Path.GetFullPath(Path.Combine(workingDirectory, relModelPath)); 
                }
            }

            var runDynamoAttrib =
                testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == "RunDynamoAttribute");

            var runDynamo = false;
            if (runDynamoAttrib != null)
            {
                runDynamo = Boolean.Parse(runDynamoAttrib.ConstructorArguments.FirstOrDefault().Value.ToString());
            }

            var testData = new TestData(data, test.Name, modelPath, runDynamo);
            data.Tests.Add(testData);

            var category = string.Empty;
            var categoryAttribs =
                testAttribs.Where(x => x.Constructor.DeclaringType.Name == "CategoryAttribute");
            foreach (var categoryAttrib in categoryAttribs)
            {
                category = categoryAttrib.ConstructorArguments.FirstOrDefault().Value.ToString();
                if (!String.IsNullOrEmpty(category))
                {
                    var cat = data.Assembly.Categories.FirstOrDefault(x => x.Name == category);
                    if (cat != null)
                    {
                        cat.Tests.Add(testData);
                    }
                    else
                    {
                        var catData = new CategoryData(data.Assembly, category);
                        catData.Tests.Add(testData);
                        data.Assembly.Categories.Add(catData);
                    }
                }
            }

            return true;
        }
    }
}
