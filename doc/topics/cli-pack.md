# cotopaxi pack

<p />

## Name

<p />

`cotopaxi pack` - Packs the documents into an Azure Cosmos DB package.

<p />

## Synopsis

<p />

```txt
cotopaxi pack <project> <package> [--version <version>]

cotopaxi pack -h|--help
```

<p />

## Description

<p />

The package format supports the following deployment operations:

<p />

- `create`  
Given a unique identifier and partition key values, creates a document if it does not exist.
- `upsert`  
Given a unique identifier and partition key values, creates a document if it does not exist, or update if it exists.
- `patch`  
Given a unique identifier and partition key values, performs partial document update if it exists.
- `delete`  
Given a unique identifier and partition key values, deletes a document if it exists.

<p />

Partial document update performs Azure Cosmos DB `set` operation on specified root-level document properties.

<p />

The project format supports relative globbing patterns and the following variables in document paths:

<p />

| Name | Value |
|:- |:- |
| `Version` | The value specified with the `--version <version>` option |

<p />

A documents file must be a JSON array of objects, where each object has the `id` property and partition key properties specified.

<p />

## Arguments

<p />

- `project`  
The path to the project that specifies documents to pack.
- `package`  
The path to the package to create.

<p />

## Options

<p />

- `--version <version>`  
Sets the package version information.

<p />

- `-h|--help`  
Prints out a description of how to use the command.

<p />

## Examples

<p />

Packing documents locally:

<p />

```txt
cotopaxi pack example.json example.cdbpkg
```

<p />

Packing documents with an Azure DevOps pipeline:

<p />

```txt
cotopaxi pack $(Build.SourcesDirectory)/example.json $(Build.StagingDirectory)/example.cdbpkg
```

<p />

Packing documents with an Azure DevOps pipeline with commit ID as package version:

<p />

```txt
cotopaxi pack $(Build.SourcesDirectory)/example.json $(Build.StagingDirectory)/example.cdbpkg --version $(Build.SourceVersion)
```

<p />

The corresponding project file `example.json`:

<p />

```json
{
  "databases": [
    {
      "name": "adventureworks",
      "containers": [
        {
          "name": "products",
          "operations": [
            {
              "name": "upsert",
              "documents": [
                "adventureworks/products/**/*.json",
                "adventureworks/products/**/*-$(Version).json"
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

<p />

The corresponding documents file `adventureworks/products/bikes.json`:

<p />

```json
[
  {
    "id": "3202cb6f-42af-4fe6-a3c5-d61927721e75",
    "category": "bikes",
    "name": "Mountain-100 Silver, 38"
  }
]
```
