# cotopaxi show

<p />

## Name

<p />

`cotopaxi show` - Shows information about database packages.

<p />

## Synopsis

<p />

```txt
cotopaxi show <package>

cotopaxi show -h|--help
```

<p />

## Arguments

<p />

- `package`  
The path to the database package or packages to show information about (globbing patterns are supported).

<p />

## Options

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

Showing information about a database package:

<p />

```txt
cotopaxi show adventureworks-v1.0.0.cdbpkg
```

<p />

Showing information about available database packages:

<p />

```txt
cotopaxi show **/*.cdbpkg
```
