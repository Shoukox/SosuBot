﻿namespace SosuBot;

public record OsuApiV2Configuration
{
    public required int ClientId { get; init; }
    public required string ClientSecret { get; init; }
}