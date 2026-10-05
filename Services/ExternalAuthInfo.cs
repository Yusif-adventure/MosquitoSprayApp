namespace SmartMosquitoControl.Services;

/// <summary>Tells views which external sign-in providers are actually configured.</summary>
public sealed record ExternalAuthInfo(bool GoogleEnabled);
