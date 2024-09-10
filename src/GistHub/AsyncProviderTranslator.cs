using System;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace GistHub;

/// <summary>
/// Translator class for implementing an async PSProvider.
/// Implementors should derive from this class, set the CmdletProvider
/// attribute and implement the SetupAsyncProvider() method to build the actual
/// async class for the provider.
/// </summary>
public abstract class AsyncProviderTranslator : NavigationCmdletProvider, IContentCmdletProvider
{
    private sealed class AsyncProviderInfo : ProviderInfo
    {
        public AsyncProviderInfo(ProviderInfo providerInfo, AsyncProviderBase provider)
            : base(providerInfo)
        {
            AsyncProvider = provider;
        }

        public AsyncProviderBase AsyncProvider { get; }
    }

    private readonly CancellationTokenSource _cancelSource = new();

    private AsyncProviderBase Provider => ((AsyncProviderInfo)ProviderInfo).AsyncProvider;

    protected override ProviderInfo Start(ProviderInfo providerInfo)
        => new AsyncProviderInfo(providerInfo, SetupAsyncProvider());

    protected override void Stop()
    {
        _cancelSource?.Dispose();
    }

    /// <summary>
    /// Implement this method to create the actual async provider class.
    /// </summary>
    protected abstract AsyncProviderBase SetupAsyncProvider();

    protected override PSDriveInfo? NewDrive(PSDriveInfo? drive)
    {
        if (drive is null)
        {
            WriteError(new ErrorRecord(
                new ArgumentNullException(nameof(drive)),
                "ProviderDriveNull",
                ErrorCategory.InvalidArgument,
                null));
            return null;
        }
        return RunBlockInAsync((p) => Provider.NewDriveAsync(p, drive));
    }

    protected override bool ItemExists(string path)
        => RunBlockInAsync((p) => Provider.ItemExistsAsync(p, path));

    protected override bool IsItemContainer(string path)
        => RunBlockInAsync((p) => Provider.IsItemContainerAsync(p, path));

    protected override bool IsValidPath(string path)
        => RunBlockInAsync((p) => Provider.IsValidPathAsync(p, path));

    protected override bool HasChildItems(string path)
        => RunBlockInAsync((p) => Provider.HasChildItemsAsync(p, path));

    protected override string GetChildName(string path)
        => RunBlockInAsync((p) => Provider.GetChildNameAsync(p, path));

    protected override void GetItem(string path)
        => RunBlockInAsync((p) => Provider.GetItemAsync(p, path));

    protected override object? GetChildNamesDynamicParameters(string path)
        => Provider.GetChildNamesDynamicParameters(path);

    protected override void GetChildNames(string path, ReturnContainers returnContainers)
        => RunBlockInAsync((p) => Provider.GetChildNamesAsync(p, path, returnContainers));

    protected override object? GetChildItemsDynamicParameters(string path, bool recurse)
        => Provider.GetChildItemsDynamicParameters(path, recurse);

    protected override void GetChildItems(string path, bool recurse, uint depth)
        => RunBlockInAsync((p) => Provider.GetChildItemsAsync(p, path, recurse, depth));

    protected override object? NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        => Provider.NewItemDynamicParameters(path, itemTypeName, newItemValue);

    protected override void NewItem(string path, string itemTypeName, object newItemValue)
        => RunBlockInAsync((p) => Provider.NewItemAsync(p, path, itemTypeName, newItemValue));

    protected override void RemoveItem(string path, bool recurse)
        => RunBlockInAsync((p) => Provider.RemoveItemAsync(p, path, recurse));

    public virtual object? ClearContentDynamicParameters(string path)
        => Provider.ClearContentDynamicParameters(path);

    public void ClearContent(string path)
        => RunBlockInAsync((p) => Provider.ClearContentAsync(p, path));

    public virtual object? GetContentReaderDynamicParameters(string path)
        => Provider.GetContentReaderDynamicParameters(path);

    public IContentReader? GetContentReader(string path)
        => RunBlockInAsync((p) => Provider.GetContentReaderAsync(p, path));

    public virtual object? GetContentWriterDynamicParameters(string path)
        => Provider.GetContentWriterDynamicParameters(path);

    public IContentWriter? GetContentWriter(string path)
        => RunBlockInAsync((p) => Provider.GetContentWriterAsync(p, path));

    protected override void StopProcessing()
    {
        _cancelSource.Cancel();
    }

    private void RunBlockInAsync(Func<AsyncPipeline, Task> task)
        => RunBlockInAsync<object?>(async (p) => { await task(p); return null; });

    private T RunBlockInAsync<T>(Func<AsyncPipeline, Task<T>> task)
    {
        // The invocation info is not exposed publicly but is used in the
        // provider. We use reflection to get the value and associate it with
        // the async pipeline being run.
        object providerContext = Reflection.CmdletProvider_Context.GetValue(this)!;
        InvocationInfo? myInvocation = (InvocationInfo?)(object?)Reflection.CmdletProviderContext_MyInvocation.GetValue(
            providerContext);

        var pipeOutChannel = Channel.CreateBounded<(object?, AsyncPipelineType)>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });
        var pipeInChannel = Channel.CreateBounded<object?>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        });

        AsyncPipeline pipeline = new(
            pipeOutChannel.Writer,
            pipeInChannel.Reader,
            _cancelSource.Token,
            Runspace.DefaultRunspace,
            PSDriveInfo,
            DynamicParameters,
            Force,
            Credential,
            myInvocation);

        Task<T> blockTask = Task.Run(async () =>
        {
            try
            {
                return await task(pipeline);
            }
            finally
            {
                pipeOutChannel.Writer.Complete();
                pipeInChannel.Writer.Complete();
            }
        });

        while (true)
        {
            object? data;
            AsyncPipelineType pipelineType;
            try
            {
                (data, pipelineType) = pipeOutChannel.Reader.ReadAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (ChannelClosedException)
            {
                break;
            }

            switch (pipelineType)
            {
                case AsyncPipelineType.Output:
                    (object item, string path, bool isContainer) = (ValueTuple<object, string, bool>)data!;
                    WriteItemObject(item, path, isContainer);
                    break;

                case AsyncPipelineType.Error:
                    WriteError((ErrorRecord)data!);
                    break;

                case AsyncPipelineType.Warning:
                    WriteWarning((string)data!);
                    break;

                case AsyncPipelineType.Verbose:
                    WriteVerbose((string)data!);
                    break;

                case AsyncPipelineType.Debug:
                    WriteDebug((string)data!);
                    break;

                case AsyncPipelineType.Information:
                    WriteInformation((InformationRecord)data!);
                    break;

                case AsyncPipelineType.Progress:
                    WriteProgress((ProgressRecord)data!);
                    break;

                case AsyncPipelineType.ShouldProcess:
                    (string target, string action) = (ValueTuple<string, string>)data!;
                    bool res = ShouldProcess(target, action);
                    pipeInChannel.Writer.WriteAsync(res).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    break;
            }
        }

        return blockTask.ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
