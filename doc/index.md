# Cotopaxi - Overview

<p />

## About

<p />

A toolset for deploying data to Azure Cosmos DB as part of a cloud-native application, designed for straightforward integration with CI/CD pipelines. The package format is based on the Common Data Model (CDM) and the Open Packaging Conventions (OPC) standards.

<p />

## Example

<p />

Creating a package project:

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

Packing the database documents for deployment in Azure DevOps:

<p />

```txt
cotopaxi pack $(Build.SourcesDirectory)/adventureworks.json $(Build.StagingDirectory)/adventureworks.cdbpkg

Building package /home/vsts/work/1/a/adventureworks.cdbpkg for project /home/vsts/work/1/s/adventureworks.json
Packing partition dfcf04cb-886e-ae82-9172-fa4a1acb5d8b for UPSERT in adventureworks\products
Packing document /home/vsts/work/1/s/adventureworks/products/bikes.json:$[0]
```

<p />

Deploying the package to an Azure Cosmos DB account in Azure DevOps:

<p />

```txt
cotopaxi deploy $(System.ArtifactsDirectory)/**/*.cdbpkg

Deploying package /home/vsts/work/r1/a/adventureworks.cdbpkg to https://adventureworks.documents.azure.com:443
Acquiring configuration for container adventureworks\products - HTTP 200 (2 RU)
Deploying partition dfcf04cb-886e-ae82-9172-fa4a1acb5d8b for UPSERT in adventureworks\products
Executing UPSERT dfcf04cb-886e-ae82-9172-fa4a1acb5d8b:$[0] - HTTP 200 (10.29 RU)
```

<p />

## Specifications

<p />

- [Microsoft - Common Data Model](https://learn.microsoft.com/en-us/common-data-model)
- [ECMA - Open Packaging Conventions](https://ecma-international.org/publications-and-standards/standards/ecma-376)
