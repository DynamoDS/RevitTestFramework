using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace RTF.Framework
{

    /// <summary>
    /// The AssemblyLoader class is used during reflection only load
    /// in an RTF app domain to allow non-locking loading of assemblies.
    /// </summary>
    [Serializable]
    public class AssemblyLoader : MarshalByRefObject
    {
        public AssemblyLoader(RTFAssemblyResolver resolver)
        {  
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += resolver.Resolve;
        }

        public AssemblyData ReadAssembly(string assemblyPath, GroupingType groupType, string workingDirectory)
        {
            // NOTE: We use reflection only load here so that we don't have to resolve all binaries
            // This is an assumption by Dynamo tests which reference assemblies that can be resolved 
            // at runtime inside Revit.
            var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);

            var data = new AssemblyData(assemblyPath, assembly.GetName().Name, groupType);

            try
            {
                var revitReference = assembly.GetReferencedAssemblies().FirstOrDefault(x => x.Name.Contains("RevitAPI"));

                if (revitReference != null)
                {
                    data.ReferencedRevitVersion = $"{(revitReference.Version.Major + 2000)}";
                }

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
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine($"ERROR: Failed to resolve assembly:");
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"ERROR: {ex.LoaderExceptions}");
                throw new Exception("A referenced type could not be loaded.");
            }
        }

        public static bool ReadFixture(Type fixtureType, IAssemblyData data, string workingDirectory)
        {
            var fixtureAttribs = CustomAttributeData.GetCustomAttributes(fixtureType);

            if (!fixtureAttribs.Any(x => x.Constructor.DeclaringType.Name == nameof(TestFixtureAttribute)))
            {
                //Console.WriteLine("Specified fixture does not have the required TestFixture attribute.");
                return false;
            }

            var fixData = new FixtureData(data, fixtureType.Name);
            data.Fixtures.Add(fixData);

            foreach (var test in fixtureType.GetMethods())
            {
                var testAttribs = CustomAttributeData.GetCustomAttributes(test);

                if (!testAttribs.Any(x => x.Constructor.DeclaringType.Name == nameof(TestAttribute)))
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
            List<string> modelPaths = new List<string>();

            var testAttribs = CustomAttributeData.GetCustomAttributes(test);

            if (testAttribs.Any(x => x.Constructor.DeclaringType.Name == nameof(IgnoreAttribute)))
            {
                return false;
            }

            var testModelAttrib = testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == nameof(TestModelAttribute));

            if (testModelAttrib != null)
            {
                string absolutePath;

                // We can't get the instantiated attribute from the assembly because we performed a ReflectionOnly load
                TestModelAttribute testModelAttribute = new TestModelAttribute((string)testModelAttrib.ConstructorArguments.First().Value);

                if (Path.IsPathRooted(testModelAttribute.Path))
                {
                    absolutePath = testModelAttribute.Path;
                }
                else
                {
                    if (workingDirectory == null)
                    {
                        // If the working directory is not specified.
                        // Add the relative path to the assembly's path.
                        absolutePath = Path.GetFullPath(
                            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), testModelAttribute.Path));
                    }
                    else
                    {
                        absolutePath = Path.GetFullPath(Path.Combine(workingDirectory, testModelAttribute.Path));
                    }
                }

                if (testModelAttribute.IsWildcard)
                {
                    string[] modelFiles = null;
                    try
                    {
                        modelFiles = Directory.GetFiles(Path.GetDirectoryName(absolutePath), Path.GetFileName(absolutePath), SearchOption.AllDirectories);
                    }
                    catch
                    {
                        // Means folder doesn't exist
                    }

                    if (modelFiles == null || modelFiles.Length == 0)
                    {
                        modelFiles = new string[] { absolutePath };
                    }

                    modelPaths.AddRange(modelFiles);
                }
                else
                {
                    modelPaths.Add(absolutePath);
                }
            }
            else
            {
                //set the default modelPath to the empty.rfa file that will live in the build directory
                modelPaths.Add(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "empty.rfa"));
            }

            var runDynamoAttrib = testAttribs.FirstOrDefault(x => x.Constructor.DeclaringType.Name == nameof(RunDynamoAttribute));
            var runDynamo = false;

            if (runDynamoAttrib != null)
            {
                runDynamo = (bool)runDynamoAttrib.ConstructorArguments.FirstOrDefault().Value;
            }

            foreach (string modelPath in modelPaths)
            {
                var testData = new TestData(data, test.Name, modelPath, runDynamo);
                data.Tests.Add(testData);

                const string EmptyCategory = "[NO CATEGORY]";

                var category = string.Empty;
                var categoryAttribs =
                    testAttribs.Where(x => x.Constructor.DeclaringType.Name == nameof(CategoryAttribute));

                if (categoryAttribs.Any())
                {
                    foreach (var categoryAttrib in categoryAttribs)
                    {
                        category = categoryAttrib.ConstructorArguments.FirstOrDefault().Value.ToString();
                        if (String.IsNullOrEmpty(category))
                        {
                            category = EmptyCategory;
                        }

                        AddWithCategory(data, category, testData);
                    }
                }
                else
                {
                    AddWithCategory(data, EmptyCategory, testData);
                }

                Console.WriteLine($"Loaded test: {testData} ({modelPath})");
            }

            return true;
        }

        private static void AddWithCategory(IFixtureData data, string category, TestData testData)
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
}
