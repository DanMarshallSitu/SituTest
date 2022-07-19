using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using SituSystems.Core.FileStorage;
using SituSystems.SituTest.Services.Contract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace SituSystems.SituTest.Services
{
    public class TempIfcFileService : ITempIfcFileService
    {
        private readonly IFileStorage _fileStorage;
        private readonly TempIfcFileStorageSettings _tempIfcFileStorageSettings;

        public TempIfcFileService(IFileStorage fileStorage,
            IOptions<TempIfcFileStorageSettings> tempIfcFileStorageSettings)
        {
            _fileStorage = fileStorage;
            _tempIfcFileStorageSettings = tempIfcFileStorageSettings.Value;
        }

        public async Task HandleFileInError(string azureFolderPath, string fileName, string homeName, List<string> errorMessages)
        {
            var errorDetail = new TempIfcFileError()
            {
                Id = Guid.NewGuid(),
                ErrorDate = DateTime.Now,
                FileName = fileName,
                Home = homeName,
                ErrorMessages = errorMessages
            };

            Log.Error("{Service}.{Method}: {@Error}", nameof(TempIfcFileService), nameof(HandleFileInError), errorDetail);

            var jsonFileName = azureFolderPath.Replace(".ifc", ".json");

            var json = JsonConvert.SerializeObject(errorDetail);

            using var stream = new MemoryStream(Encoding.Default.GetBytes(json));
            await _fileStorage.SaveFileAsync(jsonFileName, stream, _tempIfcFileStorageSettings.AzureStorageCredentials.ContainerName);
        }

        public string ContainerName()
        {
            return _tempIfcFileStorageSettings.AzureStorageCredentials.ContainerName;
        }

        public async Task DeleteBlob(string folderPath)
        {
            await _fileStorage.DeleteAsync(folderPath, _tempIfcFileStorageSettings.AzureStorageCredentials.ContainerName);
        }

        public async Task<TempIfcFileInfo> GetFileInfoFromBlobUrl(string azureFolderPath, string blobUrl, string builder, string homeName, string fileName, IfcType fileType)
        {
            return await TempIfcFileInfo.FromBlobUrl(_fileStorage, azureFolderPath, blobUrl, builder, homeName, fileName, fileType);
        }
    }
}
