using System;
using System.Reflection;

namespace RTF.Framework
{
    public interface RTFAssemblyResolver
    {
        Assembly Resolve(object sender, ResolveEventArgs args, string assemblyPath);
    }
}
