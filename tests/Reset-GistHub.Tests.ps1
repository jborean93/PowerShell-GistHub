. (Join-Path $PSScriptRoot common.ps1)

Describe "Reset-GistHub test" {
    It "Resets the cache" {
        Reset-GistHub
    }
}
