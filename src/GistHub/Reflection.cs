using System;
using System.Reflection;
using System.Management.Automation;
using System.Management.Automation.Provider;

namespace GistHub;

internal static class Reflection
{
    public static readonly Type CmdletProviderContext =
        typeof(PSObject).Assembly.GetType(
            "System.Management.Automation.CmdletProviderContext")
        ?? throw new InvalidOperationException("Could not find CmdletProviderContext type.");

    public static readonly PropertyInfo CmdletProvider_Context =
        typeof(CmdletProvider).GetProperty(
            "Context",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find CmdletProvider.Context property.");

    public static readonly PropertyInfo CmdletProviderContext_MyInvocation =
        CmdletProviderContext.GetProperty(
            "MyInvocation",
            BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find CmdletProviderContext.MyInvocation property.");
}
