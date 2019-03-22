using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Reflection;

namespace RTF.Framework
{
    public interface RTFAssemblyResolver
    {
        Assembly Resolve(object sender, ResolveEventArgs args);
        List<string> AdditionalResolutionDirectories { get; set; }
    }

    [Serializable]
    public class DefaultAssemblyResolver : RTFAssemblyResolver
    {
        private string revitDirectory;
        private string dynamoRuntimeDirectory;

        public List<string> AdditionalResolutionDirectories { get; set; }

        public DefaultAssemblyResolver(string revitDirectory)
        {
            this.revitDirectory = revitDirectory;
            AdditionalResolutionDirectories = new List<string>();
        }

        public DefaultAssemblyResolver(string revitDirectory, List<string> additionalDirectories)
        {
            this.revitDirectory = revitDirectory;
            AdditionalResolutionDirectories = additionalDirectories;
        }

        public virtual Assembly Resolve(object sender, ResolveEventArgs args)
        {
            Console.WriteLine("Attempting to resolve referenced assembly: {0}", args.Name);

            var dir = Path.GetDirectoryName(args.RequestingAssembly.Location);
            var testFile = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(testFile))
            {
                Console.WriteLine("Found assembly:{0}", testFile);
                return Assembly.ReflectionOnlyLoadFrom(testFile);
            }

            var dirInfo = new DirectoryInfo(dir);
            var assembly = SearchChildren(args, dirInfo);
            if (assembly != null)
            {
                Console.WriteLine("Found assembly:{0}", assembly.Location);
                return assembly;
            }

            //Try to get DynamoRuntimeDirectory and include it to addtion resolution paths
            if(string.IsNullOrEmpty(dynamoRuntimeDirectory))
            {
                dynamoRuntimeDirectory = TryGetDynamoCoreRuntimeFromConfig(dirInfo, true);
                if(!string.IsNullOrEmpty(dynamoRuntimeDirectory))
                {
                    AdditionalResolutionDirectories.Add(dynamoRuntimeDirectory);
                }
            }

            // Search each of the additional load paths
            foreach (var path in AdditionalResolutionDirectories)
            {
                var result = AttemptLoadFromDirectory(args, path);
                if (result != null)
                {
                    Console.WriteLine("Found assembly:{0}", result.Location);
                    return result;
                }
            }

            // Search upstream of the test assembly
            for (var i = 0; i < 3; i++)
            {
                dirInfo = dirInfo.Parent;
                assembly = SearchChildren(args, dirInfo);
                if (assembly != null)
                {
                    Console.WriteLine("Found assembly:{0}", assembly.Location);
                    return assembly;
                }

                testFile = Path.Combine(dirInfo.FullName, new AssemblyName(args.Name).Name + ".dll");
                if (File.Exists(testFile))
                {
                    return Assembly.ReflectionOnlyLoadFrom(testFile);
                }
            }

            testFile = Path.Combine(revitDirectory, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(testFile))
            {
                Console.WriteLine("Found assembly:{0}", testFile);
                return Assembly.ReflectionOnlyLoadFrom(testFile);
            }

            // If the above fail, attempt to load from the GAC
            try
            {
                return Assembly.ReflectionOnlyLoad(args.Name);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to resolve assembly with error: {ex.Message}");
                return null;
            }
        }

        private Assembly AttemptLoadFromDirectory(ResolveEventArgs args, string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return null;

            var dirInfo = new DirectoryInfo(directory);

            var testFile = Path.Combine(dirInfo.FullName, new AssemblyName(args.Name).Name + ".dll");
            return !File.Exists(testFile) ? null : Assembly.ReflectionOnlyLoadFrom(testFile);
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

        /// <summary>
        /// Tries to get Dynamo Core Runtime path from Dynamo.config file, given 
        /// a directory. This method searches for Dynamo.config file in the 
        /// given folder, if present returns DynamoRuntime app setting value from config.
        /// </summary>
        /// <param name="directory">DirectoryInfo to locate Dynamo.config file</param>
        /// <param name="searchParent">Whether to search in parent folder</param>
        /// <returns>Returns Dynamo Runtime path if successful, else string.Empty</returns>
        private static string TryGetDynamoCoreRuntimeFromConfig(DirectoryInfo directory, bool searchParent)
        {
            var configPath = Path.Combine(directory.FullName, "Dynamo.config");
            if (!File.Exists(configPath))
            {
                return searchParent ? TryGetDynamoCoreRuntimeFromConfig(directory.Parent, false) : string.Empty;
            }
        
            // Get DynamoCore path from the Dynamo.config file, if it exists
            var map = new ExeConfigurationFileMap() { ExeConfigFilename = configPath };

            var config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            var runtime = config.AppSettings.Settings["DynamoRuntime"];
            if (runtime != null)
            {
                return runtime.Value;
            }
            return string.Empty;
        }
    }
}
