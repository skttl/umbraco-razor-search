param(
    [Parameter(Mandatory)][string] $PackageDirectory,
    [Parameter(Mandatory)][string] $Version,
    [switch] $KeepArtifacts
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$packagePath = (Resolve-Path -LiteralPath $PackageDirectory).Path
$tempParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$probeRoot = Join-Path $tempParent ("razorsearch-schema-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probeRoot | Out-Null

try {
    $schemaName = 'appsettings-schema.Umbraco.Community.RazorSearch.json'
    foreach ($kind in @('direct', 'companion')) {
        $directory = Join-Path $probeRoot $kind
        New-Item -ItemType Directory -Path $directory | Out-Null
        $package = if ($kind -eq 'direct') { 'Umbraco.Community.RazorSearch' } else { 'Umbraco.Community.RazorSearch.Examine' }
        $project = Join-Path $directory 'Probe.csproj'
        @"
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><PackageReference Include="$package" Version="$Version" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project
        'var builder = WebApplication.CreateBuilder(args); var app = builder.Build(); app.MapGet("/", () => "Schema installation probe"); app.Run();' | Set-Content -LiteralPath (Join-Path $directory 'Program.cs')
        $escapedSource = [Security.SecurityElement]::Escape($packagePath)
        @"
<configuration><packageSources><clear/><add key="local" value="$escapedSource"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources></configuration>
"@ | Set-Content -LiteralPath (Join-Path $directory 'NuGet.Config')
        # An isolated package cache ensures the tested nupkg is this build, even when the version already exists locally.
        dotnet restore $project --packages (Join-Path $probeRoot 'packages') --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed for $kind." }
        foreach ($build in 1..2) {
            dotnet build $project --no-restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Consumer build $build failed for $kind." }
            $schemaFile = Join-Path $directory $schemaName
            if (-not (Test-Path -LiteralPath $schemaFile)) { throw "Schema not installed for $kind." }
            $refs = (Get-Content -LiteralPath (Join-Path $directory 'appsettings-schema.json') -Raw | ConvertFrom-Json).allOf
            if (@($refs | Where-Object { $_.'$ref' -eq "$schemaName#" }).Count -ne 1) { throw "Schema reference missing or duplicated for $kind." }
            $schema = Get-Content -LiteralPath $schemaFile -Raw | ConvertFrom-Json -AsHashtable
            if (-not $schema.properties.ContainsKey('Umbraco') -or -not $schema.definitions.UmbracoDefinition.properties.ContainsKey('Community') -or -not $schema.definitions.CommunityDefinition.properties.ContainsKey('RazorSearch')) { throw 'Schema configuration path is incorrect.' }
            if ($schema.definitions.RazorSearchRenderQueueOptions.properties.Capacity.type -ne 'integer') { throw 'Schema lost the Capacity type.' }
            if ($schema.definitions.RazorSearchSnapshotExtractionOptions.properties.BodySources.type -ne 'array') { throw 'Schema lost the BodySources type.' }
            if ($schema.definitions.RazorSearchOptions.properties.DefaultRenderer.ContainsKey('enum')) { throw 'Schema restricts custom renderer names.' }
            if ($schema.definitions.RazorSearchOptions.additionalProperties -ne $false) { throw 'Schema permits unknown RazorSearch options.' }
            if ($schema.definitions.RazorSearchSnapshotSourceDefinition.oneOf.Count -ne 2) { throw 'Schema lost the selector/property source alternatives.' }
        }
        dotnet publish $project --no-restore --verbosity quiet -o (Join-Path $directory 'published')
        if ($LASTEXITCODE -ne 0) { throw "Consumer publish failed for $kind." }
        Write-Host "$package ${Version}: schema installed, referenced once after repeated build, consumer publishes."
    }
    Write-Host "Schema installation checks passed. Artifacts: $probeRoot"
}
finally {
    if (-not $KeepArtifacts) {
        $resolved = [IO.Path]::GetFullPath($probeRoot)
        if (-not $resolved.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notmatch '^razorsearch-schema-[a-f0-9]{32}$') { throw 'Refusing to remove an unexpected probe directory.' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

