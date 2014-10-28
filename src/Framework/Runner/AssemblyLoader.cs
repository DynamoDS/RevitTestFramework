using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
                    (object s, ResolveEventArgs a) => resolver.Resolve(s, a, assemblyPath);
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
                modelPath = Path.GetFullPath(Path.Combine(workingDirectory, relModelPath));
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

    [Serializable]
    public class DefaultAssemblyResolver : RTFAssemblyResolver
    {
        public virtual Assembly Resolve(object sender, ResolveEventArgs args, string assemblyPath)
        {
            var dir = Path.GetDirectoryName(assemblyPath);
            var testFile = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(testFile))
            {
                return Assembly.ReflectionOnlyLoadFrom(testFile);
            }

            var dirInfo = new DirectoryInfo(dir);
            var assembly = SearchChildren(args, dirInfo);
            if (assembly != null)
            {
                return assembly;
            }

            // Search upstream of the test assembly
            for (var i = 0; i < 3; i++)
            {
                dirInfo = dirInfo.Parent;
                assembly = SearchChildren(args, dirInfo);
                if (assembly != null)
                {
                    return assembly;
                }

                testFile = Path.Combine(dirInfo.FullName, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(testFile))
                {
                    return Assembly.ReflectionOnlyLoadFrom(testFile);
                }
            }

            // If the above fail, attempt to load from the GAC
            var gacAssembly = Assembly.ReflectionOnlyLoad(args.Name);
            return gacAssembly ?? null;
        }

        private static Assembly SearchChildren(ResolveEventArgs args, DirectoryInfo dirInfo)
        {
            var children = dirInfo.GetDirectories();
            foreach (var child in children)
            {
                var testFile = Path.Combine(child.FullName, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(testFile))
                {
                    return Assembly.ReflectionOnlyLoadFrom(testFile);
                }
            }
            return null;
        }
    }
}
