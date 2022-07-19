using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SituSystems.SituTest.Services.Contract
{
    public interface ITempIfcFileService
    {
        Task HandleFileInError(string azureFolderPath, string fileName, string homeName, List<string> errorMessages);
        string ContainerName();
        Task DeleteBlob(string folderPath);
        Task<TempIfcFileInfo> GetFileInfoFromBlobUrl(string azureFolderPath, string blobUrl, string builder, string homeName, string fileName, IfcType fileType);
    }
}
