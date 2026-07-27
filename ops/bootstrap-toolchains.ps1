[CmdletBinding()]
param()

. (Join-Path $PSScriptRoot 'common.ps1')

$repoRoot = Get-MineScapeRoot
$toolsRoot = Join-Path $repoRoot '.tools'
$cacheRoot = Join-Path $toolsRoot 'cache'
New-DirectoryIfMissing $toolsRoot
New-DirectoryIfMissing $cacheRoot

Write-Step 'Installing private .NET SDK 10.0.302'
$dotnetDestination = Join-Path $toolsRoot 'dotnet-10.0.302'
$dotnetExe = Join-Path $dotnetDestination 'dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetExe)) {
    $releaseMetadata = Invoke-RestMethod -Uri 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
    $sdk = $releaseMetadata.releases.sdks | Where-Object { $_.version -eq '10.0.302' } | Select-Object -First 1
    if (-not $sdk) { throw 'Official .NET release metadata does not contain SDK 10.0.302.' }
    $file = $sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } | Select-Object -First 1
    if (-not $file) { throw 'Official .NET metadata has no Windows x64 SDK archive.' }
    $archive = Join-Path $cacheRoot $file.name
    $needsDownload = -not (Test-Path -LiteralPath $archive)
    if (-not $needsDownload) {
        $needsDownload = (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ne $file.hash
    }
    if ($needsDownload) {
        $partial = "$archive.download"
        Invoke-WebRequest -Uri $file.url -OutFile $partial
        $partialHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA512).Hash
        if ($partialHash -ne $file.hash) { throw "Invalid downloaded .NET SDK hash. Expected $($file.hash), got $partialHash." }
        Move-Item -LiteralPath $partial -Destination $archive -Force
    }
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash
    if ($actual -ne $file.hash) { throw "Invalid .NET SDK hash. Expected $($file.hash), got $actual." }
    New-DirectoryIfMissing $dotnetDestination
    Expand-Archive -LiteralPath $archive -DestinationPath $dotnetDestination
}

Write-Step 'Installing private Eclipse Temurin JDK 25.0.3+9'
$javaDestination = Join-Path $toolsRoot 'temurin-25.0.3+9'
$existingJava = Get-ChildItem -LiteralPath $javaDestination -Filter java.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $existingJava) {
    $assetsUri = 'https://api.adoptium.net/v3/assets/feature_releases/25/ga?architecture=x64&heap_size=normal&image_type=jdk&jvm_impl=hotspot&os=windows&page=0&page_size=20&project=jdk&sort_method=DATE&sort_order=DESC&vendor=eclipse'
    $assets = Invoke-RestMethod -Uri $assetsUri
    $release = $assets | Where-Object { $_.release_name -eq 'jdk-25.0.3+9' } | Select-Object -First 1
    if (-not $release) { throw 'Adoptium did not return the pinned 25.0.3+9 release.' }
    $binary = $release.binaries | Where-Object { $_.architecture -eq 'x64' -and $_.image_type -eq 'jdk' -and $_.os -eq 'windows' } | Select-Object -First 1
    if (-not $binary) { throw 'Adoptium release metadata has no matching Windows x64 JDK binary.' }
    $package = $binary.package
    if (-not $package.link -or -not $package.checksum) { throw 'Adoptium did not return a pinned JDK package and checksum.' }
    $archive = Join-Path $cacheRoot $package.name
    $needsDownload = -not (Test-Path -LiteralPath $archive)
    if (-not $needsDownload) {
        $needsDownload = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $package.checksum.ToLowerInvariant()
    }
    if ($needsDownload) {
        $partial = "$archive.download"
        Invoke-WebRequest -Uri $package.link -OutFile $partial
        $partialHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($partialHash -ne $package.checksum.ToLowerInvariant()) { throw "Invalid downloaded JDK hash. Expected $($package.checksum), got $partialHash." }
        Move-Item -LiteralPath $partial -Destination $archive -Force
    }
    $actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $package.checksum.ToLowerInvariant()) { throw "Invalid JDK hash. Expected $($package.checksum), got $actual." }
    New-DirectoryIfMissing $javaDestination
    Expand-Archive -LiteralPath $archive -DestinationPath $javaDestination
}

