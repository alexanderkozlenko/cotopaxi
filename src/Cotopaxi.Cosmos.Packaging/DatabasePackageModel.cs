// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using System.Diagnostics;
using System.IO.Packaging;
using Microsoft.CommonDataModel.ObjectModel.Cdm;
using Microsoft.CommonDataModel.ObjectModel.Enums;
using Microsoft.CommonDataModel.ObjectModel.Utilities;

namespace Cotopaxi.Cosmos.Packaging;

internal sealed class DatabasePackageModel : IDisposable
{
    private const string s_manifestSchemaUrl = "http://microsoft.com/cdm/schema.manifest.cdm.json";

    private readonly Package _package;
    private readonly CdmCorpusDefinition _corpusDef;
    private readonly CdmManifestDefinition _manifestDef;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private DatabasePackageModel(Package package, CdmCorpusDefinition corpusDef, CdmManifestDefinition manifestDef, CancellationTokenSource cancellationTokenSource)
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

    public static async Task<DatabasePackageModel> OpenAsync(Package package, CompressionOption compressionOption, CancellationToken cancellationToken)
    {
        Debug.Assert(package is not null);

        var cancellationTokenSource = new CancellationTokenSource();
        var corpusDef = CreateCorpusDef(package, compressionOption, cancellationTokenSource);

        if (package.FileOpenAccess.HasFlag(FileAccess.Read))
        {
            var manifestRel = package.GetRelationshipsByType(s_manifestSchemaUrl).SingleOrDefault();

            if (manifestRel is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var manifestUri = manifestRel.TargetUri;
                var manifestCorpusPath = corpusDef.Storage.AdapterPathToCorpusPath(manifestUri.OriginalString);
                var manifestDef = await corpusDef.FetchObjectAsync<CdmManifestDefinition>(manifestCorpusPath).ConfigureAwait(false);

                return new(package, corpusDef, manifestDef, cancellationTokenSource);
            }
        }

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

            var rootFolderDef = corpusDef.Storage.FetchRootFolder(DatabasePackageAdapter.SchemeName);

            rootFolderDef.Documents.Add(entitiesDef);
            rootFolderDef.Documents.Add(manifestDef);

            var partitionDec = manifestDef.Entities.Add(partitionDef);

            partitionDec.EntityPath = corpusDef.Storage.AdapterPathToCorpusPath(partitionDec.EntityPath);

            return new(package, corpusDef, manifestDef, cancellationTokenSource);
        }
    }

    public async ValueTask CloseAsync()
    {
        var context = _corpusDef.Ctx;

        if (_package.FileOpenAccess.HasFlag(FileAccess.ReadWrite) && context.FeatureFlags.ContainsKey(DatabasePackageAdapter.SchemeName))
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var persistenceOptions = new CopyOptions
            {
                SaveConfigFile = false,
            };

            await _manifestDef.SaveAsAsync(_manifestDef.Name, saveReferenced: true, persistenceOptions).ConfigureAwait(false);

            var manifestRel = _package.GetRelationshipsByType(s_manifestSchemaUrl).SingleOrDefault();

            if (manifestRel is null)
            {
                var manifestPath = _corpusDef.Storage.CorpusPathToAdapterPath(_manifestDef.AtCorpusPath);
                var manifestUri = new Uri(manifestPath, UriKind.Relative);

                _package.CreateRelationship(manifestUri, TargetMode.Internal, s_manifestSchemaUrl);
            }

            context.FeatureFlags.Remove(DatabasePackageAdapter.SchemeName);
        }
    }

    public DatabasePackageEntry CreatePackageEntry(Guid key, string databaseName, string containerName, DatabaseOperationType operationType)
    {
        Debug.Assert(databaseName is not null);
        Debug.Assert(containerName is not null);

        var partitionUri = new Uri($"/cosmosdb.document/{key:D}.json", UriKind.Relative);
        var partitionCorpusPath = _corpusDef.Storage.CreateAbsoluteCorpusPath(partitionUri.OriginalString);
        var partitionDec = _manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");

        Debug.Assert(!partitionDec.DataPartitions.Any(x => x.Location == partitionCorpusPath));

        var partitionDef = _corpusDef.MakeObject<CdmDataPartitionDefinition>(CdmObjectType.DataPartitionDef);
        var operationName = DatabaseOperation.Format(operationType);

        partitionDef.Location = partitionCorpusPath;
        partitionDef.Arguments.Add("database", [databaseName]);
        partitionDef.Arguments.Add("container", [containerName]);
        partitionDef.Arguments.Add("operation", [operationName]);

        partitionDec.DataPartitions.Add(partitionDef);

        _corpusDef.Ctx.FeatureFlags[DatabasePackageAdapter.SchemeName] = true;

        return new(partitionUri, databaseName, containerName, operationType);
    }

    public DatabasePackageEntry[] GetPackageEntries()
    {
        var partitionDec = _manifestDef.Entities.Single(static x => x.EntityName == "cosmosdb.document");
        var packageEntries = new DatabasePackageEntry[partitionDec.DataPartitions.Count];

        for (var i = 0; i < packageEntries.Length; i++)
        {
            var partitionDef = partitionDec.DataPartitions[i];
            var partitionPath = _corpusDef.Storage.CorpusPathToAdapterPath(partitionDef.Location);
            var partitionUri = new Uri(partitionPath, UriKind.Relative);
            var databaseName = partitionDef.Arguments["database"].Single();
            var containerName = partitionDef.Arguments["container"].Single();
            var operationName = partitionDef.Arguments["operation"].Single();
            var operationType = DatabaseOperation.Parse(operationName);

            packageEntries[i] = new(partitionUri, databaseName, containerName, operationType);
        }

        return packageEntries;
    }

    private static CdmCorpusDefinition CreateCorpusDef(Package package, CompressionOption compressionOption, CancellationTokenSource cancellationTokenSource)
    {
        var corpusDef = new CdmCorpusDefinition();

        var corpusEventCallback = new EventCallback
        {
            Invoke = HandleCorpusEvent,
        };

        corpusDef.SetEventCallback(corpusEventCallback, CdmStatusLevel.Error);

        var storageAdapter = new DatabasePackageAdapter(package, compressionOption, cancellationTokenSource);

        corpusDef.Storage.Unmount("local");
        corpusDef.Storage.Mount(DatabasePackageAdapter.SchemeName, storageAdapter);
        corpusDef.Storage.DefaultNamespace = DatabasePackageAdapter.SchemeName;

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
