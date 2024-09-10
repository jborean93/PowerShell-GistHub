using namespace System.IO
using namespace System.Management.Automation
using namespace System.Reflection

$importModule = Get-Command -Name Import-Module -Module Microsoft.PowerShell.Core
$moduleName = [Path]::GetFileNameWithoutExtension($PSCommandPath)
$isReload = $true

if ($IsCoreClr) {
    if (-not ('GistHub.Shared.LoadContext' -as [type])) {
        $isReload = $false
        Add-Type -Path ([Path]::Combine($PSScriptRoot, 'bin', 'net6.0', "$moduleName.Shared.dll"))
    }

    $mainModule = [GistHub.Shared.LoadContext]::Initialize()
    $innerMod = &$importModule -Assembly $mainModule -PassThru
}
else {
    $innerMod = if ('GistHub.GistProvider' -as [type]) {
        $modAssembly = [GistHub.GistProvider].Assembly
        &$importModule -Assembly $modAssembly -Force -PassThru
    }
    else {
        $isReload = $false
        $modPath = [Path]::Combine($PSScriptRoot, 'bin', 'net472', "$moduleName.dll")
        &$importModule -Name $modPath -ErrorAction Stop -PassThru
    }
}

$setNamesFunc = [PSModuleInfo].GetMethod(
    'SetName',
    [BindingFlags]'Instance, NonPublic')
$setNamesFunc.Invoke($innerMod, @($moduleName))

if ($isReload) {
    # Bug in pwsh, Import-Module in an assembly will pick up a cached instance
    # and not call the same path to set the nested module's cmdlets to the
    # current module scope. This is only technically needed if someone is
    # calling 'Import-Module -Name $module -Force' a second time. The first
    # import is still fine.
    # https://github.com/PowerShell/PowerShell/issues/20710
    $addExportedCmdlet = [PSModuleInfo].GetMethod(
        'AddExportedCmdlet',
        [BindingFlags]'Instance, NonPublic'
    )
    foreach ($cmd in $innerMod.ExportedCmdlets.Values) {
        $addExportedCmdlet.Invoke($ExecutionContext.SessionState.Module, @(, $cmd))
    }
}
