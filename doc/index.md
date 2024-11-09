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
  pack <project> <package>  Creates a package for an Azure Cosmos DB account
  deploy <package>          Deploys a package to an Azure Cosmos DB account
```

<p />

```txt
Usage:
  cotopaxi pack <project> <package> [options]

Arguments:
  <project>  The project to create a package from
  <package>  The package to create
```

<p />

```txt
Usage:
  cotopaxi deploy <package> [options]

Arguments:
  <package>  The package to deploy to the Azure Cosmos DB account

Options:
  --endpoint <endpoint>                    The address of the Azure Cosmos DB account
  --key <key>                              The account key or resource token for the Azure Cosmos DB account
  --connection-string <connection-string>  The connection string for the Azure Cosmos DB account
  --dry-run                                Show which operations would be executed, but don't execute them
 ```

<p />

Authorization parameters can be specified in environment variables:

<p />

- `AZURE_COSMOS_ENDPOINT` - the address of the Azure Cosmos DB account
- `AZURE_COSMOS_KEY` - the account key or resource token for the Azure Cosmos DB account
- `AZURE_COSMOS_CONNECTION_STRING` - the connection string for the Azure Cosmos DB account

<p />

The command-line parameters take precedence over the environment variables and the endpoint parameter takes precedence over the connection string parameter.

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
Reading project /home/vsts/work/1/s/project.json
Reading document collection /home/vsts/work/1/s/adventureworks/products/bikes.json
Packing cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json for UPSERT in adventureworks.products
Packing cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json:$[0] - OK
Successfully created package /home/vsts/work/1/a/package.cdbpkg
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
Deploying package /home/vsts/work/1/a/package.cdbpkg to https://adventureworks.documents.azure.com:443
Acquiring partition key configuration for adventureworks.products - OK (HTTP 200, 2 RU)
Deploying cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json to adventureworks.products
Executing UPSERT cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json:$[0] - OK (HTTP 200, 10.29 RU)
Successfully deployed package /home/vsts/work/1/a/package.cdbpkg (12.29 RU)
```

<p />

### Example: Deploying a package - "dry run" mode

<p />

An example command line and output for an Azure DevOps release:

<p />

```txt
cotopaxi deploy $(Build.ArtifactStagingDirectory)/**/*.cdbpkg --dry-run
```

<p />

```txt
[DRY-RUN] Deploying package /home/vsts/work/1/a/package.cdbpkg
[DRY-RUN] Deploying cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json to adventureworks.products
[DRY-RUN] Executing UPSERT cdbpkg:/cosmos.document/db93b376-c02e-4673-8ade-72485eb2c07c.json:$[0] ($.id: "3202cb6f-42af-4fe6-a3c5-d61927721e75")
[DRY-RUN] Successfully deployed package /home/vsts/work/1/a/package.cdbpkg
```
