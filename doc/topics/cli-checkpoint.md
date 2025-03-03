# cotopaxi checkpoint

<p />

## Name

<p />

`cotopaxi checkpoint` - Creates a package that reverts operations from the source package or packages.

<p />

## Synopsis

<p />

```txt
cotopaxi checkpoint <source-package> <rollback-package>
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
| `patch` | `patch` | The document does exist in the container |

<p />

## Arguments

<p />

- `source-package`  
The path to a package or packages for deployment deploy to the Azure Cosmos DB account (globbing patterns are supported).

<p />

- `rollback-package`  
The path to a package that reverts operations from the source package or packages.

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
Prints out a description of how to use the command.

<p />

## Examples

<p />

Create a rollback package with an Azure DevOps release using `AZURE_COSMOS_ENDPOINT` and `AZURE_COSMOS_KEY` environment variables:

<p />

```txt
cotopaxi checkpoint $(System.ArtifactsDirectory)/**/*.cdbpkg $(System.ArtifactsDirectory)/example.crbpkg
```
