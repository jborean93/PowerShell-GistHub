using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Threading.Tasks;

namespace GistHub;

public abstract class AsyncProviderBase
{
    public virtual Task<PSDriveInfo?> NewDriveAsync(AsyncPipeline pipeline, PSDriveInfo drive)
        => Task.FromResult((PSDriveInfo?)drive);

    public virtual Task<bool> ItemExistsAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual Task<bool> IsItemContainerAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual Task<bool> IsValidPathAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual Task<bool> HasChildItemsAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual Task<string> GetChildNameAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual Task GetItemAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual object? GetChildNamesDynamicParameters(string path)
        => null;

    public virtual Task GetChildNamesAsync(AsyncPipeline pipeline, string path, ReturnContainers returnContainers)
        => throw GetNotSupportedException();

    public virtual object? GetChildItemsDynamicParameters(string path, bool recurse)
        => null;

    public virtual Task GetChildItemsAsync(AsyncPipeline pipeline, string path, bool recurse, uint depth)
        => throw GetNotSupportedException();

    public virtual object? NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        => null;

    public virtual Task NewItemAsync(AsyncPipeline pipeline, string path, string? itemTypeName, object? newItemValue)
        => throw GetNotSupportedException();

    public virtual Task RemoveItemAsync(AsyncPipeline pipeline, string path, bool recurse)
        => throw GetNotSupportedException();

    public virtual object? ClearContentDynamicParameters(string path)
        => null;

    public virtual Task ClearContentAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual object? GetContentReaderDynamicParameters(string path)
        => null;

    public virtual Task<IContentReader?> GetContentReaderAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    public virtual object? GetContentWriterDynamicParameters(string path)
        => null;

    public virtual Task<IContentWriter?> GetContentWriterAsync(AsyncPipeline pipeline, string path)
        => throw GetNotSupportedException();

    private PSNotSupportedException GetNotSupportedException()
        => new("Provider operation stopped because the provider does not support this operation.");
}
