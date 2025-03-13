# cotopaxi format

<p />

## Name

<p />

`cotopaxi format` - Formats and cleans up the files with deployment entries.

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

The package formats the JSON files, removes comments and the system properties: `_etag`, `_rid`, `_self`, `_ts`.

<p />

## Arguments

<p />

- `source`  
The path to a file or files with deployment entries to format and clean up.

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
