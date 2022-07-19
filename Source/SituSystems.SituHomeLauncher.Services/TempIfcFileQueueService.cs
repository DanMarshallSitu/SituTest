using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using SituSystems.ArtifactStore.Contracts.Models;
using SituSystems.ArtifactStore.Services;
using SituSystems.SituHomeLauncher.Services.Contract;
using SituSystems.KeyedArtifactStore.Contracts.Commands;
using SituSystems.KeyedArtifactStore.Contracts.Models;
using SituSystems.KeyedArtifactStore.Contracts.Queries;
using SituSystems.KeyedArtifactStore.Services;
using SituSystems.Warp.Artifacts;
using SituSystems.Warp.Artifacts.ArtifactKeys;
using Microsoft.Extensions.Options;

namespace SituSystems.SituHomeLauncher.Services
{
    public class TempIfcFileQueueService : ITempIfcFileQueueService
    {
        private readonly ITempIfcFileService _tempIfcFileService;
        private readonly IKeyedArtifactStoreService _keyedArtifactStoreService;
        private readonly IKeyedFileSystemService _keyedFileSystemService;
        private readonly WarpArtifactSettings _warpSettings;

        public TempIfcFileQueueService(ITempIfcFileService tempIfcFileService,
            IKeyedArtifactStoreService keyedArtifactStoreService,
            IKeyedFileSystemService keyedFileSystemService,
            IOptions<WarpArtifactSettings> warpSettings)
        {
            _tempIfcFileService = tempIfcFileService;
            _keyedArtifactStoreService = keyedArtifactStoreService;
            _keyedFileSystemService = keyedFileSystemService;
            _warpSettings = warpSettings.Value;
        }

        public async Task ProcessTempIfcFileAsync(TempIfcFileMessage tempIfcFileMessage)
        {
            var builder = tempIfcFileMessage.Builder;
            var blobUrl = tempIfcFileMessage.BlobUrl;
            var homeName = tempIfcFileMessage.HomeName;
            var fileName = tempIfcFileMessage.FileName;
            var fileType = tempIfcFileMessage.IfcType;

            try
            {                
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var azureFolderPath = GetFullPathToFile(builder, homeName, fileName, fileType);

                var fileInfo = await _tempIfcFileService.GetFileInfoFromBlobUrl(azureFolderPath, blobUrl, builder, homeName, fileName, fileType);

                var fingerPrint = FingerPrint.Calculate(fileInfo.FullPath);
                var key = CreateKey(fileInfo);
                var exists = ExistsInArtifactStore(key, fingerPrint);
                if (exists)
                {
                    stopwatch.Stop();
                    var errorMessages = new List<string>
                    {
                        $"{fileName} already exists!"
                    };
                    await _tempIfcFileService.HandleFileInError(azureFolderPath, fileName, homeName, errorMessages);
                }
                else
                {
                    var ifc4ToJsonPath = GetIfc4ToJsonPath();
                    var tempOutputPath = GetTempOutputPath();

                    var metaData = ProcessIfcFile(fileInfo, builder, ifc4ToJsonPath, tempOutputPath);

                    var inactive = metaData.Any(m => m.Key == "Error");

                    var addResponse = _keyedFileSystemService.AddArtifact(new AddKeyedArtifactFromFileSystemRequest()
                    {
                        ArtifactKey = key,
                        InActive = inactive,
                        FingerPrint = fingerPrint, // no need to calculate it again as we have it from above
                        MetaData = metaData,
                        FilePath = fileInfo.FullPath
                    });

                    if (!addResponse.Success)
                    {
                        Console.Write($"Add Failed: {fileInfo.FullPath}");
                        Log.Error("{Builder} {HomeName} {IfcFileName} failed with errors {Errors} in {TimeTaken}", builder, fileInfo.Home, fileInfo.IfcFileName, addResponse.ErrorMessages, stopwatch.Elapsed);
                        await _tempIfcFileService.HandleFileInError(azureFolderPath, fileName, homeName, addResponse.ErrorMessages);
                    }
                    else
                    {
                        Console.WriteLine($"Added {addResponse.CallerReferenceKey} version {addResponse.Version}");
                        Log.Information("{Builder} {HomeName} {IfcFileName} added as {ResultKey} version {Version} in {TimeTaken}", builder, fileInfo.Home, fileInfo.IfcFileName, addResponse.CallerReferenceKey, addResponse.Version, stopwatch.Elapsed);
                    }
                }

                await _tempIfcFileService.DeleteBlob(azureFolderPath);
            }
            catch (Exception e)
            {
                Log.Error(e, $"ProcessTempIfcFile: An error occurred processing {blobUrl}");
            }
        }

