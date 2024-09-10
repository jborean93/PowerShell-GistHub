# GistHub
[![Test workflow](https://github.com/jborean93/PowerShell-GistHub/workflows/Test%20/badge.svg)](https://github.com/jborean93/PowerShell-GistHub/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/jborean93/PowerShell-GistHub/branch/main/graph/badge.svg?token=b51IOhpLfQ)](https://codecov.io/gh/jborean93/PowerShell-GistHub)
[![PowerShell Gallery](https://img.shields.io/powershellgallery/dt/GistHub.svg)](https://www.powershellgallery.com/packages/GistHub)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/jborean93/PowerShell-GistHub/blob/main/LICENSE)

PowerShell Provider implementation to interact with GitHub Gists.

See [GistHub index](docs/en-US/GistHub.md) and [about_GistHub](./docs/en-US/about_GistHub.md) for more details.

![alt text](.github/GistHub.png)

## Authentication
While the `GistProvider` can get gists without authentication, GitHub limits the number of requests a non-authenticated session to 50 per hour.
This limit will be reached quickly during normal operations due to the redundant calls the PowerShell provider API does.

It is recommended to either set the default authentication token with [Connect-GistHub](./docs/en-US/Connect-GistHub.md) or use the `-Credential` parameter on the provider cmdlets to set the Personal Access Token (`PAT`) the provider should use.

To set the default access token using OAuth interactively you can run:

```powershell
Connect-GistHub -OAuthDeviceCode
```

_Note: Support for the refresh token is not yet implemented._

You can also use [Managing your personal access tokens](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) to generate a PAT that can be used non-interactively.
Both the new fine-grained and class types are supported and must have write access to your gists to be able to edit those gists through the provider.

To set the default access token to the PAT you can run:

```powershell
# This can be used to generate pat.xml
# Read-Host -AsSecureString -Prompt 'PAT' | Export-Clixml pat.xml

$pat = Import-Clixml pat.xml
Connect-GistHub -AccessToken $pat
```

_Note: A serialize SecureString is not secure on non-Windows hosts. Use an alternative solution to retrieve this value._

You can also provide the `$pat` through a drive mapping or through the `-Credential` parameter:

```powershell
# The user can be anything, the PAT is the password
$pat = Get-Credential GitHubPAT
New-PSDrive -Name AuthGist -PSProvider GistHub -Root MyGHUser -Credential $pat
Get-Content AuthGist:0952263a902b8008cda506752a2f0a49

# Can also be used directly on the cmdlet
Get-Content Gist:MyGHUser/0952263a902b8008cda506752a2f0a49 -Credential $pat
```

See [about_GistHubAuthentication](./docs/en-US/about_GistHubAuthentication.md) for more details.

## Examples
A gist have 3 components to the path:

```
Gist:$UserName/$GistId/$FileName
```

The `$UserName` is the GitHub username the gist is for, the `$GistId` is the gist hash identifier, and the `$FileName` is the filename in the gist itself.
In the majority of cases the `$UserName` is required, `$GistId` is required when interactive with a specific gist, and the `$FileName` is typically required when dealing with the contents of a gist.

The `Gist` drive is automatically created by this provider and can be used to access gists for any GitHub user.

### Get Git Content
To get the contents of a gist you can use [Get-Content](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-content?view=powershell-7.4):

```powershell
# Gets the content of 'File.txt' in the gist
Get-Content Gist:GHUser/0952263a902b8008cda506752a2f0a49/File.txt

# Gets the content of the only file in the gist
# This will fail if the gist contains multiple files
Get-Content Gist:GHUser/0952263a902b8008cda506752a2f0a49

# Gets the contents of all files in the gist
Get-Item Gist:jborean93/0952263a902b8008cda506752a2f0a49
```

### Set Gist Content
To set the contents of a gist you can use [Set-Content](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/set-content?view=powershell-7.4):

```powershell
Set-Content Gist:GHUser/42ed104ab6e92aa253abd70830815a97/File.txt NewContent
```

Unlike `Get-Content`, you must provide the file name of the gist to set even if there is only one file in the gist.
Use `(Get-Item Gist:/GHUser/$GistId).Files` to enumerate the files in a gist.
It is not possible to set the gist to an empty or whitespace only string.

### Get a Gist
To get a gist or files in a gist you can use [Get-Item](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-item?view=powershell-7.4):

```powershell
# Get Gist information
Get-Item Gist:GHUser/0952263a902b8008cda506752a2f0a49

# Get Gist file information
Get-Item Gist:GHUser/0952263a902b8008cda506752a2f0a49/File.txt
```

The gist information returns properties like `Id`, `Description`, `Files`, `Url`, `GitUrl` that can be used to interact with the gist outside of the provide.
The gist file information returns properties like `FileName`, `RawUrl`, and `Length` properties about the file itself.

### Enumerate Gists
You can use [Get-ChildItem](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/get-childitem?view=powershell-7.4) to enumerate the gists of a user or the files in a gist:

```powershell
# Enumerates the gists of GHUser
Get-ChildItem Gist:GHUser

# Enumerate the files of a specific gist
Get-ChildItem Gist:GHUser/42ed104ab6e92aa253abd70830815a97

# Get a gist that has a filename matching the file pattern
Get-ChildItem Gist:GHUser | Where-Object Files -like '*Session*'
```

Avoid using wildcards in the path, while it may work it is highly inefficient compared to filtering aftering getting each item with `Where-Object`.

### Remove Gist
To remove a gist or gist file you can use [Remove-Item](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/remove-item?view=powershell-7.4):

```powershell
# Remove a gist file
Remove-Item Gist:GHUser/0952263a902b8008cda506752a2f0a49/File.txt

# Remove gist and all its files
Remove-Item Gist:GHUser/0952263a902b8008cda506752a2f0a49 -Recurse
```

The `-Recurse` switch is required to remove a gist that contains files.
Without the switch, PowerShell will prompt for confirmation before removing the gist.

### Create Gist
You can use the [New-Item](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/new-item?view=powershell-7.4) cmdlet to create a gist and/or file in a gist.

```powershell
# Creates a new gist with the file 'File.txt'
# The output object contains the gist id and other information
New-Item Gist:/GHUser/File.txt -Value foo

# Creates a new file 'File.txt' in the gist specified.
New-Item Gist:GHUser/0952263a902b8008cda506752a2f0a49/File.txt -Value bar
```

By default a gist is marked as secret but can be public by adding the `-Public` parameter.
The `-Description` parameter can be used to set the description of the gist being created.
The `-Force` parameter is required to change the description or overwrite an existing file.

It is also possible to use [Set-Content](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/set-content?view=powershell-7.4) to create a new file in an existing gist.

### Custom Gist Drive
It is possible to create a PSDrive for a specific GitHub user under a custom drive prefix using [New-PSDrive](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.management/new-psdrive?view=powershell-7.4):

```powershell
New-PSDrive -Name UserGist -PSProvider GistHub -Root MyGHUser

# Anything under UserGist: is for the user MyGHUser. This is the equivalent to
# Gist:MyGHUser/0952263a902b8008cda506752a2f0a49
Get-Item UserGist:0952263a902b8008cda506752a2f0a49
```

## Troubleshooting
The PowerShell provider is a complex API so there is bound to be problems.
A simple troubleshooting trick is to run the cmdlet with `-Verbose` to see each provider call used and the HTTP APIs calls during each operation.
It is recommended to avoid wildcard pattern matching in the `-Path` when using the provider cmdlets as they are less efficient than filtering with `Where-Object` in the pipeline.

As a lot of the results are cached to improve the speed of the provider it may be required to clear the cache.
Use [Reset-GistHub](./docs/en-US/Reset-GistHub.md) to reset the cache and the default authentication token if set.

## Requirements
These cmdlets have the following requirements

* PowerShell v5.1 or newer

## Installing
The easiest way to install this module is through [PowerShellGet](https://docs.microsoft.com/en-us/powershell/gallery/overview).

You can install this module by running either of the following `Install-PSResource` or `Install-Module` command.

```powershell
# Install for only the current user
Install-PSResource -Name GistHub -Scope CurrentUser
Install-Module -Name GistHub -Scope CurrentUser

# Install for all users
Install-PSResource -Name GistHub -Scope AllUsers
Install-Module -Name GistHub -Scope AllUsers
```

The `Install-PSResource` cmdlet is part of the new `PSResourceGet` module from Microsoft available in newer versions while `Install-Module` is present on older systems.

## Contributing
Contributing is quite easy, fork this repo and submit a pull request with the changes.
To build this module run `.\build.ps1 -Task Build` in PowerShell.
To test a build run `.\build.ps1 -Task Test` in PowerShell.
This script will ensure all dependencies are installed before running the test suite.
