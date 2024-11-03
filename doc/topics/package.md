---
uid: urn:topics:package
---

### Format

<p />

- The CDM manifest has a package-level OPC relationship of role `http://microsoft.com/cdm/schema.manifest.cdm.json`.
- The CDM entities are defined in the `/cosmosdb.entities.cdm.json` document:

<p />

```json
{
  "jsonSchemaSemanticVersion": "1.1.0",
  "imports": [
    {
      "corpusPath": "cdm:/foundations.cdm.json"
    }
  ],
  "definitions": [
    {
      "entityName": "cosmosdb.document",
      "hasAttributes": [
        {
          "name": "database",
          "dataFormat": "String"
        },
        {
          "name": "container",
          "dataFormat": "String"
        },
        {
          "name": "operation",
          "dataFormat": "String"
        }
      ]
    }
  ]
}
```

<p />

### References

<p />

- [Microsoft - Common Data Model](https://learn.microsoft.com/en-us/common-data-model)
- [ECMA - Open Packaging Conventions](https://ecma-international.org/publications-and-standards/standards/ecma-376)
