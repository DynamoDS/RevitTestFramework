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
            var dir = Path.GetDirectoryName(args.RequestingAssembly.Location);
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

            // Search each of the additional load paths
            foreach (var path in AdditionalResolutionDirectories)
            {
                var result = AttemptLoadFromDirectory(args, path);
                if (result != null)
                    return result;
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

            testFile = Path.Combine(revitDirectory, new AssemblyName(args.Name).Name + ".dll");
            if (File.Exists(testFile))
            {
                return Assembly.ReflectionOnlyLoadFrom(testFile);
            }

            // If the above fail, attempt to load from the GAC
            try
            {
                return Assembly.ReflectionOnlyLoad(args.Name);
            }
            catch
            {
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
