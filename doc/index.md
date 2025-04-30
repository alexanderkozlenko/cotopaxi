# Cotopaxi - Overview

<p />

## About

<p />

A toolset for deploying data to Azure Cosmos DB as part of a cloud-native application, designed for straightforward integration with CI/CD pipelines. The package format is based on the Common Data Model (CDM) and the Open Packaging Conventions (OPC) standards.

<p />

## How to Use

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
              "documents": [
                "adventureworks/products/**/*.json"
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

Packing the database documents for deployment in Azure DevOps:

<p />

```txt
cotopaxi pack $(Build.SourcesDirectory)/adventureworks.json $(Build.StagingDirectory)/adventureworks.cdbpkg

Packing /home/vsts/work/1/s/adventureworks/products/bikes.json:$[0] for upsert in adventureworks\products
Packing /home/vsts/work/1/s/adventureworks/products/bikes.json:$[1] for upsert in adventureworks\products
```

<p />

Deploying the package to an Azure Cosmos DB account in Azure DevOps:

<p />

```txt
cotopaxi deploy $(System.ArtifactsDirectory)/**/*.cdbpkg

Deploying package /home/vsts/work/r1/a/adventureworks.cdbpkg to endpoint https://cosmos-adventureworks.documents.azure.com:443
Executing upsert cdbpkg:dfcf04cb-886e-ae82-9172-fa4a1acb5d8b:$[0] in adventureworks\products - HTTP 200
Executing upsert cdbpkg:dfcf04cb-886e-ae82-9172-fa4a1acb5d8b:$[1] in adventureworks\products - HTTP 200
```

<p />

## Documentation

<p />

- [Microsoft - Azure Cosmos DB service quotas](https://learn.microsoft.com/en-us/azure/cosmos-db/concepts-limits)
- [Microsoft - Common Data Model](https://learn.microsoft.com/en-us/common-data-model)
- [ECMA - Open Packaging Conventions](https://ecma-international.org/publications-and-standards/standards/ecma-376)
