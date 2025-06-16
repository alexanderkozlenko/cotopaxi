# cotopaxi snapshot

<p />

## Name

<p />

`cotopaxi snapshot` - Creates a database package from the Azure Cosmos DB account.

<p />

## Synopsis

<p />

```txt
cotopaxi snapshot <profile> <package>
  [--endpoint <endpoint> --key <key>|--connection-string <connection-string>]

cotopaxi snapshot -h|--help
```

<p />

## Description

<p />

The snapshots of existing documents are added to the resulting database package as `upsert` operations.

<p />

## Arguments

<p />

- `profile`  
The path to the deployment profile or profiles that specify which documents to take snapshots of (globbing patterns are supported).

<p />

- `package`  
The path to a resulting database package with import operations.

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

#### Creating a snapshot package with Azure Pipelines using a read-only account key:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi snapshot
    $(System.ArtifactsDirectory)/**/*.profile.json
    $(System.ArtifactsDirectory)/adventureworks.sn.cdbpkg
    --endpoint $(AZURE_COSMOS_ENDPOINT)
    --key $(AZURE_COSMOS_KEY_READ_ONLY)
```

<p />

```txt
https://adventureworks.documents.azure.com:443 >>> /home/vsts/work/r1/a/adventureworks.sn.cdbpkg
read /adventureworks/products/3202cb6f-42af-4fe6-a3c5-d61927721e75:["bikes"]: HTTP 200
read /adventureworks/products/7f1b7c5a-c339-41e3-bc00-bc753b1d66bc:["bikes"]: HTTP 404
+++ upsert /adventureworks/products/3202cb6f-42af-4fe6-a3c5-d61927721e75:["bikes"] (4)
```

<p />

The corresponding deployment profile `adventureworks.profile.json`:

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
  },
  {
    "databaseName": "adventureworks",
    "containerName": "products",
    "documentId": "7f1b7c5a-c339-41e3-bc00-bc753b1d66bc",
    "documentPartitionKey": [
      "bikes"
    ]
  }
]
```
