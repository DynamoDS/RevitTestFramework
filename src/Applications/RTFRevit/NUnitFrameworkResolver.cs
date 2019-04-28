﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace RTF.Applications
{
    internal static class NUnitFrameworkResolver
    {
        private static bool isResolverSetup;

        public static void Setup()
        {
            if (isResolverSetup) return;
            AppDomain.CurrentDomain.AssemblyResolve += ResolveNUnitFramework;
            isResolverSetup = true;
        }

        private static Assembly ResolveNUnitFramework(object sender, ResolveEventArgs args)
        {
            if (!args.Name.Contains("nunit.framework")) return null;

            return Assembly.LoadFrom(GetNUnitFrameworkPath());
        }

        #region Path discovery helpers

        private static string GetNUnitFrameworkPath()
        {
            var frameworkPath = Path.Combine(GetNUnitRootDirectory(), "bin", "framework", "nunit.framework.dll");
            if (!File.Exists(frameworkPath))
            {
                throw new FileNotFoundException("It looks like NUnit is installed but could not find a needed assembly.  Have you installed NUnit correctly?");
            }

            return frameworkPath;
        }

        private static string GetNUnitRootDirectory()
        {
            var progFileDir = GetProgramFilesDirectory();

            // get all installed NUnit directories
            var nunitDirectories = Directory.GetDirectories(progFileDir, "NUnit*");

            if (nunitDirectories.Length == 0)
            {
                throw new FileLoadException("You must have NUnit 2.6.3 or greater installed!  Could not find NUnit in the Program Files directory.");
            }

            // should return the latest version
            return nunitDirectories.Last();
        }

        private static string GetProgramFilesDirectory()
        {
            var progFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

            // NUnit is x86 - make sure we use that
            if (!progFilesPath.EndsWith("(x86)"))
            {
                progFilesPath = progFilesPath + " (x86)";
            }

            // Fail early if we can't find the program files directory
            if (!Directory.Exists(progFilesPath))
            {
                throw new FileLoadException("Could not find the Program Files x86 directory!  Have you installed NUnit 2.6.3 or greater?");
            }

            return progFilesPath;
        }

        #endregion

    }
}
