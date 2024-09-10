using GistHub.GitHub;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;

namespace GistHub;

[CmdletProvider(ProviderName, ProviderCapabilities.Credentials | ProviderCapabilities.ShouldProcess)]
[OutputType(typeof(Shared.GistInfo), typeof(Shared.GistFile), ProviderCmdlet = ProviderCmdlet.GetItem)]
[OutputType(typeof(Shared.GistInfo), typeof(Shared.GistFile), ProviderCmdlet = ProviderCmdlet.GetChildItem)]
[OutputType(typeof(byte), typeof(string), ProviderCmdlet = ProviderCmdlet.GetContent)]
[OutputType(typeof(Shared.GistFile), ProviderCmdlet = ProviderCmdlet.NewItem)]
public sealed class GistProvider : AsyncProviderTranslator
{
    public const string ProviderName = "GistHub";

    protected override AsyncProviderBase SetupAsyncProvider()
        => new GistAsyncProvider();

    protected override Collection<PSDriveInfo> InitializeDefaultDrives()
        => new()
        {
            new PSDriveInfo(
                "Gist",
                ProviderInfo,
                string.Empty,
                "GistHub Root Provider",
                null)
        };
}

internal sealed class GistAsyncProvider : AsyncProviderBase
{
    public override async Task<PSDriveInfo?> NewDriveAsync(AsyncPipeline pipeline, PSDriveInfo drive)
    {
        await pipeline.WriteVerboseAsync($"NewDrive: '{drive.Name}'").ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(drive.Root))
        {
            return drive;
        }

        // If mapping with a user root, validate that the user exists and store
        // the initial gists in a cache for later use.
        BearerToken? bearerToken = GetBearerToken(pipeline);
        string ghUser = drive.Root;

