using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using System.Linq;

namespace RTF.Framework
{
    static class AddinHelpers
    {
        /// <summary>
        /// this method modifies Addin files so that their assembly paths
        /// are fully qualified
        /// </summary>
        /// <param name="addinFile"></param>
        /// <returns></returns>
        public static void FullyQualifyAddinPaths (FileInfo addinFile)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(addinFile.FullName);

            foreach(XmlElement addinElement in doc.DocumentElement.ChildNodes)
            {
                //if this element is an addin attempt to make the assembly path a full path
             if (addinElement.LocalName != "Addin")
                {
                    continue; 
                }
                var assemblies = addinElement.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "Assembly");
                var paths = assemblies.Select(x => x.InnerText);
                var files = paths.Select(x => new FileInfo(x));
                var fullpaths = files.Select(x => x.FullName);

                assemblies.ToList().Zip(fullpaths, (asm, path) => asm.InnerText = path).ToList();
            }
            doc.Save(addinFile.FullName);
        }
    }
}
