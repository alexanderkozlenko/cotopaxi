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
    private readonly CancellationTokenSource _cancellationTokenSource;

    private PackageModel(Package package, CdmCorpusDefinition corpusDef, CdmManifestDefinition manifestDef, CancellationTokenSource cancellationTokenSource)
    {
        _package = package;
        _corpusDef = corpusDef;
        _manifestDef = manifestDef;
        _cancellationTokenSource = cancellationTokenSource;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Dispose();
    }

    public Uri CreatePartition(PackagePartition partition)
    {
        ArgumentNullException.ThrowIfNull(partition);

        var partitionPath = $"/cosmosdb.document/{partition.PartitionKey:D}.json";
        var partitionUri = new Uri(partitionPath, UriKind.Relative);
        var partitionDec = _manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");
        var partitionDef = _corpusDef.MakeObject<CdmDataPartitionDefinition>(CdmObjectType.DataPartitionDef);
        var partitionOperationName = PackageOperation.Format(partition.OperationType);

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
            var partitionOperationType = PackageOperation.Parse(partitionOperationName);

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

        var corpusSaveOptions = new CopyOptions
        {
            SaveConfigFile = false,
        };

        await _manifestDef.SaveAsAsync(_manifestDef.Name, saveReferenced: true, corpusSaveOptions).ConfigureAwait(false);

        var manifestPath = _corpusDef.Storage.CorpusPathToAdapterPath(_manifestDef.AtCorpusPath);

        _package.CreateRelationship(new(manifestPath, UriKind.Relative), TargetMode.Internal, s_manifestRelType);
    }

    public static async Task<PackageModel> OpenAsync(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(package);

        var cancellationTokenSource = new CancellationTokenSource();
        var manifestRel = package.FileOpenAccess.HasFlag(FileAccess.Read) ? package.GetRelationshipsByType(s_manifestRelType).SingleOrDefault() : null;

        if (manifestRel is null)
        {
            var corpusDef = CreateCorpusDef(package, compressionOption, cancellationTokenSource);
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

            var rootFolderDef = corpusDef.Storage.FetchRootFolder(PackageAdapter.SchemeName);

            rootFolderDef.Documents.Add(entitiesDef);
            rootFolderDef.Documents.Add(manifestDef);

            var partitionDec = manifestDef.Entities.Add(partitionDef);

            partitionDec.EntityPath = corpusDef.Storage.AdapterPathToCorpusPath(partitionDec.EntityPath);

            return new(package, corpusDef, manifestDef, cancellationTokenSource);
        }
        else
        {
            cancellationToken.ThrowIfCancellationRequested();

            var corpusDef = CreateCorpusDef(package, compressionOption, cancellationTokenSource);
            var manifestPath = corpusDef.Storage.AdapterPathToCorpusPath(manifestRel.TargetUri.OriginalString);
            var manifestDef = await corpusDef.FetchObjectAsync<CdmManifestDefinition>(manifestPath).ConfigureAwait(false);

            return new(package, corpusDef, manifestDef, cancellationTokenSource);
        }
    }

    private static CdmCorpusDefinition CreateCorpusDef(Package package, CompressionOption compressionOption, CancellationTokenSource cancellationTokenSource)
    {
        var corpusDef = new CdmCorpusDefinition();

        var corpusEventCallback = new EventCallback
        {
            Invoke = HandleCorpusEvent,
        };

        corpusDef.SetEventCallback(corpusEventCallback, CdmStatusLevel.Error);

        var adapter = new PackageAdapter(package, compressionOption, cancellationTokenSource);

        corpusDef.Storage.Unmount("local");
        corpusDef.Storage.Mount(PackageAdapter.SchemeName, adapter);
        corpusDef.Storage.DefaultNamespace = PackageAdapter.SchemeName;

        return corpusDef;

        void HandleCorpusEvent(CdmStatusLevel level, string message)
        {
            if (level == CdmStatusLevel.Error)
            {
                try
                {
                    cancellationTokenSource.Cancel();
                }
                catch (AggregateException ex)
                {
                    throw new InvalidOperationException(message, ex);
                }

                throw new InvalidOperationException(message);
            }
        }
    }
}
