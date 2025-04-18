﻿// (c) Oleksandr Kozlenko. Licensed under the MIT license.

using Microsoft.Azure.Cosmos;

namespace Cotopaxi.Cosmos.PackageManagement;

internal readonly record struct PackageDocumentKey
(
    string DatabaseName,
    string ContainerName,
    string DocumentId,
    PartitionKey DocumentPartitionKey
);