Write-Step 'Installing private Python 3.13.14'
$pythonDestination = Join-Path $toolsRoot 'python-3.13.14'
$pythonExe = Join-Path $pythonDestination 'python.exe'
if (-not (Test-Path -LiteralPath $pythonExe)) {
    $pythonArchive = Join-Path $cacheRoot 'python-3.13.14-embed-amd64.zip'
    $pythonUrl = 'https://www.python.org/ftp/python/3.13.14/python-3.13.14-embed-amd64.zip'
    $pythonHash = '90b4e5b9898b72d744650524bff92377c367f44bd5fbd09e3148656c080ad907'
    $needsDownload = -not (Test-Path -LiteralPath $pythonArchive)
    if (-not $needsDownload) {
        $needsDownload = (Get-FileHash -LiteralPath $pythonArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $pythonHash
    }
    if ($needsDownload) {
        $partial = "$pythonArchive.download"
        Invoke-WebRequest -Uri $pythonUrl -OutFile $partial
        $partialHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($partialHash -ne $pythonHash) { throw "Invalid Python hash. Expected $pythonHash, got $partialHash." }
        Move-Item -LiteralPath $partial -Destination $pythonArchive -Force
    }
    New-DirectoryIfMissing $pythonDestination
    Expand-Archive -LiteralPath $pythonArchive -DestinationPath $pythonDestination
}

Write-Step 'Installing private Gradle 9.5.1'
$gradleDestination = Join-Path $toolsRoot 'gradle-9.5.1'
$gradleExe = Join-Path $gradleDestination 'gradle-9.5.1\bin\gradle.bat'
if (-not (Test-Path -LiteralPath $gradleExe)) {
    $gradleArchive = Join-Path $cacheRoot 'gradle-9.5.1-bin.zip'
    $gradleUrl = 'https://services.gradle.org/distributions/gradle-9.5.1-bin.zip'
    $gradleHashUrl = 'https://services.gradle.org/distributions/gradle-9.5.1-bin.zip.sha256'
    $gradleHash = (Invoke-RestMethod -Uri $gradleHashUrl).Trim().ToLowerInvariant()
    $needsDownload = -not (Test-Path -LiteralPath $gradleArchive)
    if (-not $needsDownload) {
        $needsDownload = (Get-FileHash -LiteralPath $gradleArchive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $gradleHash
    }
    if ($needsDownload) {
        $partial = "$gradleArchive.download"
        Invoke-WebRequest -Uri $gradleUrl -OutFile $partial
        $partialHash = (Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($partialHash -ne $gradleHash) { throw "Invalid Gradle hash. Expected $gradleHash, got $partialHash." }
        Move-Item -LiteralPath $partial -Destination $gradleArchive -Force
    }
    New-DirectoryIfMissing $gradleDestination
    Expand-Archive -LiteralPath $gradleArchive -DestinationPath $gradleDestination
}
Get-ChildItem -LiteralPath $gradleDestination -File -Recurse | Unblock-File

Initialize-PrivateDotnetEnvironment
$dotnet = Get-PrivateDotnet
$javaHome = Get-PrivateJavaHome
$python = Get-PrivatePython
$gradle = Get-PrivateGradle
$env:JAVA_HOME = $javaHome
$env:GRADLE_USER_HOME = Join-Path $toolsRoot 'gradle-home'
New-DirectoryIfMissing $env:GRADLE_USER_HOME
Write-Host "Private .NET: $dotnet"
Write-Host "Private Java:   $javaHome"
Write-Host "Private Python: $python"
Write-Host "Private Gradle: $gradle"
& $dotnet --version
& (Join-Path $javaHome 'bin\java.exe') -version
& $python --version
& $gradle --version
if ($LASTEXITCODE -ne 0) { throw 'Private Gradle could not start with the pinned Java runtime.' }
