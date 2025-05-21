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

#### Creating a rollback package with Azure Pipelines using a read-only account key:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi checkpoint
    $(System.ArtifactsDirectory)/**/*.cdbpkg
    $(System.ArtifactsDirectory)/adventureworks.crbpkg
    --endpoint $(AZURE_COSMOS_ENDPOINT)
    --key $(AZURE_COSMOS_KEY_READ_ONLY)
```

<p />

```txt
/home/vsts/work/r1/a/adventureworks-v1.0.1.cdbpkg + https://adventureworks.documents.azure.com:443 >>> /home/vsts/work/r1/a/adventureworks.crbpkg
read adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 ["bikes"]: HTTP 200 (1.00 RU)
read adventureworks\products\e1894e24-550d-4fe3-9784-47d614600baa ["bikes"]: HTTP 200 (1.00 RU)
read adventureworks\products\7f1b7c5a-c339-41e3-bc00-bc753b1d66bc ["bikes"]: HTTP 404 (1.00 RU)
+++ delete adventureworks\products\7f1b7c5a-c339-41e3-bc00-bc753b1d66bc ["bikes"] (4)
+++ upsert adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 ["bikes"] (4)
```

<p />

## References

<p />

- [Microsoft - Azure Cosmos DB service quotas](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits)
