# cotopaxi show

<p />

## Name

<p />

`cotopaxi show` - Shows information about a database package.

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
The path to the database package to show information about.

<p />

## Options

<p />

- `-h|--help`  
Show help and usage information.

<p />

## Examples

<p />

#### Showing information about a database package:

<p />

```txt
dotnet tool run cotopaxi show adventureworks-v1.0.0.cdbpkg
```

<p />

```txt
/home/vsts/work/r1/a/adventureworks-v1.0.0.cdbpkg: 0196f43b-4a47-7207-a702-2ef28659cd1a 2025-05-04T00:00:08.5198965Z (1.0.0)
cdbpkg:/cosmosdb.document/0196f43b-498e-7098-a28d-1e7d0e44e9dd.json:$[0]: upsert adventureworks\products\3202cb6f-42af-4fe6-a3c5-d61927721e75 (4)
cdbpkg:/cosmosdb.document/0196f43b-498e-7098-a28d-1e7d0e44e9dd.json:$[1]: upsert adventureworks\products\e1894e24-550d-4fe3-9784-47d614600baa (4)
```
