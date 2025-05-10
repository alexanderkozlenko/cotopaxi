# cotopaxi pack

<p />

## Name

<p />

`cotopaxi pack` - Packs the deployment entries into a database package.

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
The path to the project that specifies deployment entries to include, based on schema [`project.schema.json`](https://alexanderkozlenko.github.io/cotopaxi/schemas/project.schema.json).

<p />

- `package`  
The path to a resulting database package.

<p />

## Options

<p />

- `--version <version>`  
Sets the package version information.

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

Packing documents with Azure Pipelines:

<p />

```yaml
- script: >
    dotnet tool run cotopaxi pack
    $(Build.SourcesDirectory)/adventureworks.json
    $(Build.StagingDirectory)/adventureworks-v1.0.0.cdbpkg
```

<p />

The corresponding package source `adventureworks/products/bikes.json`:

<p />

```json
[
  {
    "id": "3202cb6f-42af-4fe6-a3c5-d61927721e75",
    "category": "bikes",
    "name": "Mountain-100 Silver, 38"
  },
  {
    "id": "e1894e24-550d-4fe3-9784-47d614600baa",
    "category": "bikes",
    "name": "Mountain-500 Black, 40"
  }
]
```

<p />

The corresponding package project `adventureworks.json`:

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
                "adventureworks/products/**/*-v$(Version).json"
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

## References

<p />

- [Microsoft - Azure Cosmos DB service quotas](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits)
