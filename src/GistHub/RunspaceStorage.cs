using GistHub.GitHub;
using System;
using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Threading;

namespace GistHub;

internal sealed class RunspaceSpecificStorage<T>
{
    private readonly ConditionalWeakTable<Runspace, Lazy<T>> _map = new();

    private readonly Func<T> _factory;

    private readonly LazyThreadSafetyMode _mode = LazyThreadSafetyMode.ExecutionAndPublication;

    public RunspaceSpecificStorage(Func<T> factory)
    {
        _factory = factory;
    }

    public T GetFromTLS()
        => GetForRunspace(Runspace.DefaultRunspace);

    public T GetForRunspace(Runspace runspace)
    {
        return _map.GetValue(
            runspace,
            _ => new Lazy<T>(() => _factory(), _mode))
            .Value;
    }
}

internal sealed class RunspaceStorage
{
    private static RunspaceSpecificStorage<RunspaceStorage> _registrations = new(() => new());

    public BearerToken? OAuthToken { get; set; }

    public Dictionary<string, UserGistCache> GistCache { get; } = new();

    public static RunspaceStorage GetFromTLS()
        => _registrations.GetFromTLS();

    public static RunspaceStorage GetForRunspace(Runspace runspace)
        => _registrations.GetForRunspace(runspace);
}
