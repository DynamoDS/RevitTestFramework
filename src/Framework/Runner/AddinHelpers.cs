using System.IO;
using System.Linq;
using System.Xml;

namespace RTF.Framework
{
    static class AddinHelpers
    {
        /// <summary>
        /// this method modifies the copied addin so that its assembly is fully pathed to the location
        /// where the original addin pointed, in the case where the assembly path was already a full path, nothing is done
        /// </summary>
        /// <param name="copiedAddinFile"></param>
        /// <param name="originalAddin"></param>
        public static void FullyQualifyAddinPaths (FileInfo copiedAddinFile, FileInfo originalAddin)
        {
            XmlDocument doc = new XmlDocument();
            using (StreamReader streamReader = new StreamReader(copiedAddinFile.FullName, true))
            {
                doc.Load(streamReader);
            }

            foreach (XmlElement addinElement in doc.DocumentElement.ChildNodes)
            {
                //if this element is an addin attempt to make the assembly path a full path
             if (addinElement.LocalName != "AddIn")
                {
                    continue; 
                }
                var assemblies = addinElement.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName == "Assembly");
                var paths = assemblies.Select(x => x.InnerText.Replace("\"", ""));
                var fullpaths = paths.Select(x =>
                {
                    if (!System.IO.Path.IsPathRooted(x))
                    {
                        x = Path.Combine(Path.GetDirectoryName(originalAddin.FullName), x);
                    }
                    if (!File.Exists(x))
                    {
                        throw new System.IO.FileNotFoundException(addinElement.LocalName +
                            "contained an assembly that did not exist, at: " + x);
                    }
                    return x;
                });

                assemblies.ToList().Zip(fullpaths, (asm, path) => asm.InnerText = path).ToList();
            }
            doc.Save(copiedAddinFile.FullName);
        }
    }
}
