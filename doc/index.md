### About

<p />

A toolset for deploying data to Azure Cosmos DB as part of a cloud-native application, designed for straightforward integration with CI/CD pipelines. The package model is based on the Common Data Model (CDM) and the Open Packaging Conventions (OPC) standards.

<p />

Supported deployment operations:

<p />

- `create` - creates the document with the partition key value and ID if it doesn't exist.
- `update` - creates the document with the partition key value and ID if it doesn't exist, or update the document if it exists.
- `delete` - deletes the document with the partition key value and ID if it exists.

<p />

Command-line interface:

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
  --connection-string <connection-string>  Specifies the connection string (defaults to COSMOS_CONNECTION_STRING environment variable)
  --endpoint <endpoint>                    Specifies the endpoint (defaults to COSMOS_ENDPOINT environment variable)
  --key <key>                              Specifies the account key or resource token (defaults to COSMOS_KEY environment variable)
  ```

<p />

> [!NOTE]
> Any existing system generated properties `_attachments`, `_etag`, `_rid`, `_self`, and `_ts` are not included in a created package.

<p />

### Example - creating a data package

<p />

An example project file content:

<p />

```json
{
  "databases": [
    {
      "name": "my_database",
      "containers": [
        {
          "name": "my_container",
          "operations": [
            {
              "name": "upsert",
              "sources": [
                "my_documents.json"
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

An example command line and output:

<p />

```txt
cotopaxi pack my_project.json my_package.cdbpkg
```

<p />

```txt
Reading /home/vsts/work/1/a/my_project.json
Reading /home/vsts/work/1/a/my_documents.json
Packing urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c for UPSERT in my_database\my_container
Packing urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c:0 - OK
Created /home/vsts/work/1/a/my_package.cdbpkg
```

<p />

### Example - deploying a data package

<p />

An example command line and output:

<p />

```txt
cotopaxi deploy my_package.cdbpkg
```

<p />

```txt
Unpacking /home/vsts/work/1/a/my_package.cdbpkg
Deploying the package to https://mycosmosdb.documents.azure.com:443
Acquiring partition key paths for my_database\my_container - OK (HTTP 200, 2 RU)
Deploying urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c to my_database\my_container
UPSERT urn:cdbpkg:db93b376-c02e-4673-8ade-72485eb2c07c:0 - OK (HTTP 200, 10.29 RU)
Deploying the package to https://mycosmosdb.documents.azure.com:443 - DONE (12.29 RU)
```

<p />

### Project JSON schemas

<p />

A package project schema:

<p />

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
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
                      "sources": {
                        "type": "array",
                        "items": {
                          "type": "string",
                          "minLength": 1
                        },
                        "uniqueItems": true
                      }
                    },
                    "required": [
                      "name",
                      "sources"
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

<p />

A package project source schema:

<p />

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
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
