---
external help file: GistHub.dll-Help.xml
Module Name: GistHub
online version: https://www.github.com/jborean93/PowerShell-GistHub/blob/main/docs/en-US/Connect-GistHub.md
schema: 2.0.0
---

# Connect-GistHub

## SYNOPSIS
Sets the default authentication token used by the GistHub provider.

## SYNTAX

### AccessToken (Default)
```
Connect-GistHub [-AccessToken] <SecureString> [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

### OAuthDeviceCode
```
Connect-GistHub [-OAuthDeviceCode] [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Sets the default authentication token used by the GistHub provider through either an explicitly provided personal access token or through OAuth device authorization flow.
The OAuth device authorization flow will request `Read and Write` access for `Gists`.
If using a `Fine-grained` Personal Access Token (`PAT`) it must also have `Read and Write` access for `Gists`.
If using a classic PAT it must have the `gists` scope.

This access token will be used for all the `Gist` provider operations unless an explicit credential is specified.
See [about_GistHubAuthentication](./about_GistHubAuthentication.md) for more information.

## EXAMPLES

### Example 1 - Use OAuth to generate access token
```powershell
PS C:\> Connect-GistHub -OAuthDeviceCode
# Please go to https://github.com/login/device and enter the code: ...
```

Uses OAuth device authorization flow to generate an access token.
This call will print out the URL you should open and the device code to authorize the application.
PowerShell will not continue onto the next step until either the authorization is approved, cancelled, or ctrl+c is pressed.

### Example 2 - Provide PAT interactively
```powershell
PS C:\> Connect-GistHub
```

Will prompt for a PAT interactively through a secure prompt.

### Example 3 - Provide PAT through explicit SecureString
```powershell
PS C:\> $pat = ConvertTo-SecureString ...
PS C:\> Connect-GistHub -AccessToken $pat
```

Provides the PAT through an explicit SecureString object.

## PARAMETERS

### -AccessToken
The classic or fine-grained Personal Access Token (`PAT`) to set as the default authentication token.
This token must have read-write access to access gists.

```yaml
Type: SecureString
Parameter Sets: AccessToken
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OAuthDeviceCode
Use the interactive OAuth device code flow to generate a short lived access token for authentication.
The URL and device code will be written to the PowerShell host and will wait until the OAuth process is done.

```yaml
Type: SwitchParameter
Parameter Sets: OAuthDeviceCode
Aliases:

Required: True
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
New common parameter introduced in PowerShell 7.4.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
## OUTPUTS

### None
## NOTES

## RELATED LINKS
