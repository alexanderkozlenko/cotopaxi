# cotopaxi deploy

<p />

## Name

<p />

`cotopaxi deploy` - Deploys the database packages to the Azure Cosmos DB account.

<p />

## Synopsis

<p />

```txt
cotopaxi deploy <package>
  [--endpoint <endpoint> --key <key>|--connection-string <connection-string>]
  [--profile <profile>]
  [--dry-run]

cotopaxi deploy -h|--help
```

<p />

## Description

<p />

The command uses a connection to the Azure Cosmos DB account to get partition keys configuration for regular and "dry run" modes.

<p />

The operations on the same document defined by a unique identifier and partition key values are executed in the following order:

<p />

1. Delete a document if it exists
2. Create a document if it does not exist
3. Upsert a document
4. Patch a document if it exists

<p />

## Arguments

<p />

- `package`  
The path to the database package or packages to deploy to the Azure Cosmos DB account (globbing patterns are supported).

<p />

## Options

<p />

- `--endpoint <endpoint>`  
The address of the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_ENDPOINT` environment variable. Must be used with `--key` option or `AZURE_COSMOS_KEY` environment variable.

<p />

- `--key <key>`  
The account key or resource token for the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_KEY` environment variable. Must be used with `--endpoint` option or `AZURE_COSMOS_ENDPOINT` environment variable.

<p />

- `--connection-string <connection-string>`  
The connection string for the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_CONNECTION_STRING` environment variable.

<p />

- `--profile`  
The path to the deployment profile or profiles that specify documents eligible for updates, based on schema [`profile.schema.json`](https://alexanderkozlenko.github.io/cotopaxi/schemas/profile.schema.json) (globbing patterns are supported).

<p />

- `--dry-run`  
Show which operations would be executed instead of actually executing them.

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

#### Deploying with Azure Pipelines:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi deploy
    $(System.ArtifactsDirectory)/**/*.cdbpkg
    --endpoint $(AZURE_COSMOS_ENDPOINT)
    --key $(AZURE_COSMOS_KEY)
```

<p />

```txt
/home/vsts/work/r1/a/adventureworks-v1.0.0.cdbpkg >>> https://adventureworks.documents.azure.com:443
upsert adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 ["bikes"]: HTTP 200 (6.67 RU)
upsert adventureworks\products\e1894e24-550d-4fe3-9784-47d614600baa ["bikes"]: HTTP 200 (6.67 RU)
```

<p />

#### Deploying with Azure Pipelines using a deployment profile:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi deploy
    $(System.ArtifactsDirectory)/**/*.cdbpkg
    --endpoint $(AZURE_COSMOS_ENDPOINT)
    --key $(AZURE_COSMOS_KEY)
    --profile $(System.ArtifactsDirectory)/**/*.profile.json
```

<p />

```txt
/home/vsts/work/r1/a/adventureworks-v1.0.0.cdbpkg >>> https://adventureworks.documents.azure.com:443
upsert adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 ["bikes"]: HTTP 200 (6.67 RU)
```

<p />

The corresponding deployment profile `adventureworks-v1.0.0.profile.json`:

<p />

```json
[
  {
    "databaseName": "adventureworks",
    "containerName": "products",
    "documentId": "3202cb6f-42af-4fe6-a3c5-d61927721e75",
    "documentPartitionKey": [
      "bikes"
    ]
  }
]
```

<p />

#### Deploying with Azure Pipelines in dry run mode using a read-only account key:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi deploy
    $(System.ArtifactsDirectory)/**/*.cdbpkg
    --endpoint $(AZURE_COSMOS_ENDPOINT)
    --key $(AZURE_COSMOS_KEY_READ_ONLY)
    --dry-run
```

<p />

```txt
[dry-run] /home/vsts/work/r1/a/adventureworks-v1.0.0.cdbpkg >>> https://adventureworks.documents.azure.com:443
[dry-run] upsert adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 ["bikes"]: HTTP ??? (?.?? RU)
[dry-run] upsert adventureworks\products\e1894e24-550d-4fe3-9784-47d614600baa ["bikes"]: HTTP ??? (?.?? RU)
```

<p />

## References

<p />

- [Microsoft - Azure Cosmos DB service quotas](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits)
