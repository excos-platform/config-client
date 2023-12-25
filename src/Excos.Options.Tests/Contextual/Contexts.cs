// Copyright (c) Marian Dziubiak and Contributors.
// Licensed under the Apache License, Version 2.0

using Microsoft.Extensions.Options.Contextual;

namespace Excos.Options.Tests.Contextual;

[OptionsContext]
internal partial class ContextWithIdentifier
{
    public string Identifier { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
}
