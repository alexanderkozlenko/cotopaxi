# cotopaxi format

<p />

## Name

<p />

`cotopaxi format` - Formats and prunes the sources with deployment entries.

<p />

## Synopsis

<p />

```txt
cotopaxi format <source>

cotopaxi format -h|--help
```

<p />

## Description

<p />

The command formats sources, removes comments and the system properties: `_etag`, `_rid`, `_self`, `_ts`.

<p />

## Arguments

<p />

- `source`  
The path to a source or sources with deployment entries to format and prune (globbing patterns are supported).

<p />

## Options

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

Formatting a project file:

<p />

```txt
cotopaxi format adventureworks/products/bikes.json
```

<p />

Formatting all project files:

<p />

```txt
cotopaxi format adventureworks/**/*.json
```

<p />

## References

<p />

- [Microsoft - Azure Cosmos DB documents](https://learn.microsoft.com/en-us/rest/api/cosmos-db/documents)
