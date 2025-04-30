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
The path to a source or sources with deployment entries to format and prune.

<p />

## Options

<p />

- `-h|--help`  
Prints out a description of how to use the command.

<p />

## Examples

<p />

Formatting all project files:

<p />

```txt
cotopaxi format adventureworks/**/*.json
```
