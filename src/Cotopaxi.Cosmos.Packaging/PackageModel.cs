// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Collections.Frozen;
using System.IO.Packaging;
using Microsoft.CommonDataModel.ObjectModel.Cdm;
using Microsoft.CommonDataModel.ObjectModel.Enums;
using Microsoft.CommonDataModel.ObjectModel.Utilities;

namespace Cotopaxi.Cosmos.Packaging;

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

    public Uri CreatePartition(PackagePartition partition)
    {
        ArgumentNullException.ThrowIfNull(partition);

        var partitionPath = $"/cosmosdb.document/{partition.PartitionKey:D}.json";
        var partitionUri = new Uri(partitionPath, UriKind.Relative);
        var partitionDec = _manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");
        var partitionDef = _corpusDef.MakeObject<CdmDataPartitionDefinition>(CdmObjectType.DataPartitionDef);

        var partitionOperationName = partition.OperationType switch
        {
            PackageOperationType.Delete => "delete",
            PackageOperationType.Create => "create",
            PackageOperationType.Upsert => "upsert",
            PackageOperationType.Patch => "patch",
            _ => throw new InvalidOperationException(),
        };

        partitionDef.Location = _corpusDef.Storage.CreateAbsoluteCorpusPath(partitionPath);
        partitionDef.Arguments.Add("database", [partition.DatabaseName]);
        partitionDef.Arguments.Add("container", [partition.ContainerName]);
        partitionDef.Arguments.Add("operation", [partitionOperationName]);

        if (partitionDec.DataPartitions.Any(x => x.Location == partitionDef.Location))
        {
            throw new InvalidOperationException("A partition with the same key already exists");
        }

        partitionDec.DataPartitions.Add(partitionDef);

        return partitionUri;
    }

    public IReadOnlyDictionary<Uri, PackagePartition> GetPartitions()
    {
        var partitionDec = _manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");
        var partitions = new Dictionary<Uri, PackagePartition>(partitionDec.DataPartitions.Count);

        foreach (var partitionDef in partitionDec.DataPartitions)
        {
            var partitionPath = _corpusDef.Storage.CorpusPathToAdapterPath(partitionDef.Location);
            var partitionUri = new Uri(partitionPath, UriKind.Relative);
            var partitionKey = Guid.ParseExact(Path.GetFileNameWithoutExtension(partitionPath), "D");
            var partitionDatabaseName = partitionDef.Arguments["database"].Single();
            var partitionContainerName = partitionDef.Arguments["container"].Single();
            var partitionOperationName = partitionDef.Arguments["operation"].Single();

            var partitionOperationType = partitionOperationName?.ToLowerInvariant() switch
            {
                "delete" => PackageOperationType.Delete,
                "create" => PackageOperationType.Create,
                "upsert" => PackageOperationType.Upsert,
                "patch" => PackageOperationType.Patch,
                _ => throw new NotSupportedException(),
            };

            var partition = new PackagePartition(
                partitionKey,
                partitionDatabaseName,
                partitionContainerName,
                partitionOperationType);

            partitions.Add(partitionUri, partition);
        }

        return partitions.ToFrozenDictionary();
    }

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

    public static async Task<PackageModel> OpenAsync(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        cancellationToken.ThrowIfCancellationRequested();

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
