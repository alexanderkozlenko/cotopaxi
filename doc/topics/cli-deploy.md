# cotopaxi deploy

<p />

## Name

<p />

`cotopaxi deploy` - Deploys the package or packages to the Azure Cosmos DB account.

<p />

## Synopsis

<p />

```txt
cotopaxi deploy <package>
  [--endpoint <endpoint> --key <key>|--connection-string <connection-string>]
  [--dry-run]

cotopaxi deploy -h|--help
```

<p />

## Description

<p />

The command uses a connection to the Azure Cosmos DB account to get partition keys configuration for regular and "dry run" modes.

<p />

## Arguments

<p />

- `package`  
The path to a package or packages to deploy to the Azure Cosmos DB account (globbing patterns are supported).

<p />

## Options

<p />

- `--endpoint <endpoint>`  
The address of the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_ENDPOINT` environment variable. Must be used with `--key` option or `AZURE_COSMOS_KEY` environment variable.

<p />

- `--key <key>`  
The account key or resource token for the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_KEY` environment variable. Must be used with `--endpoint` option.

<p />

- `--connection-string <connection-string>`  
The connection string for the Azure Cosmos DB account. Can be specified with `AZURE_COSMOS_CONNECTION_STRING` environment variable.

<p />

- `--dry-run`  
Show which operations would be executed instead of actually executing them.

<p />

- `-h|--help`  
Prints out a description of how to use the command.

<p />

## Examples

<p />

Deploying a package locally:

<p />

```txt
cotopaxi deploy example.cdbpkg --endpoint https://example.documents.azure.com:443 --key $key$
```

<p />

Deploying a package locally using `AZURE_COSMOS_KEY` environment variable:

<p />

```txt
cotopaxi deploy example.cdbpkg --endpoint https://example.documents.azure.com:443
```

<p />

Deploying packages with an Azure DevOps release using `AZURE_COSMOS_ENDPOINT` and `AZURE_COSMOS_KEY` environment variables:

<p />

```txt
cotopaxi deploy $(System.ArtifactsDirectory)/**/*.cdbpkg
```

<p />

Showing operations for deployment with an Azure DevOps release using `AZURE_COSMOS_ENDPOINT` and `AZURE_COSMOS_KEY` environment variables:

<p />

```txt
cotopaxi deploy $(System.ArtifactsDirectory)/**/*.cdbpkg --dry-run
```
