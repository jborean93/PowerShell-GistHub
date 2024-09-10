using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GistHub;

internal enum AsyncPipelineType
{
    Output,
    Error,
    Warning,
    Verbose,
    Debug,
    Information,
    Progress,
    ShouldProcess,
}

/// <summary>
/// Represents a pipeline that can be used to write output objects on the
/// various PowerShell streams. It also exposes the current provider state for
/// use by an async compatible provider.
/// </summary>
public sealed class AsyncPipeline
{
    private readonly ChannelWriter<(object?, AsyncPipelineType)> _outPipe;
    private readonly ChannelReader<object?> _inPipe;

    internal AsyncPipeline(
        ChannelWriter<(object?, AsyncPipelineType)> outPipe,
        ChannelReader<object?> inPipe,
        CancellationToken cancelToken,
        Runspace currentRunspace,
        PSDriveInfo? driveInfo,
        object? dynamicParameters,
        bool force,
        PSCredential credential,
        InvocationInfo? myInvocation)
    {
        _outPipe = outPipe;
        _inPipe = inPipe;
        CancelToken = cancelToken;
        CurrentRunspace = currentRunspace;
        DriveInfo = driveInfo;
        DynamicParameters = dynamicParameters;
        Force = force;
        Credential = credential;
        MyInvocation = myInvocation;
    }

    /// <summary>
    /// Gets the cancellation token that will be cancelled when the pipeline is
    /// stopped.
    /// </summary>
    public CancellationToken CancelToken { get; }

    /// <summary>
    /// Gets the current runspace for the provider. This is used instead of
    /// Runspace.DefaultRunspace as an async task is run on another thread and
    /// cannot access that property.
    /// </summary>
    public Runspace CurrentRunspace { get; }

    /// <summary>
    /// Gets the current PSDrive for the current operation.
    /// </summary>
    public PSDriveInfo? DriveInfo { get; }

    /// <summary>
    /// Gets the dynamic parameters for the current operation.
    /// </summary>
    public object? DynamicParameters { get; }

    /// <summary>
    /// Gets a value indicating whether -Force is specified.
    /// </summary>
    public bool Force { get; }

    /// <summary>
    /// Gets the credential specified by -Credential.
    /// </summary>
    public PSCredential Credential { get; }

    /// <summary>
    /// Gets the invocation information for the current operation. Will be null
    /// if the operation is not being run from a cmdlet.
    /// </summary>
    public InvocationInfo? MyInvocation { get; }

    public async Task<bool> ShouldProcessAsync(
        string target,
        string action,
        CancellationToken cancellationToken = default)
    {
        await WriteToOutputPipeAsync((target, action), AsyncPipelineType.ShouldProcess);
        return (bool)(await _inPipe.ReadAsync(cancellationToken))!;
    }

    public async Task WriteItemObjectAsync(object item, string path, bool isContainer)
        => await WriteToOutputPipeAsync((item, path, isContainer), AsyncPipelineType.Output);

    public async Task WriteErrorAsync(ErrorRecord errorRecord)
        => await WriteToOutputPipeAsync(errorRecord, AsyncPipelineType.Error);

    public async Task WriteWarningAsync(string message)
        => await WriteToOutputPipeAsync(message, AsyncPipelineType.Warning);

    public async Task WriteVerboseAsync(string message)
        => await WriteToOutputPipeAsync(message, AsyncPipelineType.Verbose);

    public async Task WriteDebugAsync(string message)
        => await WriteToOutputPipeAsync(message, AsyncPipelineType.Debug);

    public async Task WriteInformationAsync(InformationRecord informationRecord)
        => await WriteToOutputPipeAsync(informationRecord, AsyncPipelineType.Information);

    public async Task WriteProgressAsync(ProgressRecord progressRecord)
        => await WriteToOutputPipeAsync(progressRecord, AsyncPipelineType.Progress);

    private async Task WriteToOutputPipeAsync(object? data, AsyncPipelineType pipelineType)
        => await _outPipe.WriteAsync((data, pipelineType), CancelToken);
}
