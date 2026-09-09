param([string] $ReleaseVersion)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    [xml] $props = Get-Content Directory.Build.props
    $major = [string] $props.Project.PropertyGroup.UmbracoMajorVersion

    if ($ReleaseVersion) {
        $version = $ReleaseVersion -replace '^v', ''
        if ($version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$') {
            throw "Tag $ReleaseVersion is not a valid NuGet/SemVer version."
        }
    } else {
        $version = '0.0.0-dev'
    }
    if ($version.Split('.')[0] -ne $major) { throw 'Package and Umbraco majors differ.' }
    [xml] $project = Get-Content src/Umbraco.Community.RazorSearch/Umbraco.Community.RazorSearch.csproj
    $cms = $project.SelectSingleNode('/Project/ItemGroup/PackageReference[@Include="Umbraco.Cms"]')
    if (-not $cms.Version.StartsWith("[$major.")) { throw 'CMS dependency does not match the release major.' }

    Push-Location src/Umbraco.Community.RazorSearch/Client
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }
        npm run check
        if ($LASTEXITCODE -ne 0) { throw 'Client type checking failed.' }
        $env:RELEASE_VERSION = $version
        npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }
    } finally { Pop-Location }

    dotnet build Umbraco.Community.RazorSearch.slnx -c Release -p:Version=$version
    if ($LASTEXITCODE -ne 0) { throw 'Solution build failed.' }
    dotnet test Umbraco.Community.RazorSearch.slnx -c Release --no-build --logger trx
    if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
    dotnet publish src/Umbraco.Community.RazorSearch.Demo/Umbraco.Community.RazorSearch.Demo.csproj -c Release --no-build -p:Version=$version -o artifacts/demo
    if ($LASTEXITCODE -ne 0) { throw 'Demo publish failed.' }
    foreach ($package in @('Umbraco.Community.RazorSearch', 'Umbraco.Community.RazorSearch.Examine')) {
        dotnet pack "src/$package/$package.csproj" -c Release --no-build -p:Version=$version -p:PackageVersion=$version -o artifacts/packages
        if ($LASTEXITCODE -ne 0) { throw "Pack failed for $package." }
    }
    & "$PSScriptRoot/VerifyPackages.ps1" -Version $version
    & "$PSScriptRoot/Test-PackageSchema.ps1" -PackageDirectory artifacts/packages -Version $version
} finally { Pop-Location }
