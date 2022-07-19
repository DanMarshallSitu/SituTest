using SituSystems.Core.FileStorage;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SituSystems.SituTest.Services
{
    public class TempIfcFileInfo
    {
        internal string IfcFileName;
        internal string IfcName;
        internal string DesignOptionName;
        internal string Home;
        internal string Builder;
        internal IfcType FileType;

        internal string IfcLongName() { return IfcLongName(Home, IfcName); }
        internal string FullPath;

        public static string IfcLongName(string homeName, string ifcName)
        {
            return $"{homeName}_{ifcName}";
        }

        internal string WarpOutputPath(string outputFolder)
        {
            bool includeBuilder = !outputFolder.Contains(Builder);
            var outputPath = Path.Combine(outputFolder, includeBuilder ? Builder : "", "Homes", Home);
            return outputPath;
        }

        internal static async Task<TempIfcFileInfo> FromBlobUrl(IFileStorage fileStorage, string folderPath, string blobUrl, string builder, string home, string fileName, IfcType fileType)
        {
            var designOptionName = Path.GetFileNameWithoutExtension(fileName);
            var localFileName = await GetFileFromBlob(fileStorage, folderPath, fileName);

            var result = new TempIfcFileInfo()
            {
                Builder = builder,
                DesignOptionName = designOptionName,
                FileType = fileType,
                FullPath = localFileName,
                Home = home,
                IfcFileName = fileName,
                IfcName = Path.GetFileNameWithoutExtension(fileName),
            };

            if (fileType == IfcType.Master)
            {
                // designOptionName becomes the ifcName without the homeOption value in there.
                // So Mast1 Aintree 311 Delta -> delta
                var nameTokens = result.DesignOptionName.Split(' ').ToList();
                var homeOptionTokens = result.Home.Split(' ').ToList();
                homeOptionTokens.Add("Mast");

                foreach (var homeOptionToken in homeOptionTokens)
                {
                    for (var i = 0; i < nameTokens.Count; ++i)
                    {
                        if (nameTokens[i].Contains(homeOptionToken, StringComparison.CurrentCultureIgnoreCase))
                        {
                            nameTokens.RemoveAt(i--);
                        }
                    }
                }

                var designOptionNameJoined = string.Join(" ", nameTokens);
                result.DesignOptionName = string.IsNullOrWhiteSpace(designOptionNameJoined) ? "master" : designOptionNameJoined;
                result.IfcName = result.DesignOptionName?.ToLower();
            }
            return result;
        }

        private static async Task<string> GetFileFromBlob(IFileStorage fileStorage, string folderPath, string fileName)
        {

            var blobStream = await fileStorage.LoadAsync(folderPath);
            if (blobStream == null)
            {
                throw new Exception($"{fileName} could not be opened");
            }

            var localFileName = Path.Combine(Path.GetTempPath(), fileName);
            if (File.Exists(localFileName))
            {
                File.Delete(localFileName);
            }
            using (var fileStream = File.OpenWrite(localFileName))
            {
                if (fileStream == null)
                {
                    throw new Exception($"{fileName} could not be created");
                }

                blobStream.CopyTo(fileStream);
            }

            return localFileName;
        }
    }
}
