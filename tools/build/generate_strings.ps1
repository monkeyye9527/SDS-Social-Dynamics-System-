param(
    [string]$ProjectDir = $PSScriptRoot + "\..\.."
)

$stringsDir = Join-Path $ProjectDir "strings"
$poFile = Join-Path $stringsDir "strings.po"

if (-not (Test-Path $stringsDir)) {
    New-Item -ItemType Directory -Path $stringsDir -Force | Out-Null
}

if (-not (Test-Path $poFile)) {
    @"
msgid ""
msgstr ""
"Project-Id-Version: Social Dynamics System (SDS) 3.0\n"
"Content-Type: text/plain; charset=UTF-8\n"

msgid "ONIModPack.SocialInteraction"
msgstr "Social Interaction"

msgid "ONIModPack.CommunityCenter"
msgstr "Community Center"
"@ | Set-Content -Path $poFile -Encoding UTF8
    Write-Host "Generated default strings.po"
} else {
    Write-Host "strings.po already exists, skipping generation"
}
