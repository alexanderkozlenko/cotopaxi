# cotopaxi diff

<p />

## Name

<p />

`cotopaxi diff` - Shows changes between database packages.

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
Prints out a description of how to use the command.

<p />

## Examples

<p />

Show changes in a new version of database package using environment variables:

<p />

```txt
cotopaxi diff example-v2.cdbpkg example-v1.cdbpkg
```

<p />

Generate a deployment profile based on changes in a new version of database package using environment variables:

<p />

```txt
cotopaxi diff example-v2.cdbpkg example-v1.cdbpkg --profile example-v2.cdbdep
```
