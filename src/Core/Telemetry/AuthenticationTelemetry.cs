using Altinn.Authorization.ServiceDefaults.Telemetry;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Altinn.Platform.Authentication.Core.Telemetry;

public sealed class AuthenticationTelemetry
{
    public static readonly string Name = "Altinn.Authentication";
    private static readonly ActivitySource _activitySource = new(Name);

    private readonly Meter _meter;

    private readonly Counter<int> _sessionsCreated;

    public AuthenticationTelemetry(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(Name);

        _sessionsCreated = _meter.CreateCounter<int>(
            name: "altinn.authentication.oidc.sessions_created",
            description: "Number of OIDC sessions created");
    }

    /// <summary>
    /// Creates and starts a new <see cref="Activity"/> object if there is any listener to the Activity, returns null otherwise.
    /// </summary>
    /// <param name="name">The operation name of the Activity.</param>
    /// <param name="kind">The <see cref="ActivityKind"/>.</param>
    /// <param name="parentContext">The parent <see cref="ActivityContext"/> object to initialize the created Activity object with.</param>
    /// <param name="tags">The optional tags list to initialize the created Activity object with.</param>
    /// <param name="links">The optional <see cref="ActivityLink"/> list to initialize the created Activity object with.</param>
    /// <param name="startTime">The optional start timestamp to set on the created Activity object.</param>
    /// <returns>The created <see cref="Activity"/> object or null if there is no any event listener.</returns>
    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        ActivityContext parentContext = default,
        ReadOnlySpan<KeyValuePair<string, object?>> tags = default,
        ReadOnlySpan<ActivityLink> links = default,
        DateTimeOffset startTime = default)
        => _activitySource.StartActivity(name, kind, parentContext, tags, links, startTime);

    public void SessionCreated()
    {
        _sessionsCreated.Add(1);
    }
}