        private string GetTempOutputPath()
        {
            var outputPath = Path.Combine(_warpSettings.LocalCacheRootPath, "Temp");
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            return outputPath;
        }

        private string GetIfc4ToJsonPath()
        {
            var softwareKey = new SoftwareKey();
            softwareKey.Deserialize("software/ifc4tojson");

            var response = _keyedArtifactStoreService.SearchArtifacts(new SearchKeyedArtifactsRequest()
            {
                ArtifactKey = softwareKey,
                ExcludeDeleted = true,
                IncludeInactive = false,
                ReturnLatestOnly = true
            });

            var artifact = response.Artifacts.First();

            var get = _keyedFileSystemService.GetArtifactToLocalCache(new GetKeyedArtifactToLocalCacheRequest()
            {
                ArtifactKey = softwareKey,
                Version = artifact.Version,
                LocalCacheRootPath = _warpSettings.LocalCacheRootPath
            });

            if (!get.Success)
            {
                throw new Exception("Unable to retrieve latest ifc4tojson.exe");
            }

            var keyedArtifactVersionReference = new KeyedArtifactVersionReference()
            {
                artifactKey = softwareKey,
                Version = artifact.Version
            };

            return Path.Combine(_warpSettings.LocalCacheRootPath, keyedArtifactVersionReference.Path);
        }


        private bool ExistsInArtifactStore(ArtifactKey artifactKey, string fingerPrint)
        {
            var existing = _keyedArtifactStoreService.SearchArtifacts(new SearchKeyedArtifactsRequest()
            {
                ArtifactKey = artifactKey,
                FingerPrint = fingerPrint,
                IncludeInactive = true
            });
            return existing.Success && existing.Artifacts.Any();
        }

        public string ContainerName()
        {
            return _tempIfcFileService.ContainerName();
        }

        private string GetFullPathToFile(string builder, string homeName, string fileName, IfcType fileType)
        {
            var folderPath = Path.Combine(builder, homeName);
            folderPath = Path.Combine(folderPath, fileType == IfcType.Master ? "Master" : "Option");
            folderPath = Path.Combine(folderPath, fileName);

            return folderPath;
        }


        private ArtifactKey CreateKey(TempIfcFileInfo file)
        {
            return new IfcFileKey(file.Builder, file.Home, file.FileType == IfcType.Master, file.IfcName);
        }

        private List<MetaData> ProcessIfcFile(TempIfcFileInfo file, string builder, string ifc4tojsonpath, string tempOutputPath)
        {
            List<MetaData> results;
            try
            {
                var processor = new IfcToJsonRunner(file.FullPath, builder, ifc4tojsonpath, tempOutputPath);

                if (!processor.RunMetaDataOnly(out results, out string errorMsg))
                {
                    results = new List<MetaData>
                    {
                        new MetaData()
                        {
                            Key = "Error",
                            Value = errorMsg
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                results = new List<MetaData>
                {
                    new MetaData()
                    {
                        Key = "Error",
                        Value = "Unable to process IFC file"
                    }
                };
            }

            return results;
        }
    }
}
