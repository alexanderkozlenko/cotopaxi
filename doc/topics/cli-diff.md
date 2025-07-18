# cotopaxi diff

<p />

## Name

<p />

`cotopaxi diff` - Shows differences between database packages.

<p />

## Synopsis

<p />

```txt
cotopaxi diff <package1> <package2>
  [--endpoint <endpoint> --key <key>|--connection-string <connection-string>]
  [--profile <profile>]
  [--exit-code]

cotopaxi diff -h|--help
```

<p />

## Description

<p />

The command uses a connection to the Azure Cosmos DB account to get partition keys configuration.

<p />

## Arguments

<p />

- `package1`  
The path to the database package to compare.

<p />

- `package2`  
The path to the database package to compare with.

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
The path to the deployment profile to generate based on new and modified documents, which can be used with `deploy` command. The option automatically discards `--exit-code` instruction.

<p />

- `--exit-code`  
Instruct the program to exit with `1` if there were differences and `0` otherwise.

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

#### Showing changes in a new version:

<p />

```txt
dotnet tool run cotopaxi diff adventureworks-v1.0.1.cdbpkg adventureworks-v1.0.0.cdbpkg
```

<p />

```txt
+++ upsert /adventureworks/products/7f1b7c5a-c339-41e3-bc00-bc753b1d66bc:["bikes"] (+4)
*** upsert /adventureworks/products/3202cb6f-42af-4fe6-a3c5-d61927721e75:["bikes"] (*1)
```

<p />

#### Showing changes in a new version and generating a deployment profile based on changes:

<p />

```txt
dotnet tool run cotopaxi diff adventureworks-v1.0.1.cdbpkg adventureworks-v1.0.0.cdbpkg --profile adventureworks-v1.0.1.profile.json
```

<p />

```txt
+++ upsert /adventureworks/products/7f1b7c5a-c339-41e3-bc00-bc753b1d66bc:["bikes"] (+4)
*** upsert /adventureworks/products/3202cb6f-42af-4fe6-a3c5-d61927721e75:["bikes"] (*1)
```
