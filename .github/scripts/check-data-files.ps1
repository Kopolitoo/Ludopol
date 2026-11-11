# Checks the language files read by Ludopol at startup (same check as Prunel).
#
# Every Languages/*.json file must be valid JSON, with exactly the same keys as
# Languages/en.json (the reference language) and the same {0}, {1}... in each text.
#
# Used by the automatic build (.github/workflows/build.yml). Can also be run by hand
# from the repository folder: pwsh .github/scripts/check-data-files.ps1

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$errors = 0

function Report([string]$file, [string]$message) {
    Write-Host "::error file=$file::$message"
    $script:errors++
}

function Read-Json([string]$path, [string]$name) {
    try {
        return Get-Content -Raw -Encoding utf8 $path | ConvertFrom-Json -AsHashtable
    } catch {
        Report $name "Invalid JSON: $($_.Exception.Message)"
        return $null
    }
}

function Get-Placeholders([string]$text) {
    return ([regex]::Matches($text, '\{\d+') | ForEach-Object { $_.Value } | Sort-Object -Unique) -join ','
}

$reference = Read-Json (Join-Path $root 'Languages/en.json') 'Languages/en.json'
if ($null -eq $reference) { exit 1 }

foreach ($file in Get-ChildItem (Join-Path $root 'Languages') -Filter '*.json') {
    $name = "Languages/$($file.Name)"
    $texts = Read-Json $file.FullName $name
    if ($null -eq $texts) { continue }
    $before = $errors

    foreach ($key in $reference.Keys) {
        if (-not $texts.ContainsKey($key)) {
            Report $name "Missing key: $key"
        } elseif ((Get-Placeholders $texts[$key]) -ne (Get-Placeholders $reference[$key])) {
            Report $name "Placeholders differ from en.json for key: $key"
        }
    }
    foreach ($key in $texts.Keys) {
        if (-not $reference.ContainsKey($key)) { Report $name "Key not in en.json: $key" }
    }

    if ($errors -eq $before) { Write-Host "OK  $name ($($texts.Count) keys)" }
}

if ($errors -gt 0) {
    Write-Host "$errors problem(s) found."
    exit 1
}
Write-Host 'All language files are OK.'
