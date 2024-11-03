### About

<p />

A toolset for deploying data to Azure Cosmos DB as part of a cloud-native application, designed for straightforward integration with CI/CD pipelines. The package format is based on the Common Data Model (CDM) and the Open Packaging Conventions (OPC) standards.

<p />

Supported deployment operations:

<p />

- `create` - creates a document with the `id` and a partition key value if the document doesn't exist.
- `update` - creates a document with the `id` and a partition key value if the document doesn't exist, or update if it exists.
- `delete` - deletes a document with the `id` and a partition key value if the document exists.

<p />

Any existing system generated properties `_attachments`, `_etag`, `_rid`, `_self`, and `_ts` are not included in a package.

<p />

### Usage

<p />

The supported command-line interface:

<p />

```txt
Usage:
  cotopaxi [command] [options]

Commands:
  pack <project> <package>  Creates a data package for Azure Cosmos DB
  deploy <package>          Deploys a data package to an Azure Cosmos DB instance
```

<p />

```txt
Usage:
  cotopaxi pack <project> <package> [options]

Arguments:
  <project>  Specifies the input project path
  <package>  Specifies the output package path
```

<p />

```txt
Usage:
  cotopaxi deploy <package> [options]

Arguments:
  <package>  Specifies the package to deploy

Options:
  --endpoint <endpoint>                    Specifies the Azure Cosmos DB endpoint
  --key <key>                              Specifies the Azure Cosmos DB account key or resource token
  --connection-string <connection-string>  Specifies the Azure Cosmos DB connection string
```

<p />

The environment variables can also be used to provide authentication information for deployment, however, they have lower precedence:

<p />

- `COSMOS_ENDPOINT` - specifies the Azure Cosmos DB endpoint.
- `COSMOS_KEY` - specifies the Azure Cosmos DB account key or resource token.
- `COSMOS_CONNECTION_STRING` - specifies the Azure Cosmos DB connection string.

<p />

The endpoint parameter takes precedence over the connection string parameter regardless of the configuration source.

<p />

### Example: Creating a package

<p />

An example package project `project.json`:

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

An example document collection for update `adventureworks/products/bikes.json`:

<p />

```json
[
  {
    "id": "3202cb6f-42af-4fe6-a3c5-d61927721e75",
    "category": "bikes",
    "name": "Mountain-100 Silver, 38",
    "price": "3578.27"
  }
]
```

<p />

An example command line and output for an Azure DevOps pipeline:

<p />

```txt
cotopaxi pack $(Build.SourcesDirectory)/project.json $(Build.ArtifactStagingDirectory)/package.cdbpkg
```

<p />

```txt
Reading /home/vsts/work/1/s/project.json
Reading /home/vsts/work/1/s/adventureworks/products/bikes.json
Packing urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c for UPSERT in adventureworks.products
Packing urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c:0 - OK
Successfully created /home/vsts/work/1/a/package.cdbpkg
```

<p />

### Example: Deploying a package

<p />

An example command line and output for an Azure DevOps release:

<p />

```txt
cotopaxi deploy $(Build.ArtifactStagingDirectory)/**/*.cdbpkg
```

<p />

```txt
Deploying 1 packages to https://adventureworks.documents.azure.com:443
Deploying /home/vsts/work/1/a/package.cdbpkg
Acquiring partition key paths for adventureworks.products - OK (HTTP 200, 2 RU)
Deploying urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c to adventureworks.products
Executing UPSERT urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c:0 - OK (HTTP 200, 10.29 RU)
Successfully deployed 1 packages to https://adventureworks.documents.azure.com:443 (12.29 RU)
```
