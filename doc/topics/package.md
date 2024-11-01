---
uid: urn:topics:package
---

### Format

<p />

- The CDM namespace and URI scheme for the OPC package is `opc`.
- The CDM manifest has a package-level OPC relationship of type `http://microsoft.com/cdm/schema.manifest.cdm.json`.
- The CDM entity for database documents is named `cosmosdb.document` and has the following arguments:

<p />

| Name | Format | Purpose |
| - | - | - |
| `database` | `string` | The target database name. |
| `container` | `string` | The target container name. |
| `operation` | `string` | The operation to perform. |

<p />

### References

<p />

- [Microsoft - Common Data Model](https://learn.microsoft.com/en-us/common-data-model)
- [ECMA - Open Packaging Conventions](https://ecma-international.org/publications-and-standards/standards/ecma-376)