        bool validUser = await GitHubClient.TestGitHubUser(
            ghUser,
            token: bearerToken,
            pipeline: pipeline,
            cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
        if (!validUser)
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException($"Cannot create gist drive for GitHub User '{ghUser}' that does not exist."),
                "GistHubUserNotFound",
                ErrorCategory.ObjectNotFound,
                ghUser)).ConfigureAwait(false);
            return null;
        }

        await UpdateUserGistsAsync(pipeline, GistPath.Parse(ghUser), bearerToken).ConfigureAwait(false);
        return drive;
    }

    public override async Task<bool> ItemExistsAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"ItemExists: '{path}'").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.UserName))
        {
            return true;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            return await GitHubClient.TestGitHubUser(
                gistPath.UserName!,
                token: bearerToken,
                pipeline: pipeline,
                cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
        }

        // Try to get the gist from the cache first, if it is not there then
        // get it from the server.
        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        if (!cache.Gists.TryGetValue(gistPath.GistId!, out Gist? gistInfo))
        {
            gistInfo = await GitHubClient.GetGistAsync(
                gistPath.GistId!,
                token: bearerToken,
                cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
            if (gistInfo is not null)
            {
                cache.UpdateGistCache(gistInfo);
            }
        }

        if (gistInfo is null)
        {
            return false;
        }
        else if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            return true;
        }
        else
        {
            return gistInfo.Files.ContainsKey(gistPath.FileName!);
        }
    }

    public override async Task<bool> IsItemContainerAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"IsItemContainer: '{path}'").ConfigureAwait(false);
        // Gists are containers, files in a gist are not.
        return string.IsNullOrWhiteSpace(GistPath.Parse(path).FileName);
    }

    public override async Task<bool> IsValidPathAsync(AsyncPipeline pipeline, string? path)
    {
        await pipeline.WriteVerboseAsync($"IsValidPath: '{path}'").ConfigureAwait(false);
        // All paths are considered valid.
        return true;
    }

    public override async Task<bool> HasChildItemsAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"HasChildItems: '{path}'").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            return true;
        }
        if (!string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            return false;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        if (cache.Gists.TryGetValue(gistPath.GistId!, out Gist? gist))
        {
            return gist.Files.Count > 0;
        }
        else
        {
            return false;
        }
    }

    public override Task<string> GetChildNameAsync(AsyncPipeline pipeline, string path)
    {
        // The default virtual method calls ItemExists multiple times, it is
        // more efficient to just return the result from the path string.
        GistPath gistPath = GistPath.Parse(path);
        return Task.FromResult(gistPath.FileName ?? gistPath.GistId ?? gistPath.UserName ?? string.Empty);
    }

    public override async Task GetItemAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"GetItem: '{path}'").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.UserName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub User name is required when getting an item."),
                "GistHubUserRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }
        else if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("Gist Id is required when getting an item."),
                "GistHubIdRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        if (!cache.Gists.TryGetValue(gistPath.GistId!, out Gist? gist))
        {
            await WriteGistNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
            return;
        }

        Shared.GistInfo gistInfo = CreateGistInfo(pipeline, gist);
        if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            await pipeline.WriteItemObjectAsync(gistInfo, path, true).ConfigureAwait(false);
            return;
        }

        foreach (Shared.GistFile file in gistInfo.Files)
        {
            if (file.Name == gistPath.FileName)
            {
                await pipeline.WriteItemObjectAsync(file, path, false).ConfigureAwait(false);
                return;
            }
        }
        await WriteGistFileNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
    }

    public override object? GetChildNamesDynamicParameters(string path)
        => new GistGetChildDynamicParameters();

    public override async Task GetChildNamesAsync(AsyncPipeline pipeline, string path, ReturnContainers returnContainers)
    {
        await pipeline.WriteVerboseAsync($"GetChildNames: Path '{path}' ReturnContainers {returnContainers}").ConfigureAwait(false);
        await GetChildItemsOrNamesAsync(pipeline, path, true).ConfigureAwait(false);
    }

    public override object? GetChildItemsDynamicParameters(string path, bool recurse)
        => new GistGetChildDynamicParameters();

    public override async Task GetChildItemsAsync(AsyncPipeline pipeline, string path, bool recurse, uint depth)
    {
        await pipeline.WriteVerboseAsync($"GetChildItems: Path '{path}' Recurse {recurse} Depth {depth}").ConfigureAwait(false);
        await GetChildItemsOrNamesAsync(pipeline, path, false).ConfigureAwait(false);
    }
    public async Task GetChildItemsOrNamesAsync(AsyncPipeline pipeline, string path, bool returnNames)
    {
        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath!.UserName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub User name is required when enumerating children."),
                "GistHubUserRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            foreach (KeyValuePair<string, Gist> gist in cache.Gists)
            {
                Shared.GistInfo gistInfo = CreateGistInfo(pipeline, gist.Value);
                if (returnNames)
                {
                    await pipeline.WriteItemObjectAsync(gistInfo.Id, gistInfo.ProviderPath, true).ConfigureAwait(false);
                }
                else
                {
                    await pipeline.WriteItemObjectAsync(gistInfo, gistInfo.ProviderPath, true).ConfigureAwait(false);
                }
            }
        }
        else if (cache.Gists.TryGetValue(gistPath.GistId!, out Gist? gist))
        {
            Shared.GistInfo gistInfo = CreateGistInfo(pipeline, gist);

            if (returnNames)
            {
                foreach (Shared.GistFile file in gistInfo.Files)
                {
                    await pipeline.WriteItemObjectAsync(file.Name, file.ProviderPath, false).ConfigureAwait(false);
                }
            }
            else
            {
                foreach (Shared.GistFile file in gistInfo.Files)
                {
                    await pipeline.WriteItemObjectAsync(file, file.ProviderPath, false).ConfigureAwait(false);
                }
            }
        }
        else
        {
            await WriteGistNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
        }
    }

    public override object? NewItemDynamicParameters(string path, string itemTypeName, object newItemValue)
        => new GistNewItemDynamicParameters();

    public override async Task NewItemAsync(AsyncPipeline pipeline, string path, string? itemTypeName, object? newItemValue)
    {
        await pipeline.WriteVerboseAsync($"NewItem: '{path}' ItemTypeName '{itemTypeName}' - {newItemValue}").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.UserName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub User name is required when creating a gist."),
                "GistHubUserRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("Gist Id or filename is required when creating an item."),
                "GistHubIdOrFileNameRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }
        if (!string.IsNullOrWhiteSpace(itemTypeName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GistHub does not support any item type value to be set."),
                "GistHubItemTypeNotSupported",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }

        // If no gist id was specified the gist id portion is the filename of
        // a new gist to create. Otherwise we create a new file in the
        // specified gist.
        string? gistId = null;
        string fileName;
        if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            fileName = gistPath.GistId!;
        }
        else
        {
            gistId = gistPath.GistId;
            fileName = gistPath.FileName!;
        }

        GistNewItemDynamicParameters? newParams = pipeline.DynamicParameters as GistNewItemDynamicParameters;
        string gistContent = LanguagePrimitives.ConvertTo<string>(newItemValue);
        string? description = newParams?.Description;
        bool publicGist = newParams?.Public == true;

        if (string.IsNullOrWhiteSpace(gistContent))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("Gist content value is required when creating a gist."),
                "GistHubContentRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        if (bearerToken is null)
        {
            await WriteGistTokenRequiredErrorAsync(pipeline, gistPath, "create a gist").ConfigureAwait(false);
            return;
        }

        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        Gist? existingGist = null;
        if (gistId is not null)
        {
            if (!cache.Gists.TryGetValue(gistId, out existingGist))
            {
                await WriteGistNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
                return;
            }
        }

        Gist? newGist = null;
        if (existingGist is not null)
        {
            if (publicGist && !existingGist.Public)
            {
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new InvalidOperationException("Cannot change a private gist to a public gist."),
                    "GistHubCannotChangeGistScope",
                    ErrorCategory.PermissionDenied,
                    path)).ConfigureAwait(false);
                return;
            }
            if (!string.IsNullOrWhiteSpace(description) && existingGist.Description != description && !pipeline.Force)
            {
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new InvalidOperationException(
                        "Cannot change the description of an existing gist. Set -Force to overwrite."),
                    "GistHubCannotChangeGistDescription",
                    ErrorCategory.PermissionDenied,
                    path)).ConfigureAwait(false);
                return;
            }

            if (existingGist.Files.TryGetValue(fileName, out GistFile? _) && !pipeline.Force)
            {
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new InvalidOperationException("Cannot create gist, file already exists. Set -Force to overwrite."),
                    "GistHubCannotCreateDuplicateFile",
                    ErrorCategory.ResourceExists,
                    path)).ConfigureAwait(false);
                return;
            }

            if (await pipeline.ShouldProcessAsync(path, "Create Gist file").ConfigureAwait(false))
            {
                newGist = await GitHubClient.UpdateGistAsync(
                    existingGist.Id,
                    new UpdateGist(
                        description: description,
                        files: new Dictionary<string, UpdateGistFile?>
                        {
                            { fileName, new UpdateGistFile(fileName: null, content: gistContent) }
                        }),
                    token: bearerToken,
                    responseAsBase64Content: false,
                    pipeline: pipeline,
                    cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
            }
        }
        else
        {
            if (await pipeline.ShouldProcessAsync(path, "Create Gist").ConfigureAwait(false))
            {
                newGist = await GitHubClient.CreateGistAsync(
                    new CreateGist(
                        description: description,
                        files: new Dictionary<string, CreateGistFile>
                        {
                            { fileName, new CreateGistFile(gistContent) }
                        },
                        @public: publicGist),
                    token: bearerToken,
                    pipeline: pipeline,
                    cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
            }
        }

        // -Whatif won't have this set.
        if (newGist is not null)
        {
            cache.UpdateGistCache(newGist);
            Shared.GistInfo gistInfo = CreateGistInfo(pipeline, newGist);
            foreach (Shared.GistFile file in gistInfo.Files)
            {
                if (file.Name == fileName)
                {
                    await pipeline.WriteItemObjectAsync(file, file.ProviderPath, false).ConfigureAwait(false);
                    break;
                }
            }
        }
    }

    public override async Task RemoveItemAsync(AsyncPipeline pipeline, string path, bool recurse)
    {
        await pipeline.WriteVerboseAsync($"RemoveItem: '{path}' Recurse {recurse}").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath!.UserName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub User name is required when removing gists."),
                "GistHubUserRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }
        else if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("Gist Id is required when removing an item."),
                "GistHubIdRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return;
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        if (bearerToken is null)
        {
            await WriteGistTokenRequiredErrorAsync(pipeline, gistPath, "remove a gist").ConfigureAwait(false);
            return;
        }

        UserGistCache cache = await UpdateUserGistsAsync(pipeline, gistPath, bearerToken).ConfigureAwait(false);
        if (!cache.Gists.TryGetValue(gistPath.GistId!, out Gist? gist))
        {
            await WriteGistNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
            return;
        }

        if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            if (await pipeline.ShouldProcessAsync(gistPath.Path, "Remove Gist").ConfigureAwait(false))
            {
                await GitHubClient.DeleteGistAsync(
                    gist.Id,
                    token: bearerToken,
                    pipeline: pipeline,
                    cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
                cache.Gists.Remove(gist.Id);
            }

            return;
        }
        else if (gist.Files.TryGetValue(gistPath.FileName!, out GistFile? file))
        {
            if (await pipeline.ShouldProcessAsync(gistPath.Path, "Remove Gist File").ConfigureAwait(false))
            {
                Gist newInfo = await GitHubClient.UpdateGistAsync(
                    gist.Id,
                    new UpdateGist(
                        description: null,
                        files: new Dictionary<string, UpdateGistFile?>
                        {
                            { file.FileName, null }
                        }),
                    token: bearerToken,
                    responseAsBase64Content: false,
                    pipeline: pipeline,
                    cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
                cache.UpdateGistCache(newInfo);
            }
        }
        else
        {
            await WriteGistFileNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
        }
    }

    public override object? GetContentReaderDynamicParameters(string path)
            => new GistGetContentDynamicParameters();

    public override async Task ClearContentAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"ClearContent: '{path}'").ConfigureAwait(false);

        if (pipeline.MyInvocation?.MyCommand is CmdletInfo cmdletInfo &&
            cmdletInfo.ImplementingType.FullName == "Microsoft.PowerShell.Commands.SetContentCommand")
        {
            return;
        }

        // The Gist API cannot set a gist to an empty string so we cannot clear the content.
        throw new PSNotSupportedException();
    }

    public override async Task<IContentReader?> GetContentReaderAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"GetContentReader: '{path}'").ConfigureAwait(false);

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub gist id is required when getting the content."),
                "GistHubGistIdRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return null;
        }

        GistGetContentDynamicParameters? contentParms = pipeline.DynamicParameters as GistGetContentDynamicParameters;
        bool asByteStream = contentParms?.AsByteStream == true;
        bool raw = contentParms?.Raw == true;
        string? delimiter = contentParms?.Delimiter;

        if (!string.IsNullOrEmpty(delimiter))
        {
            if (asByteStream)
            {
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new ArgumentException("-Delimiter cannot be used with -AsByteStream."),
                    "GistHubDelimiterAsByteStream",
                    ErrorCategory.InvalidArgument,
                    path)).ConfigureAwait(false);
                return null;
            }
            else if (raw)
            {
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new ArgumentException("-Delimiter cannot be used with -Raw."),
                    "GistHubDelimiterRaw",
                    ErrorCategory.InvalidArgument,
                    path)).ConfigureAwait(false);
                return null;
            }
        }

        BearerToken? bearerToken = GetBearerToken(pipeline);
        Stream? contentStream = await GetGistFileContentAsync(
            pipeline,
            bearerToken,
            gistPath,
            asByteStream).ConfigureAwait(false);
        if (contentStream is null)
        {
            return null;
        }

        return new GistReader(contentStream, delimiter, !asByteStream, raw);
    }

    public override object? GetContentWriterDynamicParameters(string path)
        => new GistSetContentDynamicParameters();

    public override async Task<IContentWriter?> GetContentWriterAsync(AsyncPipeline pipeline, string path)
    {
        await pipeline.WriteVerboseAsync($"GetContentWriter: '{path}'").ConfigureAwait(false);

        // Hack to determine if ClearContent was called in Set-Content. We
        // need to seek the stream to the beginning if it was.
        bool shouldClear = pipeline.MyInvocation?.MyCommand is CmdletInfo cmdletInfo &&
            cmdletInfo.ImplementingType.FullName == "Microsoft.PowerShell.Commands.SetContentCommand";

        GistPath gistPath = GistPath.Parse(path);
        if (string.IsNullOrWhiteSpace(gistPath.GistId))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub gist id is required when setting the content."),
                "GistHubGistIdRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return null;
        }
        if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            await pipeline.WriteErrorAsync(new ErrorRecord(
                new ArgumentException("GitHub gist file name is required when setting the content."),
                "GistHubFileNameRequired",
                ErrorCategory.InvalidArgument,
                path)).ConfigureAwait(false);
            return null;
        }

        GistSetContentDynamicParameters? contentParms = pipeline.DynamicParameters as GistSetContentDynamicParameters;
        string? delimiter = contentParms?.Delimiter;

        BearerToken? bearerToken = GetBearerToken(pipeline);
        if (bearerToken is null)
        {
            await WriteGistTokenRequiredErrorAsync(pipeline, gistPath, "get content writer").ConfigureAwait(false);
            return null;
        }

        // We still attempt to get the content stream to verify the gist
        // exists.
        Stream? contentStream = await GetGistFileContentAsync(
            pipeline,
            bearerToken,
            gistPath,
            false,
            missingAsEmpty: true).ConfigureAwait(false);
        if (contentStream is null)
        {
            // Will be null if there was an error getting the content.
            return null;
        }

        if (shouldClear)
        {
            contentStream = new MemoryStream();
        }
        IContentWriter writer = new GistWriter(
            bearerToken,
            delimiter,
            gistPath,
            contentStream);

        return writer;
    }

    private BearerToken? GetBearerToken(AsyncPipeline pipeline)
    {
        /*
        Uses the following logic for getting the Bearer Token
            1. Explicit -Credential on cmdlet
            2. Explicit -Credential on PSDrive
            3. Default token set by Connect-GistHub
        */
        BearerToken? token;

        // Cmdlets like Get-ChildItem don't have a -Credential
        // parameter, we check if our custom dynamic parameters that
        // add it back in has it set, otherwise use the normal logic.
        if (pipeline.DynamicParameters is GistGetChildDynamicParameters dynParams &&
            dynParams.Credential != PSCredential.Empty &&
            dynParams.Credential.Password.Length > 0)
        {
            token = new BearerToken(dynParams.Credential.GetNetworkCredential().Password, null);
        }
        else if (pipeline.Credential != PSCredential.Empty && pipeline.Credential.Password.Length > 0)
        {
            token = new BearerToken(pipeline.Credential.GetNetworkCredential().Password, null);
        }
        else if (pipeline.DriveInfo is not null &&
            pipeline.DriveInfo.Credential != PSCredential.Empty &&
            pipeline.DriveInfo.Credential.Password.Length > 0)
        {
            token = new BearerToken(pipeline.DriveInfo.Credential.GetNetworkCredential().Password, null);
        }
        else
        {
            token = RunspaceStorage.GetForRunspace(pipeline.CurrentRunspace).OAuthToken;
        }

        return token;
    }

    private async Task WriteGistNotFoundErrorAsync(AsyncPipeline pipeline, GistPath gist)
    {
        await pipeline.WriteErrorAsync(new ErrorRecord(
            new ItemNotFoundException($"Gist Id '{gist.GistId}' is not found."),
            "GistHubGistNotFound",
            ErrorCategory.ObjectNotFound,
            gist.Path)).ConfigureAwait(false);
    }

    private async Task WriteGistFileNotFoundErrorAsync(AsyncPipeline pipeline, GistPath gist)
    {
        await pipeline.WriteErrorAsync(new ErrorRecord(
            new ItemNotFoundException($"Gist file '{gist.FileName}' not found in Gist '{gist.GistId}'."),
            "GistHubGistFileNotFound",
            ErrorCategory.ObjectNotFound,
            gist.Path)).ConfigureAwait(false);
    }

    private async Task WriteGistTokenRequiredErrorAsync(AsyncPipeline pipeline, GistPath gist, string action)
    {
        await pipeline.WriteErrorAsync(new ErrorRecord(
            new AuthenticationException($"Cannot {action} without authentication token."),
            "GistHubNoToken",
            ErrorCategory.PermissionDenied,
            gist.Path)).ConfigureAwait(false);
    }

    private Shared.GistInfo CreateGistInfo(AsyncPipeline pipeline, Gist gist)
    {
        ProviderInfo provider = pipeline.DriveInfo!.Provider;
        string gistLocation = $"{gist.Owner.Login}{Path.DirectorySeparatorChar}{gist.Id}";
        string gistPath = $"Gist:{gistLocation}";
        string gistPSPath = $"{provider}::{gistLocation}";

        Shared.GistFile[] files = new Shared.GistFile[gist.Files.Count];
        Shared.GistInfo gistInfo = new(
            gist.Id,
            gistPath,
            gist.Description,
            gist.HtmlUrl,
            gist.GitPullUrl,
            files,
            gist.CreatedAt,
            gist.UpdatedAt,
            gist.Owner.Login,
            !gist.Public,
            gistLocation);

        PSObject psGist = PSObject.AsPSObject(gistInfo);
        psGist.Properties.Add(new PSNoteProperty("PSPath", gistPSPath));
        psGist.Properties.Add(new PSNoteProperty("PSParentPath", $"{provider}::"));
        psGist.Properties.Add(new PSNoteProperty("PSChildName", gist.Id));
        psGist.Properties.Add(new PSNoteProperty("PSDrive", pipeline.DriveInfo));
        psGist.Properties.Add(new PSNoteProperty("PSProvider", provider));
        psGist.Properties.Add(new PSNoteProperty("PSIsContainer", true));

        int i = 0;
        foreach (GistFile file in gist.Files.Values)
        {
            Shared.GistFile gistFile = new(
                file.FileName,
                $"{gistPath}{Path.DirectorySeparatorChar}{file.FileName}",
                file.Language,
                file.RawUrl,
                file.Size,
                gistInfo);

            // WriteItemObject adds these properties, we repeat it here to
            // ensure the caller using these values work as expected.
            PSObject psFile = PSObject.AsPSObject(gistFile);
            psFile.Properties.Add(new PSNoteProperty(
                "PSPath",
                $"${gistPSPath}{Path.DirectorySeparatorChar}{file.FileName}"));
            psFile.Properties.Add(new PSNoteProperty("PSParentPath", gistPSPath));
            psFile.Properties.Add(new PSNoteProperty("PSChildName", file.FileName));
            psFile.Properties.Add(new PSNoteProperty("PSDrive", pipeline.DriveInfo));
            psFile.Properties.Add(new PSNoteProperty("PSProvider", provider));
            psFile.Properties.Add(new PSNoteProperty("PSIsContainer", false));

            files[i] = gistFile;
            i++;
        }

        return gistInfo;
    }

    private async Task<Stream?> GetGistFileContentAsync(
        AsyncPipeline pipeline,
        BearerToken? bearerToken,
        GistPath gistPath,
        bool readAsBase64,
        bool missingAsEmpty = false)
    {
        Debug.Assert(gistPath.GistId is not null);

        Gist? gist = await GitHubClient.GetGistAsync(
            gistPath.GistId!,
            readAsBase64: readAsBase64,
            token: bearerToken,
            cancellationToken: pipeline.CancelToken).ConfigureAwait(false);

        if (gist is null)
        {
            await WriteGistNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
            return null;
        }

        GistFile? file;
        if (string.IsNullOrWhiteSpace(gistPath.FileName))
        {
            if (gist!.Files.Count != 1)
            {
                string fileNames = string.Join("', '", gist.Files.Keys);
                string msg = "GitHub gist file name is required when getting the " +
                    $"content of a gist with multiple files. Available files: '{fileNames}'";
                await pipeline.WriteErrorAsync(new ErrorRecord(
                    new ArgumentException(msg),
                    "GistHubFileRequired",
                    ErrorCategory.InvalidArgument,
                    gistPath.Path));
                return null;
            }

            using IEnumerator<GistFile> enumerator = gist.Files.Values.GetEnumerator();
            enumerator.MoveNext();
            file = enumerator.Current;
        }
        else if (!gist!.Files.TryGetValue(gistPath.FileName!, out file))
        {
            if (missingAsEmpty)
            {
                return new MemoryStream();
            }
            else
            {
                await WriteGistFileNotFoundErrorAsync(pipeline, gistPath).ConfigureAwait(false);
                return null;
            }
        }

        if (file.Truncated)
        {
            return await GitHubClient.GetGistFileStreamAsync(
                file.RawUrl,
                token: bearerToken,
                pipeline: pipeline,
                cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
        }
        else if (readAsBase64)
        {
            return new MemoryStream(Convert.FromBase64String(file.Content ?? ""));
        }
        else
        {
            // The content needs to be a Stream so we build one from the
            // encoded bytes.
            return new MemoryStream(Encoding.UTF8.GetBytes(file.Content ?? ""));
        }
    }

    private async Task<UserGistCache> UpdateUserGistsAsync(
        AsyncPipeline pipeline,
        GistPath gistPath,
        BearerToken? token)
    {
        Dictionary<string, UserGistCache> gistCache = RunspaceStorage.GetForRunspace(pipeline.CurrentRunspace).GistCache;
        string userName = gistPath.UserName!;
        string? gistId = gistPath.GistId;

        if (!gistCache.TryGetValue(userName, out UserGistCache? cache))
        {
            cache = new UserGistCache(userName);
            gistCache.Add(userName, cache);
        }

        if (cache.LastUpdate is null || DateTime.UtcNow - cache.LastUpdate.Value >= TimeSpan.FromSeconds(30))
        {
            await foreach (Gist[] gists in GitHubClient.GetGistsforUserAsync(
                userName,
                since: cache.LastUpdate,
                token: token,
                pipeline: pipeline).WithCancellation(pipeline.CancelToken).ConfigureAwait(false))
            {
                foreach (Gist gist in gists)
                {
                    cache.Gists[gist.Id] = gist;
                }
            }
            cache.LastUpdate = DateTime.UtcNow;
        }

        // A secret gist may not be returned if we are not authenticated or we
        // are not the user the gist is for. Try an explicit check and add that
        // to the cache.
        if (gistId is not null && !cache.Gists.ContainsKey(gistId))
        {
            Gist? gist = await GitHubClient.GetGistAsync(
                gistId,
                token: token,
                cancellationToken: pipeline.CancelToken).ConfigureAwait(false);
            if (gist is not null)
            {
                cache.UpdateGistCache(gist);
            }
        }

        return cache;
    }
}
