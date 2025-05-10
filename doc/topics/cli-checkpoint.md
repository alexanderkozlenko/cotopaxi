# cotopaxi checkpoint

<p />

## Name

<p />

`cotopaxi checkpoint` - Creates a database package with rollback operations.

<p />

## Synopsis

<p />

```txt
cotopaxi checkpoint <package> <rollback-package>
  [--endpoint <endpoint> --key <key>|--connection-string <connection-string>]

cotopaxi checkpoint -h|--help
```

<p />

## Description

<p />

| Deploy Operation | Rollback Operation | Condition |
|:-:|:-:|:- |
| `delete` | N/A | The document does not exist in the container |
| `delete` | `upsert` | The document does exist in the container |
| `create` | `delete` | The document does not exist in the container |
| `create` | N/A | The document does exist in the container |
| `upsert` | `delete` | The document does not exist in the container |
| `upsert` | `upsert` | The document does exist in the container |
| `patch` | N/A | The document does not exist in the container |
| `patch` | `upsert` | The document does exist in the container |

<p />

## Arguments

<p />

- `package`  
The path to the database package or packages for deployment to the Azure Cosmos DB account (globbing patterns are supported).

<p />

- `rollback-package`  
The path to a resulting database package with rollback operations.

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

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

Creating a rollback package with Azure Pipelines using `AZURE_COSMOS_ENDPOINT` and `AZURE_COSMOS_KEY` environment variables:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi checkpoint
    $(System.ArtifactsDirectory)/**/*.cdbpkg
    $(System.ArtifactsDirectory)/adventureworks-rollback.cdbpkg
```

<p />

## References

<p />

- [Microsoft - Azure Cosmos DB service quotas](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits)
