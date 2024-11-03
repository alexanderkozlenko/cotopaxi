---
uid: urn:topics:schemas
---

### Project JSON schemas

<p />

The package project schema:

<p />

```json
{
  "$schema": "https://json-schema.org/draft/2020-12",
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

<p />

The document collection schema:

<p />

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
