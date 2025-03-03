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

Building deployment package /home/vsts/work/1/a/adventureworks.cdbpkg for project /home/vsts/work/1/s/adventureworks.json
Packing deployment entries dfcf04cb-886e-ae82-9172-fa4a1acb5d8b for container adventureworks\products (upsert)
Packing deployment entry /home/vsts/work/1/s/adventureworks/products/bikes.json:$[0]
```

<p />

Deploying the package to an Azure Cosmos DB account in Azure DevOps:

<p />

```txt
cotopaxi deploy $(System.ArtifactsDirectory)/**/*.cdbpkg

Deploying package /home/vsts/work/r1/a/adventureworks.cdbpkg to account cosmos-adventureworks
Requesting properties for container adventureworks\products - HTTP 200
Deploying entries cdbpkg:dfcf04cb-886e-ae82-9172-fa4a1acb5d8b to container adventureworks\products (upsert)
Deploying entry cdbpkg:dfcf04cb-886e-ae82-9172-fa4a1acb5d8b:$[0] (upsert) - HTTP 200
```

<p />

## Specifications

<p />

- [Microsoft - Common Data Model](https://learn.microsoft.com/en-us/common-data-model)
- [ECMA - Open Packaging Conventions](https://ecma-international.org/publications-and-standards/standards/ecma-376)
