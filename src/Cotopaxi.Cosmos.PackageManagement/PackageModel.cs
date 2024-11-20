// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;
using Microsoft.CommonDataModel.ObjectModel.Cdm;
using Microsoft.CommonDataModel.ObjectModel.Enums;
using Microsoft.CommonDataModel.ObjectModel.Utilities;

namespace Cotopaxi.Cosmos.PackageManagement;

public sealed class PackageModel : IDisposable
{
    private const string s_manifestRelType = "http://microsoft.com/cdm/schema.manifest.cdm.json";

    private readonly Package _package;
    private readonly CdmCorpusDefinition _corpusDef;
    private readonly CdmManifestDefinition _manifestDef;

    private PackageModel(Package package, CdmCorpusDefinition corpusDef, CdmManifestDefinition manifestDef)
    {
        _package = package;
        _corpusDef = corpusDef;
        _manifestDef = manifestDef;
    }

    public void Dispose()
    {
        _corpusDef.Storage.Unmount(PackageAdapter.Scheme);
    }

    public async Task SaveAsync()
    {
        if (_package.FileOpenAccess != FileAccess.Read)
        {
            var corpusSaveOptions = new CopyOptions
            {
                SaveConfigFile = false,
            };

            await _manifestDef.SaveAsAsync(_manifestDef.Name, saveReferenced: true, corpusSaveOptions).ConfigureAwait(false);

            var manifestPath = _corpusDef.Storage.CorpusPathToAdapterPath(_manifestDef.AtCorpusPath);

            _package.CreateRelationship(new(manifestPath, UriKind.Relative), TargetMode.Internal, s_manifestRelType);
        }
    }

    public Uri CreatePartition(string partitionName, string databaseName, string containerName, string operationName)
    {
        Debug.Assert(partitionName is { Length: > 0 });
        Debug.Assert(databaseName is { Length: > 0 });
        Debug.Assert(containerName is { Length: > 0 });
        Debug.Assert(operationName is { Length: > 0 });

        var partitionPath = $"/cosmosdb.document/{partitionName}.json";
        var partitionDec = _manifestDef.Entities.First(static x => x.EntityName == "cosmosdb.document");
        var partitionDef = _corpusDef.MakeObject<CdmDataPartitionDefinition>(CdmObjectType.DataPartitionDef);

        partitionDef.Location = _corpusDef.Storage.CreateAbsoluteCorpusPath(partitionPath);
        partitionDef.Arguments.Add("database", [databaseName]);
        partitionDef.Arguments.Add("container", [containerName]);
        partitionDef.Arguments.Add("operation", [operationName]);

        partitionDec.DataPartitions.Add(partitionDef);

        return new(partitionPath, UriKind.Relative);
    }

    public PackagePartition[] GetPartitions()
    {
        var partitions = new Dictionary<string, PackagePartition>(StringComparer.Ordinal);
        var partitionDec = _manifestDef.Entities.SingleOrDefault(static x => x.EntityName == "cosmosdb.document");

        if (partitionDec is not null)
        {
            partitions.EnsureCapacity(partitionDec.DataPartitions.Count);

            foreach (var partitionDef in partitionDec.DataPartitions)
            {
                var partitionPath = _corpusDef.Storage.CorpusPathToAdapterPath(partitionDef.Location);
                var partitionName = Path.GetFileNameWithoutExtension(partitionPath);
                var partitionDatabaseName = partitionDef.Arguments["database"].Single();
                var partitionContainerName = partitionDef.Arguments["container"].Single();
                var partitionOperationName = partitionDef.Arguments["operation"].Single();

                var partition = new PackagePartition(
                    new(partitionPath, UriKind.Relative),
                    partitionName,
                    partitionDatabaseName,
                    partitionContainerName,
                    partitionOperationName);

                partitions.Add(partitionPath, partition);
            }
        }

        return [.. partitions.Values];
    }

    public static async Task<PackageModel> OpenAsync(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        Debug.Assert(package is not null);

        var corpusDef = CreateCorpus(package, compressionOption, cancellationToken);

        if (package.FileOpenAccess != FileAccess.Read)
        {
            var manifestDef = corpusDef.MakeObject<CdmManifestDefinition>(CdmObjectType.ManifestDef, "cosmosdb");
            var entitiesDef = corpusDef.MakeObject<CdmDocumentDefinition>(CdmObjectType.DocumentDef, "cosmosdb.entities.cdm.json");
            var partitionDef = corpusDef.MakeObject<CdmEntityDefinition>(CdmObjectType.EntityDef, "cosmosdb.document");
            var partitionDatabaseDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "database");
            var partitionContainerDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "container");
            var partitionOperationDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "operation");

            partitionDatabaseDef.DataFormat = CdmDataFormat.String;
            partitionContainerDef.DataFormat = CdmDataFormat.String;
            partitionOperationDef.DataFormat = CdmDataFormat.String;
            partitionDef.Attributes.Add(partitionDatabaseDef);
            partitionDef.Attributes.Add(partitionContainerDef);
            partitionDef.Attributes.Add(partitionOperationDef);
            entitiesDef.Imports.Add("cdm:/foundations.cdm.json");
            entitiesDef.Definitions.Add(partitionDef);

            var rootFolderDef = corpusDef.Storage.FetchRootFolder(PackageAdapter.Scheme);

            rootFolderDef.Documents.Add(entitiesDef);
            rootFolderDef.Documents.Add(manifestDef);

            var partitionDec = manifestDef.Entities.Add(partitionDef);

            partitionDec.EntityPath = corpusDef.Storage.AdapterPathToCorpusPath(partitionDec.EntityPath);

            return new(package, corpusDef, manifestDef);
        }
        else
        {
            var manifestRel = package.GetRelationshipsByType(s_manifestRelType).Single();
            var manifestPath = corpusDef.Storage.AdapterPathToCorpusPath(manifestRel.TargetUri.OriginalString);
            var manifestDef = await corpusDef.FetchObjectAsync<CdmManifestDefinition>(manifestPath).ConfigureAwait(false);

            return new(package, corpusDef, manifestDef);
        }
    }

    private static CdmCorpusDefinition CreateCorpus(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        var corpusDef = new CdmCorpusDefinition();

        var corpusEventCallback = new EventCallback
        {
            Invoke = static (_, message) => throw new InvalidOperationException(message),
        };

        corpusDef.SetEventCallback(corpusEventCallback, CdmStatusLevel.Error);

        var adapter = new PackageAdapter(package, compressionOption, cancellationToken);

        corpusDef.Storage.Unmount("local");
        corpusDef.Storage.Mount(PackageAdapter.Scheme, adapter);
        corpusDef.Storage.DefaultNamespace = PackageAdapter.Scheme;

        return corpusDef;
    }
}
