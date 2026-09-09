param([Parameter(Mandatory)][string] $Version)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packageRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'artifacts/packages'
foreach ($id in @('Umbraco.Community.RazorSearch', 'Umbraco.Community.RazorSearch.Examine')) {
    $zip = [IO.Compression.ZipFile]::OpenRead((Join-Path $packageRoot "$id.$Version.nupkg"))
    try {
        foreach ($required in @('icon.png', 'README.md', "$id.nuspec", "lib/net10.0/$id.dll")) {
            if (-not $zip.GetEntry($required)) { throw "$id lacks $required" }
        }
        if ($id -eq 'Umbraco.Community.RazorSearch') {
            foreach ($required in @('appsettings-schema.Umbraco.Community.RazorSearch.json', 'buildTransitive/Umbraco.Community.RazorSearch.props')) {
                if (-not $zip.GetEntry($required)) { throw "$id lacks $required" }
            }
            if (-not @($zip.Entries | Where-Object FullName -Like '*App_Plugins*/umbraco-package.json').Count) {
                throw 'The core package lacks its backoffice manifest.'
            }
            if (-not @($zip.Entries | Where-Object FullName -Like '*App_Plugins*/*.js').Count) {
                throw 'The core package lacks built backoffice JavaScript.'
            }
            $manifestEntry = $zip.Entries | Where-Object FullName -Like '*App_Plugins*/umbraco-package.json' | Select-Object -First 1
            $reader = [IO.StreamReader]::new($manifestEntry.Open())
            try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json } finally { $reader.Dispose() }
            if ($manifest.version -ne $Version) { throw 'The packaged backoffice manifest version differs from NuGet.' }
        }
        Write-Host "Verified $id $Version"
    } finally { $zip.Dispose() }
}
