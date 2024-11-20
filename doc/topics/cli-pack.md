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
Creates a document with the identifier and partition key if the document does not exist.
- `update`  
Creates a document with the identifier and partition key if the document does not exist, or update if it exists.
- `delete`  
Deletes a document with the identifier and partition key if the document exists.

<p />

A documents file must be a JSON array of objects, where each object has the `$.id` property.

<p />

The project format supports relative globbing patterns in `$.databases[*].containers[*].operations[*].documents[*]`.

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
              "documents": [ "adventureworks/products/**/*.json" ]
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

<p />

## Schemas

<p />

<details>
<summary>The project file schema</summary>

```json
{
  "$schema": "https://json-schema.org/draft/2020-12",
  "$id": "https://alexanderkozlenko.github.io/cotopaxi/schemas/project.json",
  "type": "object",
  "properties": {
    "databases": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "name": {
            "type": "string",
            "minLength": 1,
            "maxLength": 256
          },
          "containers": {
            "type": "array",
            "items": {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string",
                  "minLength": 1,
                  "maxLength": 256
                },
                "operations": {
                  "type": "array",
                  "items": {
                    "type": "object",
                    "properties": {
                      "name": {
                        "type": "string",
                        "enum": [
                          "create",
                          "upsert",
                          "delete"
                        ]
                      },
                      "documents": {
                        "type": "array",
                        "items": {
                          "type": "string"
                        }
                      }
                    },
                    "required": [
                      "name",
                      "documents"
                    ]
                  }
                }
              },
              "required": [
                "name",
                "operations"
              ]
            }
          }
        },
        "required": [
          "name",
          "containers"
        ]
      }
    }
  },
  "required": [
    "databases"
  ]
}
```

</details>

<p />

<details>
<summary>The documents file schema</summary>

```json
{
  "$schema": "https://json-schema.org/draft/2020-12",
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "id": {
        "type": "string",
        "minLength": 1,
        "maxLength": 255
      }
    },
    "required": [
      "id"
    ]
  }
}
```

</details>
