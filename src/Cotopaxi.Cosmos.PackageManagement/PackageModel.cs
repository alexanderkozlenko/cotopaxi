// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Collections.Frozen;
using System.Diagnostics;
using System.IO.Packaging;
using Microsoft.CommonDataModel.ObjectModel.Cdm;
using Microsoft.CommonDataModel.ObjectModel.Enums;
using Microsoft.CommonDataModel.ObjectModel.Utilities;

namespace Cotopaxi.Cosmos.PackageManagement;

internal sealed class PackageModel : IAsyncDisposable
{
    private const string s_manifestRelType = "http://microsoft.com/cdm/schema.manifest.cdm.json";

    private readonly Package _package;
    private readonly CdmCorpusDefinition _corpusDef;
    private readonly CdmManifestDefinition _manifestDef;
    private readonly CdmEntityDeclarationDefinition _entryDec;

    private PackageModel(Package package, CdmCorpusDefinition corpusDef, CdmManifestDefinition manifestDef, CdmEntityDeclarationDefinition entryDec)
    {
        _package = package;
        _corpusDef = corpusDef;
        _manifestDef = manifestDef;
        _entryDec = entryDec;
    }

    public async ValueTask DisposeAsync()
    {
        if (_package.FileOpenAccess != FileAccess.Read)
        {
            var corpusSaveOptions = new CopyOptions
            {
                SaveConfigFile = false,
            };

            await _manifestDef.SaveAsAsync(_manifestDef.Name, saveReferenced: true, corpusSaveOptions).ConfigureAwait(false);

            var manifestPath = _corpusDef.Storage.CorpusPathToAdapterPath(_manifestDef.AtCorpusPath);
            var manifestUri = new Uri(manifestPath, UriKind.Relative);

            _package.CreateRelationship(manifestUri, TargetMode.Internal, s_manifestRelType);
        }

        _corpusDef.Storage.Unmount(PackageModelAdapter.Scheme);
    }

    public string CreateEntry(Guid entryKey, PackageEntry entry)
    {
        Debug.Assert(entry is not null);

        var entryPath = $"/cosmosdb.document/{entryKey}.json";
        var partitionDef = _corpusDef.MakeObject<CdmDataPartitionDefinition>(CdmObjectType.DataPartitionDef);

        partitionDef.Location = _corpusDef.Storage.CreateAbsoluteCorpusPath(entryPath);
        partitionDef.Arguments.Add("database", [entry.DatabaseName]);
        partitionDef.Arguments.Add("container", [entry.ContainerName]);
        partitionDef.Arguments.Add("operation", [entry.OperationName.ToLowerInvariant()]);

        _entryDec.DataPartitions.Add(partitionDef);

        return entryPath;
    }

    public FrozenSet<PackageEntry> ExtractEntries()
    {
        var entries = new HashSet<PackageEntry>(_entryDec.DataPartitions.Count);

        foreach (var partitionDef in _entryDec.DataPartitions)
        {
            var entryPath = _corpusDef.Storage.CorpusPathToAdapterPath(partitionDef.Location);
            var entryUUID = Guid.Parse(Path.GetFileNameWithoutExtension(entryPath));
            var entryDatabaseName = partitionDef.Arguments["database"].Single();
            var entryContainerName = partitionDef.Arguments["container"].Single();
            var entryOperationName = partitionDef.Arguments["operation"].Single().ToUpperInvariant();
            var entry = new PackageEntry(entryUUID, entryDatabaseName, entryContainerName, entryOperationName, entryPath);

            entries.Add(entry);
        }

        return entries.ToFrozenSet();
    }

    public static async Task<PackageModel> OpenAsync(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        Debug.Assert(package is not null);

        var corpusDef = CreateCorpus(package, compressionOption, cancellationToken);

        if (package.FileOpenAccess != FileAccess.Read)
        {
            var manifestDef = corpusDef.MakeObject<CdmManifestDefinition>(CdmObjectType.ManifestDef, "cosmosdb");
            var entitiesDef = corpusDef.MakeObject<CdmDocumentDefinition>(CdmObjectType.DocumentDef, "cosmosdb.entities.cdm.json");
            var entryDef = corpusDef.MakeObject<CdmEntityDefinition>(CdmObjectType.EntityDef, "cosmosdb.document");
            var entryDatabaseDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "database");
            var entryContainerDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "container");
            var entryOperationDef = corpusDef.MakeObject<CdmTypeAttributeDefinition>(CdmObjectType.TypeAttributeDef, "operation");

            entryDatabaseDef.DataFormat = CdmDataFormat.String;
            entryContainerDef.DataFormat = CdmDataFormat.String;
            entryOperationDef.DataFormat = CdmDataFormat.String;
            entryDef.Attributes.Add(entryDatabaseDef);
            entryDef.Attributes.Add(entryContainerDef);
            entryDef.Attributes.Add(entryOperationDef);
            entitiesDef.Imports.Add("cdm:/foundations.cdm.json");
            entitiesDef.Definitions.Add(entryDef);

            var rootFolderDef = corpusDef.Storage.FetchRootFolder(PackageModelAdapter.Scheme);

            rootFolderDef.Documents.Add(entitiesDef);
            rootFolderDef.Documents.Add(manifestDef);

            var entryDec = manifestDef.Entities.Add(entryDef);

            entryDec.EntityPath = corpusDef.Storage.AdapterPathToCorpusPath(entryDec.EntityPath);

            return new(package, corpusDef, manifestDef, entryDec);
        }
        else
        {
            var manifestRel = package.GetRelationshipsByType(s_manifestRelType).Single();
            var manifestPath = corpusDef.Storage.AdapterPathToCorpusPath(manifestRel.TargetUri.OriginalString);
            var manifestDef = await corpusDef.FetchObjectAsync<CdmManifestDefinition>(manifestPath).ConfigureAwait(false);
            var entryDec = manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");

            return new(package, corpusDef, manifestDef, entryDec);
        }
    }

    private static CdmCorpusDefinition CreateCorpus(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        var corpusAdapter = new PackageModelAdapter(package, compressionOption, cancellationToken);
        var corpusDef = new CdmCorpusDefinition();

        var corpusEventCallback = new EventCallback
        {
            Invoke = static (_, message) => throw new InvalidOperationException(message),
        };

        corpusDef.SetEventCallback(corpusEventCallback, CdmStatusLevel.Error);
        corpusDef.Storage.Unmount("local");
        corpusDef.Storage.Mount(PackageModelAdapter.Scheme, corpusAdapter);
        corpusDef.Storage.DefaultNamespace = PackageModelAdapter.Scheme;

        return corpusDef;
    }
}
