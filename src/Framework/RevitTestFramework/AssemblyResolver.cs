using System;
using System.IO;
using System.Reflection;

namespace RTF.Framework
{
    public interface RTFAssemblyResolver
    {
        Assembly Resolve(object sender, ResolveEventArgs args);
    }

    [Serializable]
    public class DefaultAssemblyResolver : RTFAssemblyResolver
    {
        private string revitDirectory;

        public DefaultAssemblyResolver(string revitDirectory)
        {
            this.revitDirectory = revitDirectory;
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
