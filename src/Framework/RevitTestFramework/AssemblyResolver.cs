using System;
using System.Collections.Generic;
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
            Console.WriteLine("Attempting to resolve referenced assembliy: {0}", args.Name);

            // Search the local directory
            var dir = Path.GetDirectoryName(args.RequestingAssembly.Location);
            var testFile = Path.Combine(dir, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(testFile))
            {
                Console.WriteLine("Found assembly:{0}", testFile);
                return Assembly.ReflectionOnlyLoadFrom(testFile);
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

            // If the above fail, attempt to load from the GAC
            try
            {
                return Assembly.ReflectionOnlyLoad(args.Name);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
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
    }
}
