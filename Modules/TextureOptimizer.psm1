Set-StrictMode -Version Latest

function Get-RimForgeTextureSafeProperty {
    [CmdletBinding()]
    param(
        [AllowNull()]$InputObject,
        [Parameter(Mandatory)][string]$PropertyName,
        $DefaultValue = $null
    )

    if ($null -eq $InputObject) { return $DefaultValue }

    if ($InputObject.PSObject.Properties.Name -contains $PropertyName) {
        return $InputObject.$PropertyName
    }

    return $DefaultValue
}

function Import-RimForgeTextureRules {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Texture rules file not found: $Path"
    }

    try {
        $data = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Texture rules JSON is invalid: $($_.Exception.Message)"
    }

    if ([int]$data.DimensionMultiple -lt 1) {
        throw "DimensionMultiple must be at least 1."
    }

    if ([string]$data.CompressionFormat -ne "BC7_UNORM") {
        throw "RimForge texture conversion currently requires BC7_UNORM."
    }

    return $data
}

function Resolve-RimForgeTexturePath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [Parameter(Mandatory)][string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $ProjectRoot $Path))
}

function Test-RimForgePortableExecutable {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            IsValid = $false
            Architecture = $null
            Error = "File does not exist."
        }
    }

    try {
        $stream = [System.IO.File]::OpenRead($Path)

        try {
            $reader = New-Object System.IO.BinaryReader($stream)

            if ($stream.Length -lt 64) {
                throw "File is too small to be a valid Windows executable."
            }

            if ($reader.ReadUInt16() -ne 0x5A4D) {
                throw "Missing MZ executable header."
            }

            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()

            if ($peOffset -lt 0 -or ($peOffset + 6) -gt $stream.Length) {
                throw "Invalid PE header offset."
            }

            $stream.Position = $peOffset

            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw "Missing PE signature."
            }

            $machine = $reader.ReadUInt16()
            $architecture = switch ($machine) {
                0x014c { "x86" }
                0x8664 { "x64" }
                0xAA64 { "arm64" }
                default { "Unknown (0x{0:X4})" -f $machine }
            }

            return [PSCustomObject]@{
                IsValid = $true
                Architecture = $architecture
                Error = $null
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return [PSCustomObject]@{
            IsValid = $false
            Architecture = $null
            Error = $_.Exception.Message
        }
    }
}

function Test-RimForgeTexconvExecutable {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [int]$TimeoutSeconds = 10
    )

    $pe = Test-RimForgePortableExecutable -Path $Path

    if (-not $pe.IsValid) {
        return [PSCustomObject]@{
            IsInstalled = $true
            IsValid = $false
            Path = $Path
            Architecture = $pe.Architecture
            VersionText = $null
            Error = $pe.Error
        }
    }

    $stdoutPath = Join-Path ([System.IO.Path]::GetTempPath()) (
        "rimforge-texconv-{0}.out" -f [guid]::NewGuid().ToString("N")
    )
    $stderrPath = Join-Path ([System.IO.Path]::GetTempPath()) (
        "rimforge-texconv-{0}.err" -f [guid]::NewGuid().ToString("N")
    )

    try {
        $process = Start-Process `
            -FilePath $Path `
            -ArgumentList @("--version") `
            -PassThru `
            -NoNewWindow `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath

        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            try { $process.Kill() } catch {}

            return [PSCustomObject]@{
                IsInstalled = $true
                IsValid = $false
                Path = $Path
                Architecture = $pe.Architecture
                VersionText = $null
                Error = "texconv.exe validation timed out."
            }
        }

        $stdout = if (Test-Path -LiteralPath $stdoutPath) {
            Get-Content -LiteralPath $stdoutPath -Raw
        } else { "" }

        $stderr = if (Test-Path -LiteralPath $stderrPath) {
            Get-Content -LiteralPath $stderrPath -Raw
        } else { "" }

        $combined = @($stdout, $stderr) -join "`n"

        return [PSCustomObject]@{
            IsInstalled = $true
            IsValid = ($process.ExitCode -eq 0 -or -not [string]::IsNullOrWhiteSpace($combined))
            Path = $Path
            Architecture = $pe.Architecture
            VersionText = $combined.Trim()
            Error = if (
                $process.ExitCode -ne 0 -and
                [string]::IsNullOrWhiteSpace($combined)
            ) {
                "texconv.exe exited with code $($process.ExitCode)."
            } else { $null }
        }
    }
    catch {
        return [PSCustomObject]@{
            IsInstalled = $true
            IsValid = $false
            Path = $Path
            Architecture = $pe.Architecture
            VersionText = $null
            Error = $_.Exception.Message
        }
    }
    finally {
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-RimForgeTexconvStatus {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [AllowNull()][string]$ConfiguredPath,
        [int]$TimeoutSeconds = 10
    )

    $candidates = @()

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $candidates += [PSCustomObject]@{
            Source = "Configured"
            Path = Resolve-RimForgeTexturePath `
                -ProjectRoot $ProjectRoot `
                -Path $ConfiguredPath
        }
    }

    $candidates += [PSCustomObject]@{
        Source = "Bundled"
        Path = Join-Path $ProjectRoot "Tools\DirectXTex\texconv.exe"
    }

    $pathCommand = Get-Command "texconv.exe" -ErrorAction SilentlyContinue

    if ($null -ne $pathCommand) {
        $candidates += [PSCustomObject]@{
            Source = "PATH"
            Path = $pathCommand.Source
        }
    }

    $seen = @{}
    $invalidCandidates = @()

    foreach ($candidate in @($candidates)) {
        $candidatePath = [System.IO.Path]::GetFullPath([string]$candidate.Path)
        $key = $candidatePath.ToLowerInvariant()

        if ($seen.ContainsKey($key)) {
            continue
        }

        $seen[$key] = $true

        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            continue
        }

        $validation = Test-RimForgeTexconvExecutable `
            -Path $candidatePath `
            -TimeoutSeconds $TimeoutSeconds

        if ($validation.IsValid) {
            return [PSCustomObject]@{
                IsInstalled = $true
                IsValid = $true
                Source = $candidate.Source
                Path = $candidatePath
                Architecture = $validation.Architecture
                VersionText = $validation.VersionText
                Error = $null
                InvalidCandidates = @($invalidCandidates)
                CanInstall = $false
            }
        }

        $invalidCandidates += [PSCustomObject]@{
            Source = $candidate.Source
            Path = $candidatePath
            Architecture = $validation.Architecture
            Error = $validation.Error
        }
    }

    $firstInvalid = @($invalidCandidates | Select-Object -First 1)

    return [PSCustomObject]@{
        IsInstalled = (@($invalidCandidates).Count -gt 0)
        IsValid = $false
        Source = if (@($firstInvalid).Count -gt 0) {
            $firstInvalid[0].Source
        } else { $null }
        Path = if (@($firstInvalid).Count -gt 0) {
            $firstInvalid[0].Path
        } else { $null }
        Architecture = if (@($firstInvalid).Count -gt 0) {
            $firstInvalid[0].Architecture
        } else { $null }
        VersionText = $null
        Error = if (@($firstInvalid).Count -gt 0) {
            $firstInvalid[0].Error
        } else {
            "texconv.exe was not found."
        }
        InvalidCandidates = @($invalidCandidates)
        CanInstall = ($null -ne (
            Get-Command "winget.exe" -ErrorAction SilentlyContinue
        ))
    }
}


function Request-RimForgeTexconvInstallApproval {
    [CmdletBinding()]
    param(
        [string]$Reason = "texconv is required to convert PNG textures to BC7 DDS files."
    )

    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop

        $message = @(
            $Reason,
            "",
            "RimForge can install Microsoft's DirectXTex Texconv package now using Winget.",
            "",
            "Install texconv?"
        ) -join "`r`n"

        $result = [System.Windows.Forms.MessageBox]::Show(
            $message,
            "RimForge Texture Optimizer",
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question,
            [System.Windows.Forms.MessageBoxDefaultButton]::Button1
        )

        return (
            $result -eq
            [System.Windows.Forms.DialogResult]::Yes
        )
    }
    catch {
        return $false
    }
}

function Install-RimForgeTexconv {
    [CmdletBinding(SupportsShouldProcess)]
    param([switch]$AcceptPackageAgreements)

    $winget = Get-Command "winget.exe" -ErrorAction SilentlyContinue

    if ($null -eq $winget) {
        throw (
            "winget.exe is unavailable. Install texconv manually or place it " +
            "at Tools\DirectXTex\texconv.exe."
        )
    }

    $arguments = @(
        "install",
        "--id", "Microsoft.DirectXTex.Texconv",
        "--exact"
    )

    if ($AcceptPackageAgreements) {
        $arguments += @(
            "--accept-package-agreements",
            "--accept-source-agreements"
        )
    }

    if ($PSCmdlet.ShouldProcess(
        "Microsoft.DirectXTex.Texconv",
        "Install with winget"
    )) {
        $process = Start-Process `
            -FilePath $winget.Source `
            -ArgumentList $arguments `
            -Wait `
            -PassThru `
            -NoNewWindow

        if ($process.ExitCode -ne 0) {
            throw "winget exited with code $($process.ExitCode)."
        }
    }
}

function Resolve-RimForgeTexconvPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ProjectRoot,
        [AllowNull()][string]$ConfiguredPath
    )

    $status = Get-RimForgeTexconvStatus `
        -ProjectRoot $ProjectRoot `
        -ConfiguredPath $ConfiguredPath

    if ($status.IsValid) {
        return [string]$status.Path
    }

    $detectedPath = if (
        [string]::IsNullOrWhiteSpace([string]$status.Path)
    ) {
        "(none)"
    }
    else {
        [string]$status.Path
    }

    throw (
        "texconv is unavailable or invalid.`n" +
        "Detected path: $detectedPath`n" +
        "Reason: $($status.Error)`n" +
        "Place a valid texconv.exe at Tools\DirectXTex\texconv.exe or run:`n" +
        "winget install --id Microsoft.DirectXTex.Texconv --exact"
    )
}

function Test-RimForgeTextureExcludedPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)]$Rules
    )

    $segments = @($RelativePath -split '[\\/]')

    foreach ($segment in @($segments)) {
        if (@($Rules.ExcludedFolders) -contains $segment) {
            return $true
        }
    }

    return $false
}

function Test-RimForgeTextureTargetVersionPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][string]$TargetVersion
    )

    $first = @($RelativePath -split '[\\/]')[0]

    if ($first -match '^(?i)v?(\d+\.\d+)$') {
        return ($Matches[1] -eq $TargetVersion)
    }

    return $true
}

function Test-RimForgeTexturePathPattern {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Value,
        [AllowNull()]$Patterns
    )

    foreach ($pattern in @($Patterns)) {
        if ($Value -match [string]$pattern) {
            return $true
        }
    }

    return $false
}

function Get-RimForgeNearestMultiple {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$Value,
        [Parameter(Mandatory)][int]$Multiple,
        [ValidateSet("Up","Down")][string]$TieRule = "Up"
    )

    if ($Value -le 0) { throw "Image dimensions must be positive." }
    if ($Multiple -le 0) { throw "Multiple must be positive." }

    $lower = [Math]::Floor($Value / [double]$Multiple) * $Multiple
    $upper = [Math]::Ceiling($Value / [double]$Multiple) * $Multiple

    if ($lower -lt $Multiple) { $lower = $Multiple }
    if ($upper -lt $Multiple) { $upper = $Multiple }

    $lowerDistance = [Math]::Abs($Value - $lower)
    $upperDistance = [Math]::Abs($upper - $Value)

    if ($lowerDistance -lt $upperDistance) { return [int]$lower }
    if ($upperDistance -lt $lowerDistance) { return [int]$upper }

    if ($TieRule -eq "Down") { return [int]$lower }
    return [int]$upper
}

function Get-RimForgeTextureGeometry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][int]$OriginalWidth,
        [Parameter(Mandatory)][int]$OriginalHeight,
        [int]$DimensionMultiple = 4,
        [ValidateSet("Up","Down")][string]$TieRule = "Up"
    )

    $canvasWidth = Get-RimForgeNearestMultiple `
        -Value $OriginalWidth `
        -Multiple $DimensionMultiple `
        -TieRule $TieRule

    $canvasHeight = Get-RimForgeNearestMultiple `
        -Value $OriginalHeight `
        -Multiple $DimensionMultiple `
        -TieRule $TieRule

    $scale = [Math]::Min(
        $canvasWidth / [double]$OriginalWidth,
        $canvasHeight / [double]$OriginalHeight
    )

    $scaledWidth = [Math]::Max(
        1,
        [Math]::Min(
            $canvasWidth,
            [int][Math]::Round(
                $OriginalWidth * $scale,
                [MidpointRounding]::AwayFromZero
            )
        )
    )

    $scaledHeight = [Math]::Max(
        1,
        [Math]::Min(
            $canvasHeight,
            [int][Math]::Round(
                $OriginalHeight * $scale,
                [MidpointRounding]::AwayFromZero
            )
        )
    )

    $paddingX = $canvasWidth - $scaledWidth
    $paddingY = $canvasHeight - $scaledHeight

    $left = [int][Math]::Floor($paddingX / 2.0)
    $right = [int]($paddingX - $left)
    $top = [int][Math]::Floor($paddingY / 2.0)
    $bottom = [int]($paddingY - $top)

    return [PSCustomObject][ordered]@{
        OriginalWidth  = $OriginalWidth
        OriginalHeight = $OriginalHeight
        CanvasWidth    = $canvasWidth
        CanvasHeight   = $canvasHeight
        ScaledWidth    = $scaledWidth
        ScaledHeight   = $scaledHeight
        PaddingLeft    = $left
        PaddingRight   = $right
        PaddingTop     = $top
        PaddingBottom  = $bottom
        RequiresCanvas = (
            $canvasWidth -ne $OriginalWidth -or
            $canvasHeight -ne $OriginalHeight
        )
    }
}

function Initialize-RimForgeSystemDrawing {
    [CmdletBinding()]
    param()

    try {
        Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    }
    catch {
        throw (
            "System.Drawing could not be loaded. The texture optimizer " +
            "requires Windows PowerShell or a compatible System.Drawing runtime. " +
            $_.Exception.Message
        )
    }
}

function Get-RimForgePngDimensions {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    Initialize-RimForgeSystemDrawing

    $image = $null

    try {
        $image = [System.Drawing.Image]::FromFile($Path)
        return [PSCustomObject]@{
            Width  = [int]$image.Width
            Height = [int]$image.Height
        }
    }
    finally {
        if ($null -ne $image) { $image.Dispose() }
    }
}

function New-RimForgePaddedPng {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [Parameter(Mandatory)]$Geometry
    )

    Initialize-RimForgeSystemDrawing

    $parent = Split-Path -Parent $DestinationPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    $source = $null
    $canvas = $null
    $graphics = $null

    try {
        $source = [System.Drawing.Image]::FromFile($SourcePath)
        $canvas = New-Object System.Drawing.Bitmap(
            [int]$Geometry.CanvasWidth,
            [int]$Geometry.CanvasHeight,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        )

        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode =
            [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality =
            [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode =
            [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode =
            [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode =
            [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $destinationRectangle = New-Object System.Drawing.Rectangle(
            [int]$Geometry.PaddingLeft,
            [int]$Geometry.PaddingTop,
            [int]$Geometry.ScaledWidth,
            [int]$Geometry.ScaledHeight
        )

        $graphics.DrawImage(
            $source,
            $destinationRectangle,
            0,
            0,
            $source.Width,
            $source.Height,
            [System.Drawing.GraphicsUnit]::Pixel
        )

        $canvas.Save(
            $DestinationPath,
            [System.Drawing.Imaging.ImageFormat]::Png
        )
    }
    finally {
        if ($null -ne $graphics) { $graphics.Dispose() }
        if ($null -ne $canvas) { $canvas.Dispose() }
        if ($null -ne $source) { $source.Dispose() }
    }
}

function Get-RimForgeTextureCache {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Path)

    $lookup = @{}

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $lookup
    }

    try {
        $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json

        foreach ($item in @($document.Items)) {
            $lookup[[string]$item.Path] = $item
        }
    }
    catch {
        return @{}
    }

    return $lookup
}

function Save-RimForgeTextureCache {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][hashtable]$Lookup
    )

    $parent = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }

    [PSCustomObject]@{
        SchemaVersion = 1
        Items = @(
            foreach ($key in @($Lookup.Keys | Sort-Object)) {
                $Lookup[$key]
            }
        )
    } |
        ConvertTo-Json -Depth 8 |
        Set-Content -LiteralPath $Path -Encoding UTF8
}

function Get-RimForgeTextureInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][array]$Mods,
        [Parameter(Mandatory)]$Rules,
        [Parameter(Mandatory)][string]$ProjectRoot,
        [string]$TargetVersion = "1.6",
        [scriptblock]$ProgressCallback,
        [System.Threading.CancellationToken]$CancellationToken = (
            [System.Threading.CancellationToken]::None
        )
    )

    $cacheFolder = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.CacheFolder)

    $cachePath = Join-Path $cacheFolder "TextureInventory.json"
    $cache = Get-RimForgeTextureCache -Path $cachePath
    $updatedCache = @{}
    $items = @()
    $modIndex = 0
    $modTotal = @($Mods).Count

    foreach ($mod in @($Mods)) {
        $CancellationToken.ThrowIfCancellationRequested()
        $modIndex++

        $rootPath = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "RootPath" `
            -DefaultValue "")

        if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
            continue
        }

        $packageId = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "PackageId" `
            -DefaultValue $null)

        if ([string]::IsNullOrWhiteSpace($packageId)) {
            $packageId = "workshop-" + [string](Get-RimForgeTextureSafeProperty `
                -InputObject $mod `
                -PropertyName "WorkshopID" `
                -DefaultValue (Split-Path $rootPath -Leaf))
        }

        $modName = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "Name" `
            -DefaultValue $packageId)

        if ($null -ne $ProgressCallback) {
            & $ProgressCallback ([PSCustomObject]@{
                Phase = "Inventory"
                Current = $modIndex
                Total = $modTotal
                Name = $modName
                Percent = if ($modTotal -gt 0) {
                    [Math]::Floor((($modIndex - 1) / $modTotal) * 100)
                } else { 100 }
            })
        }

        $files = @(
            Get-ChildItem `
                -LiteralPath $rootPath `
                -Recurse `
                -File `
                -Filter "*.png" `
                -ErrorAction SilentlyContinue
        )

        foreach ($file in @($files)) {
            $CancellationToken.ThrowIfCancellationRequested()

            $relative = $file.FullName.Substring($rootPath.Length).TrimStart(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            )

            if (Test-RimForgeTextureExcludedPath `
                -RelativePath $relative `
                -Rules $Rules) {
                continue
            }

            if (-not (Test-RimForgeTextureTargetVersionPath `
                -RelativePath $relative `
                -TargetVersion $TargetVersion)) {
                continue
            }

            if ($relative -notmatch '(?i)(^|[\\/])Textures([\\/]|$)') {
                continue
            }

            if (
                [bool]$Rules.SkipUiTextures -and
                (Test-RimForgeTexturePathPattern `
                    -Value $relative `
                    -Patterns $Rules.UiPathPatterns)
            ) {
                continue
            }

            if (
                [bool]$Rules.SkipNormalMaps -and
                (Test-RimForgeTexturePathPattern `
                    -Value $relative `
                    -Patterns $Rules.NormalMapPatterns)
            ) {
                continue
            }

            $signature = "{0}|{1}|{2}" -f
                $file.Length,
                $file.LastWriteTimeUtc.Ticks,
                $TargetVersion

            $cacheKey = $file.FullName.ToLowerInvariant()
            $dimensions = $null

            if (
                $cache.ContainsKey($cacheKey) -and
                [string]$cache[$cacheKey].Signature -eq $signature
            ) {
                $dimensions = [PSCustomObject]@{
                    Width = [int]$cache[$cacheKey].Width
                    Height = [int]$cache[$cacheKey].Height
                }
            }
            else {
                try {
                    $dimensions = Get-RimForgePngDimensions -Path $file.FullName
                }
                catch {
                    $items += [PSCustomObject][ordered]@{
                        PackageId = $packageId
                        ModName = $modName
                        ModRoot = $rootPath
                        SourcePath = $file.FullName
                        RelativePath = $relative
                        Status = "Unreadable"
                        Error = $_.Exception.Message
                    }
                    continue
                }
            }

            $updatedCache[$cacheKey] = [PSCustomObject]@{
                Path = $cacheKey
                Signature = $signature
                Width = $dimensions.Width
                Height = $dimensions.Height
            }

            $geometry = Get-RimForgeTextureGeometry `
                -OriginalWidth $dimensions.Width `
                -OriginalHeight $dimensions.Height `
                -DimensionMultiple ([int]$Rules.DimensionMultiple) `
                -TieRule ([string]$Rules.TieRule)

            $existingDds = [System.IO.Path]::ChangeExtension(
                $file.FullName,
                ".dds"
            )

            $items += [PSCustomObject][ordered]@{
                PackageId = $packageId
                ModName = $modName
                ModRoot = $rootPath
                SourcePath = $file.FullName
                RelativePath = $relative
                ExistingDdsPath = $existingDds
                ExistingDds = (Test-Path -LiteralPath $existingDds -PathType Leaf)
                OriginalWidth = $dimensions.Width
                OriginalHeight = $dimensions.Height
                CanvasWidth = $geometry.CanvasWidth
                CanvasHeight = $geometry.CanvasHeight
                ScaledWidth = $geometry.ScaledWidth
                ScaledHeight = $geometry.ScaledHeight
                PaddingLeft = $geometry.PaddingLeft
                PaddingRight = $geometry.PaddingRight
                PaddingTop = $geometry.PaddingTop
                PaddingBottom = $geometry.PaddingBottom
                RequiresCanvas = $geometry.RequiresCanvas
                Status = "Ready"
                Error = $null
            }
        }
    }

    Save-RimForgeTextureCache -Path $cachePath -Lookup $updatedCache
    return @($items)
}

function New-RimForgeTextureConversionPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][array]$Inventory,
        [Parameter(Mandatory)]$Rules,
        [Parameter(Mandatory)][string]$ProjectRoot,
        [string]$OutputPath
    )

    $stagingRoot = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.StagingFolder)

    $planItems = @()

    foreach ($item in @($Inventory)) {
        if ([string]$item.Status -ne "Ready") {
            $planItems += $item
            continue
        }

        $safePackage = ([string]$item.PackageId) -replace '[^a-zA-Z0-9._-]', "_"
        $relativeDds = [System.IO.Path]::ChangeExtension(
            [string]$item.RelativePath,
            ".dds"
        )

        $stagePath = Join-Path `
            (Join-Path $stagingRoot $safePackage) `
            $relativeDds

        $disposition = "Convert"

        if (
            [bool]$item.ExistingDds -and
            [bool]$Rules.SkipExistingDds -and
            -not [bool]$Rules.OverwriteExistingDds
        ) {
            $disposition = "SkipExistingDds"
        }

        $planItems += [PSCustomObject][ordered]@{
            PackageId = $item.PackageId
            ModName = $item.ModName
            ModRoot = $item.ModRoot
            SourcePath = $item.SourcePath
            RelativePath = $item.RelativePath
            ExistingDdsPath = $item.ExistingDdsPath
            ExistingDds = $item.ExistingDds
            StagePath = $stagePath
            InstallPath = [System.IO.Path]::ChangeExtension(
                [string]$item.SourcePath,
                ".dds"
            )
            OriginalWidth = $item.OriginalWidth
            OriginalHeight = $item.OriginalHeight
            CanvasWidth = $item.CanvasWidth
            CanvasHeight = $item.CanvasHeight
            ScaledWidth = $item.ScaledWidth
            ScaledHeight = $item.ScaledHeight
            PaddingLeft = $item.PaddingLeft
            PaddingRight = $item.PaddingRight
            PaddingTop = $item.PaddingTop
            PaddingBottom = $item.PaddingBottom
            RequiresCanvas = $item.RequiresCanvas
            CompressionFormat = [string]$Rules.CompressionFormat
            GenerateMipmaps = [bool]$Rules.GenerateMipmaps
            Disposition = $disposition
        }
    }

    $plan = [PSCustomObject][ordered]@{
        SchemaVersion = 1
        GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
        TargetVersion = [string]$Rules.TargetVersion
        CompressionFormat = [string]$Rules.CompressionFormat
        DimensionMultiple = [int]$Rules.DimensionMultiple
        PreserveAspectRatio = $true
        PaddingMode = "Transparent"
        Items = @($planItems)
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $parent = Split-Path -Parent $OutputPath
        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force | Out-Null
        }

        $plan |
            ConvertTo-Json -Depth 12 |
            Set-Content -LiteralPath $OutputPath -Encoding UTF8
    }

    return $plan
}


function Get-RimForgeDdsHeaderInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            IsReadable  = $false
            IsDds       = $false
            Width       = $null
            Height      = $null
            MipmapCount = $null
            FourCc      = $null
            DxgiFormat  = $null
            Error       = "DDS file does not exist."
        }
    }

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite
        )

        try {
            if ($stream.Length -lt 128) {
                throw "DDS file is too small to contain a valid header."
            }

            $reader = New-Object System.IO.BinaryReader($stream)
            $magicBytes = $reader.ReadBytes(4)
            $magic = [System.Text.Encoding]::ASCII.GetString($magicBytes)

            if ($magic -ne "DDS ") {
                throw "DDS magic header is missing."
            }

            $stream.Position = 12
            $height = $reader.ReadInt32()
            $width = $reader.ReadInt32()

            $stream.Position = 28
            $mipmapCount = $reader.ReadInt32()

            $stream.Position = 84
            $fourCc = [System.Text.Encoding]::ASCII.GetString(
                $reader.ReadBytes(4)
            )

            $dxgiFormat = $null

            if ($fourCc -eq "DX10" -and $stream.Length -ge 148) {
                $stream.Position = 128
                $dxgiFormat = $reader.ReadInt32()
            }

            if ($width -lt 1 -or $height -lt 1) {
                throw "DDS header contains invalid dimensions."
            }

            return [PSCustomObject]@{
                IsReadable  = $true
                IsDds       = $true
                Width       = [int]$width
                Height      = [int]$height
                MipmapCount = [int]$mipmapCount
                FourCc      = $fourCc
                DxgiFormat  = $dxgiFormat
                Error       = $null
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return [PSCustomObject]@{
            IsReadable  = $false
            IsDds       = $false
            Width       = $null
            Height      = $null
            MipmapCount = $null
            FourCc      = $null
            DxgiFormat  = $null
            Error       = $_.Exception.Message
        }
    }
}

function Get-RimForgeInvalidDdsInventory {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Mods,

        [Parameter(Mandatory)]
        $Rules,

        [Parameter(Mandatory)]
        [string]$ProjectRoot,

        [string]$TargetVersion = "1.6",

        [scriptblock]$ProgressCallback,

        [System.Threading.CancellationToken]$CancellationToken = (
            [System.Threading.CancellationToken]::None
        )
    )

    $multiple = [int](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "DimensionMultiple" `
        -DefaultValue 4)

    if ($multiple -lt 1) {
        $multiple = 4
    }

    $items = @()
    $modIndex = 0
    $modTotal = @($Mods).Count

    foreach ($mod in @($Mods)) {
        $CancellationToken.ThrowIfCancellationRequested()
        $modIndex++

        $rootPath = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "RootPath" `
            -DefaultValue "")

        if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) {
            continue
        }

        $packageId = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "PackageId" `
            -DefaultValue "")

        if ([string]::IsNullOrWhiteSpace($packageId)) {
            $packageId = "workshop-" + [string](Get-RimForgeTextureSafeProperty `
                -InputObject $mod `
                -PropertyName "WorkshopID" `
                -DefaultValue (Split-Path $rootPath -Leaf))
        }

        $modName = [string](Get-RimForgeTextureSafeProperty `
            -InputObject $mod `
            -PropertyName "Name" `
            -DefaultValue $packageId)

        if ($null -ne $ProgressCallback) {
            & $ProgressCallback ([PSCustomObject]@{
                Phase   = "DDS header scan"
                Current = $modIndex
                Total   = $modTotal
                Name    = $modName
                Percent = if ($modTotal -gt 0) {
                    [Math]::Floor((($modIndex - 1) / $modTotal) * 100)
                }
                else {
                    100
                }
            })
        }

        $ddsFiles = @(
            Get-ChildItem `
                -LiteralPath $rootPath `
                -Recurse `
                -File `
                -Filter "*.dds" `
                -ErrorAction SilentlyContinue
        )

        foreach ($file in @($ddsFiles)) {
            $CancellationToken.ThrowIfCancellationRequested()

            $relative = $file.FullName.Substring($rootPath.Length).TrimStart(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar
            )

            if (Test-RimForgeTextureExcludedPath `
                -RelativePath $relative `
                -Rules $Rules) {
                continue
            }

            if (-not (Test-RimForgeTextureTargetVersionPath `
                -RelativePath $relative `
                -TargetVersion $TargetVersion)) {
                continue
            }

            if ($relative -notmatch '(?i)(^|[\\/])Textures([\\/]|$)') {
                continue
            }

            $header = Get-RimForgeDdsHeaderInfo -Path $file.FullName

            if (-not $header.IsReadable) {
                $items += [PSCustomObject][ordered]@{
                    PackageId       = $packageId
                    ModName          = $modName
                    ModRoot          = $rootPath
                    DdsPath          = $file.FullName
                    RelativeDdsPath  = $relative
                    PngPath          = [System.IO.Path]::ChangeExtension(
                        $file.FullName,
                        ".png"
                    )
                    DdsWidth         = $null
                    DdsHeight        = $null
                    IsDimensionValid = $false
                    HasSourcePng     = $false
                    Status           = "UnreadableDds"
                    Error            = $header.Error
                }

                continue
            }

            $validDimensions = (
                ($header.Width % $multiple) -eq 0 -and
                ($header.Height % $multiple) -eq 0
            )

            if ($validDimensions) {
                continue
            }

            $pngPath = [System.IO.Path]::ChangeExtension(
                $file.FullName,
                ".png"
            )

            $hasPng = Test-Path -LiteralPath $pngPath -PathType Leaf

            $items += [PSCustomObject][ordered]@{
                PackageId       = $packageId
                ModName          = $modName
                ModRoot          = $rootPath
                DdsPath          = $file.FullName
                RelativeDdsPath  = $relative
                PngPath          = $pngPath
                DdsWidth         = [int]$header.Width
                DdsHeight        = [int]$header.Height
                MipmapCount      = [int]$header.MipmapCount
                FourCc           = $header.FourCc
                DxgiFormat       = $header.DxgiFormat
                IsDimensionValid = $false
                HasSourcePng     = $hasPng
                Status           = if ($hasPng) {
                    "InvalidDimensions"
                }
                else {
                    "MissingSourcePng"
                }
                Error            = $null
            }
        }
    }

    return @($items)
}

function New-RimForgeInvalidDdsRepairPlan {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [array]$Inventory,

        [Parameter(Mandatory)]
        $Rules,

        [Parameter(Mandatory)]
        [string]$ProjectRoot,

        [string]$OutputPath
    )

    $stagingRoot = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.StagingFolder)

    $items = @()

    foreach ($entry in @($Inventory)) {
        if ([string]$entry.Status -ne "InvalidDimensions") {
            $items += [PSCustomObject][ordered]@{
                PackageId   = $entry.PackageId
                ModName      = $entry.ModName
                SourcePath   = $entry.PngPath
                RelativePath = $entry.RelativeDdsPath
                InstallPath  = $entry.DdsPath
                StagePath    = $null
                Disposition  = [string]$entry.Status
                Status       = [string]$entry.Status
                Error        = $entry.Error
            }

            continue
        }

        try {
            $pngDimensions = Get-RimForgePngDimensions `
                -Path ([string]$entry.PngPath)

            $geometry = Get-RimForgeTextureGeometry `
                -OriginalWidth ([int]$pngDimensions.Width) `
                -OriginalHeight ([int]$pngDimensions.Height) `
                -DimensionMultiple ([int]$Rules.DimensionMultiple) `
                -TieRule ([string]$Rules.TieRule)

            $safePackage = (
                [string]$entry.PackageId
            ) -replace '[^a-zA-Z0-9._-]', "_"

            $stagePath = Join-Path `
                (Join-Path $stagingRoot $safePackage) `
                ([string]$entry.RelativeDdsPath)

            $items += [PSCustomObject][ordered]@{
                PackageId       = $entry.PackageId
                ModName          = $entry.ModName
                ModRoot          = $entry.ModRoot
                SourcePath       = $entry.PngPath
                RelativePath     = $entry.RelativeDdsPath
                ExistingDdsPath  = $entry.DdsPath
                ExistingDds      = $true
                StagePath        = $stagePath
                InstallPath      = $entry.DdsPath
                OriginalWidth    = [int]$pngDimensions.Width
                OriginalHeight   = [int]$pngDimensions.Height
                PreviousDdsWidth = [int]$entry.DdsWidth
                PreviousDdsHeight = [int]$entry.DdsHeight
                CanvasWidth      = [int]$geometry.CanvasWidth
                CanvasHeight     = [int]$geometry.CanvasHeight
                ScaledWidth      = [int]$geometry.ScaledWidth
                ScaledHeight     = [int]$geometry.ScaledHeight
                PaddingLeft      = [int]$geometry.PaddingLeft
                PaddingRight     = [int]$geometry.PaddingRight
                PaddingTop       = [int]$geometry.PaddingTop
                PaddingBottom    = [int]$geometry.PaddingBottom
                RequiresCanvas   = [bool]$geometry.RequiresCanvas
                CompressionFormat = "BC7_UNORM"
                GenerateMipmaps  = [bool]$Rules.GenerateMipmaps
                Disposition      = "Convert"
                RepairReason     = "DDS dimensions are not divisible by four."
            }
        }
        catch {
            $items += [PSCustomObject][ordered]@{
                PackageId   = $entry.PackageId
                ModName      = $entry.ModName
                SourcePath   = $entry.PngPath
                RelativePath = $entry.RelativeDdsPath
                InstallPath  = $entry.DdsPath
                StagePath    = $null
                Disposition  = "UnreadableSourcePng"
                Status       = "UnreadableSourcePng"
                Error        = $_.Exception.Message
            }
        }
    }

    $plan = [PSCustomObject][ordered]@{
        SchemaVersion      = 1
        PlanType           = "RepairInvalidDdsDimensions"
        GeneratedAtUtc     = [DateTime]::UtcNow.ToString("o")
        CompressionFormat  = "BC7_UNORM"
        DimensionMultiple  = [int]$Rules.DimensionMultiple
        PreserveAspectRatio = $true
        PaddingMode        = "Transparent"
        Items              = @($items)
        Summary            = [PSCustomObject][ordered]@{
            Scheduled = @(
                $items |
                Where-Object {
                    $_.PSObject.Properties.Name -contains "Disposition" -and
                    $_.Disposition -eq "Convert"
                }
            ).Count
            MissingSourcePng = @(
                $items |
                Where-Object {
                    $_.PSObject.Properties.Name -contains "Disposition" -and
                    $_.Disposition -eq "MissingSourcePng"
                }
            ).Count
            UnreadableDds = @(
                $items |
                Where-Object {
                    $_.PSObject.Properties.Name -contains "Disposition" -and
                    $_.Disposition -eq "UnreadableDds"
                }
            ).Count
            UnreadableSourcePng = @(
                $items |
                Where-Object {
                    $_.PSObject.Properties.Name -contains "Disposition" -and
                    $_.Disposition -eq "UnreadableSourcePng"
                }
            ).Count
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $parent = Split-Path -Parent $OutputPath

        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force |
                Out-Null
        }

        $plan |
            ConvertTo-Json -Depth 14 |
            Set-Content `
                -LiteralPath $OutputPath `
                -Encoding UTF8
    }

    return $plan
}

function Test-RimForgeDdsFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][int]$ExpectedWidth,
        [Parameter(Mandatory)][int]$ExpectedHeight,
        [bool]$ExpectMipmaps = $true
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return [PSCustomObject]@{
            IsValid = $false
            Error = "DDS file does not exist."
        }
    }

    $bytes = [System.IO.File]::ReadAllBytes($Path)

    if ($bytes.Length -lt 148) {
        return [PSCustomObject]@{
            IsValid = $false
            Error = "DDS file is too small."
        }
    }

    $magic = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)

    if ($magic -ne "DDS ") {
        return [PSCustomObject]@{
            IsValid = $false
            Error = "DDS magic header is missing."
        }
    }

    $height = [BitConverter]::ToInt32($bytes, 12)
    $width = [BitConverter]::ToInt32($bytes, 16)
    $mipmapCount = [BitConverter]::ToInt32($bytes, 28)
    $fourCc = [System.Text.Encoding]::ASCII.GetString($bytes, 84, 4)
    $dxgiFormat = if ($fourCc -eq "DX10") {
        [BitConverter]::ToInt32($bytes, 128)
    } else { -1 }

    $errors = @()

    if ($width -ne $ExpectedWidth -or $height -ne $ExpectedHeight) {
        $errors += (
            "Expected {0}x{1}, found {2}x{3}." -f
            $ExpectedWidth, $ExpectedHeight, $width, $height
        )
    }

    if ($fourCc -ne "DX10" -or $dxgiFormat -ne 98) {
        $errors += (
            "Expected BC7_UNORM DX10 format (98), found FourCC '{0}', DXGI {1}." -f
            $fourCc, $dxgiFormat
        )
    }

    if ($ExpectMipmaps -and [Math]::Max($width, $height) -gt 1 -and $mipmapCount -le 1) {
        $errors += "Mipmaps were expected but the DDS reports one or fewer levels."
    }

    return [PSCustomObject]@{
        IsValid = (@($errors).Count -eq 0)
        Width = $width
        Height = $height
        MipmapCount = $mipmapCount
        FourCc = $fourCc
        DxgiFormat = $dxgiFormat
        Errors = @($errors)
        Error = if (@($errors).Count -gt 0) {
            $errors -join " "
        } else { $null }
    }
}


function Get-RimForgeTextureFileSignature {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        $Item,

        [Parameter(Mandatory)]
        $Rules,

        [Parameter(Mandatory)]
        [string]$TexconvPath
    )

    $file = Get-Item -LiteralPath $Path -ErrorAction Stop
    $tool = Get-Item -LiteralPath $TexconvPath -ErrorAction Stop

    return (
        "{0}|{1}|{2}x{3}|{4}x{5}|{6}|mips={7}|tool={8}:{9}" -f
        $file.Length,
        $file.LastWriteTimeUtc.Ticks,
        [int]$Item.OriginalWidth,
        [int]$Item.OriginalHeight,
        [int]$Item.CanvasWidth,
        [int]$Item.CanvasHeight,
        [string]$Rules.CompressionFormat,
        [bool]$Item.GenerateMipmaps,
        $tool.Length,
        $tool.LastWriteTimeUtc.Ticks
    )
}

function Get-RimForgeTextureConversionCache {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    $lookup = @{}

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $lookup
    }

    try {
        $document = Get-Content -LiteralPath $Path -Raw |
            ConvertFrom-Json

        foreach ($item in @($document.Items)) {
            if (
                $null -ne $item -and
                -not [string]::IsNullOrWhiteSpace([string]$item.SourcePath)
            ) {
                $lookup[[string]$item.SourcePath.ToLowerInvariant()] = $item
            }
        }
    }
    catch {
        return @{}
    }

    return $lookup
}

function Save-RimForgeTextureConversionCache {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [hashtable]$Lookup
    )

    $parent = Split-Path -Parent $Path

    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        New-Item -ItemType Directory -Path $parent -Force |
            Out-Null
    }

    $temporaryPath = "{0}.{1}.tmp" -f
        $Path,
        [guid]::NewGuid().ToString("N")

    try {
        [PSCustomObject]@{
            SchemaVersion = 1
            UpdatedAtUtc  = [DateTime]::UtcNow.ToString("o")
            Items         = @(
                foreach ($key in @($Lookup.Keys | Sort-Object)) {
                    $Lookup[$key]
                }
            )
        } |
            ConvertTo-Json -Depth 10 |
            Set-Content `
                -LiteralPath $temporaryPath `
                -Encoding UTF8

        Move-Item `
            -LiteralPath $temporaryPath `
            -Destination $Path `
            -Force
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
            Remove-Item `
                -LiteralPath $temporaryPath `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }
}

function Test-RimForgeCachedTextureOutput {
    [CmdletBinding()]
    param(
        [AllowNull()]
        $CacheEntry,

        [Parameter(Mandatory)]
        [string]$ExpectedSignature,

        [Parameter(Mandatory)]
        [string]$ExpectedPath,

        [bool]$ValidateHeader = $false,

        [Parameter(Mandatory)]
        [int]$ExpectedWidth,

        [Parameter(Mandatory)]
        [int]$ExpectedHeight,

        [bool]$ExpectMipmaps = $true
    )

    if ($null -eq $CacheEntry) {
        return $false
    }

    if ([string]$CacheEntry.Signature -ne $ExpectedSignature) {
        return $false
    }

    if (-not (Test-Path -LiteralPath $ExpectedPath -PathType Leaf)) {
        return $false
    }

    $file = Get-Item -LiteralPath $ExpectedPath -ErrorAction SilentlyContinue

    if ($null -eq $file) {
        return $false
    }

    if (
        $CacheEntry.PSObject.Properties.Name -contains "OutputLength" -and
        [long]$CacheEntry.OutputLength -ne [long]$file.Length
    ) {
        return $false
    }

    if ($ValidateHeader) {
        $validation = Test-RimForgeDdsFile `
            -Path $ExpectedPath `
            -ExpectedWidth $ExpectedWidth `
            -ExpectedHeight $ExpectedHeight `
            -ExpectMipmaps $ExpectMipmaps

        return [bool]$validation.IsValid
    }

    return $true
}

function New-RimForgeFastPreparedInput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Item,

        [Parameter(Mandatory)]
        [string]$PreparedFolder
    )

    if (-not (Test-Path -LiteralPath $PreparedFolder -PathType Container)) {
        New-Item -ItemType Directory -Path $PreparedFolder -Force |
            Out-Null
    }

    $token = [guid]::NewGuid().ToString("N")
    $preparedPath = Join-Path $PreparedFolder ("{0}.png" -f $token)

    if ([bool]$Item.RequiresCanvas) {
        New-RimForgePaddedPng `
            -SourcePath ([string]$Item.SourcePath) `
            -DestinationPath $preparedPath `
            -Geometry $Item

        return [PSCustomObject]@{
            Token        = $token
            PreparedPath = $preparedPath
            Preparation  = "ResizeAndPad"
        }
    }

    # Avoid decoding/re-encoding images whose dimensions are already valid.
    # A hard link is attempted first; copying is the safe fallback.
    try {
        New-Item `
            -ItemType HardLink `
            -Path $preparedPath `
            -Target ([string]$Item.SourcePath) `
            -ErrorAction Stop |
            Out-Null

        $preparation = "HardLink"
    }
    catch {
        Copy-Item `
            -LiteralPath ([string]$Item.SourcePath) `
            -Destination $preparedPath `
            -Force

        $preparation = "Copy"
    }

    return [PSCustomObject]@{
        Token        = $token
        PreparedPath = $preparedPath
        Preparation  = $preparation
    }
}


function ConvertTo-RimForgeWindowsCommandLineArgument {
    [CmdletBinding()]
    param(
        [AllowEmptyString()]
        [Parameter(Mandatory)]
        [string]$Value
    )

    if ($Value.Length -gt 0 -and $Value -notmatch '[\s"]') {
        return $Value
    }

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.Append('"')
    $backslashCount = 0

    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') {
            $backslashCount++
            continue
        }

        if ($character -eq '"') {
            [void]$builder.Append(('\' * (($backslashCount * 2) + 1)))
            [void]$builder.Append('"')
            $backslashCount = 0
            continue
        }

        if ($backslashCount -gt 0) {
            [void]$builder.Append(('\' * $backslashCount))
            $backslashCount = 0
        }

        [void]$builder.Append($character)
    }

    if ($backslashCount -gt 0) {
        [void]$builder.Append(('\' * ($backslashCount * 2)))
    }

    [void]$builder.Append('"')
    return $builder.ToString()
}

function Invoke-RimForgeNativeProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [int]$TimeoutSeconds = 0
    )

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $FilePath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.WorkingDirectory = Split-Path -Parent $FilePath

    $argumentListProperty = $startInfo.PSObject.Properties["ArgumentList"]

    if ($null -ne $argumentListProperty) {
        foreach ($argument in @($Arguments)) {
            [void]$startInfo.ArgumentList.Add([string]$argument)
        }
    }
    else {
        $startInfo.Arguments = (
            @(
                foreach ($argument in @($Arguments)) {
                    ConvertTo-RimForgeWindowsCommandLineArgument `
                        -Value ([string]$argument)
                }
            ) -join " "
        )
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo

    try {
        if (-not $process.Start()) {
            throw "The native process could not be started."
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()

        if ($TimeoutSeconds -gt 0) {
            if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
                try { $process.Kill() } catch {}
                throw (
                    "Native process timed out after {0} second(s)." -f
                    $TimeoutSeconds
                )
            }
        }
        else {
            $process.WaitForExit()
        }

        $stdout = $stdoutTask.GetAwaiter().GetResult()
        $stderr = $stderrTask.GetAwaiter().GetResult()

        return [PSCustomObject]@{
            ExitCode = [int]$process.ExitCode
            StdOut   = [string]$stdout
            StdErr   = [string]$stderr
            Command  = (
                "{0} {1}" -f
                $FilePath,
                (
                    @(
                        foreach ($argument in @($Arguments)) {
                            ConvertTo-RimForgeWindowsCommandLineArgument `
                                -Value ([string]$argument)
                        }
                    ) -join " "
                )
            )
        }
    }
    finally {
        $process.Dispose()
    }
}

function Invoke-RimForgeTexconvBatch {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$TexconvPath,

        [Parameter(Mandatory)]
        [array]$PreparedItems,

        [Parameter(Mandatory)]
        [string]$OutputFolder,

        [Parameter(Mandatory)]
        [string]$Format,

        [bool]$GenerateMipmaps = $true,

        [int]$TimeoutSeconds = 600
    )

    if (@($PreparedItems).Count -eq 0) {
        return
    }

    if (-not (Test-Path -LiteralPath $OutputFolder -PathType Container)) {
        New-Item -ItemType Directory -Path $OutputFolder -Force |
            Out-Null
    }

    $mipmapArgument = if ($GenerateMipmaps) { "0" } else { "1" }

    $arguments = @(
        "-nologo",
        "-y",
        "-f", $Format,
        "-m", $mipmapArgument,
        "-o", $OutputFolder
    )

    foreach ($prepared in @($PreparedItems)) {
        $arguments += [string]$prepared.PreparedPath
    }

    $processResult = Invoke-RimForgeNativeProcess `
        -FilePath $TexconvPath `
        -Arguments @($arguments) `
        -TimeoutSeconds $TimeoutSeconds

    if ($processResult.ExitCode -ne 0) {
        $details = @(
            $processResult.StdErr,
            $processResult.StdOut
        ) |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace([string]$_)
            }

        $detailText = if (@($details).Count -gt 0) {
            ($details -join "`n").Trim()
        }
        else {
            "No output was returned by texconv."
        }

        throw (
            "texconv.exe exited with code {0}.`n{1}`nCommand: {2}" -f
            $processResult.ExitCode,
            $detailText,
            $processResult.Command
        )
    }
}

function Install-RimForgeConvertedTexture {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$StagePath,

        [Parameter(Mandatory)]
        [string]$InstallPath,

        [Parameter(Mandatory)]
        [string]$BackupPath,

        [ValidateSet("Copy","Move","HardLink")]
        [string]$InstallStrategy = "Move",

        [ValidateSet("Copy","Move")]
        [string]$BackupStrategy = "Move",

        [bool]$OverwriteExistingDds = $false,

        [bool]$BackupExistingDds = $true
    )

    $existing = Test-Path -LiteralPath $InstallPath -PathType Leaf

    if ($existing -and -not $OverwriteExistingDds) {
        throw "DDS already exists and overwrite is disabled."
    }

    if ($existing) {
        if ($BackupExistingDds) {
            $backupParent = Split-Path -Parent $BackupPath

            if (-not (Test-Path -LiteralPath $backupParent -PathType Container)) {
                New-Item -ItemType Directory -Path $backupParent -Force |
                    Out-Null
            }

            if ($BackupStrategy -eq "Move") {
                Move-Item `
                    -LiteralPath $InstallPath `
                    -Destination $BackupPath `
                    -Force
            }
            else {
                Copy-Item `
                    -LiteralPath $InstallPath `
                    -Destination $BackupPath `
                    -Force
            }
        }
        else {
            Remove-Item `
                -LiteralPath $InstallPath `
                -Force
        }
    }

    $installParent = Split-Path -Parent $InstallPath

    if (-not (Test-Path -LiteralPath $installParent -PathType Container)) {
        New-Item -ItemType Directory -Path $installParent -Force |
            Out-Null
    }

    switch ($InstallStrategy) {
        "Move" {
            Move-Item `
                -LiteralPath $StagePath `
                -Destination $InstallPath `
                -Force
        }
        "HardLink" {
            try {
                New-Item `
                    -ItemType HardLink `
                    -Path $InstallPath `
                    -Target $StagePath `
                    -ErrorAction Stop |
                    Out-Null
            }
            catch {
                Copy-Item `
                    -LiteralPath $StagePath `
                    -Destination $InstallPath `
                    -Force
            }
        }
        default {
            Copy-Item `
                -LiteralPath $StagePath `
                -Destination $InstallPath `
                -Force
        }
    }
}

function Invoke-RimForgeTextureConversion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Plan,
        [Parameter(Mandatory)]$Rules,
        [Parameter(Mandatory)][string]$ProjectRoot,
        [ValidateSet("Stage","Install")][string]$Mode = "Stage",
        [AllowNull()][string]$TexconvPath,
        [string]$ManifestPath,
        [scriptblock]$ProgressCallback,
        [System.Threading.CancellationToken]$CancellationToken = (
            [System.Threading.CancellationToken]::None
        )
    )

    $resolvedTexconv = Resolve-RimForgeTexconvPath `
        -ProjectRoot $ProjectRoot `
        -ConfiguredPath $TexconvPath

    $tempRoot = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.TemporaryFolder)

    $backupRoot = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.BackupFolder)

    $cacheRoot = Resolve-RimForgeTexturePath `
        -ProjectRoot $ProjectRoot `
        -Path ([string]$Rules.CacheFolder)

    foreach ($folder in @($tempRoot, $backupRoot, $cacheRoot)) {
        New-Item -ItemType Directory -Path $folder -Force |
            Out-Null
    }

    $batchSize = [int](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "TexconvBatchSize" `
        -DefaultValue 16)

    if ($batchSize -lt 1) {
        $batchSize = 16
    }

    $batchTimeoutSeconds = [int](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "TexconvBatchTimeoutSeconds" `
        -DefaultValue 600)

    if ($batchTimeoutSeconds -lt 30) {
        $batchTimeoutSeconds = 600
    }

    $useCache = [bool](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "UseConversionCache" `
        -DefaultValue $true)

    $skipValidated = [bool](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "SkipValidatedOutputs" `
        -DefaultValue $true)

    $validateCached = [bool](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "ValidateCachedOutputs" `
        -DefaultValue $false)

    $installStrategy = [string](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "InstallStrategy" `
        -DefaultValue "Move")

    $backupStrategy = [string](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "BackupStrategy" `
        -DefaultValue "Move")

    $backupExistingDds = [bool](Get-RimForgeTextureSafeProperty `
        -InputObject $Rules `
        -PropertyName "BackupExistingDds" `
        -DefaultValue $false)

    $cachePath = Join-Path $cacheRoot "TextureConversionCache.json"
    $cache = if ($useCache) {
        Get-RimForgeTextureConversionCache -Path $cachePath
    }
    else {
        @{}
    }

    $updatedCache = @{}
    $results = @()
    $convertItems = @(
        $Plan.Items |
        Where-Object { $_.Disposition -eq "Convert" }
    )

    $total = @($convertItems).Count
    $processedCount = 0
    $cacheHitCount = 0
    $batchCount = if ($total -gt 0) {
        [Math]::Ceiling($total / [double]$batchSize)
    }
    else {
        0
    }

    # Preserve planned skips in the manifest.
    foreach ($item in @($Plan.Items)) {
        if ($item.Disposition -eq "Convert") {
            continue
        }

        $results += [PSCustomObject][ordered]@{
            PackageId = $item.PackageId
            SourcePath = $item.SourcePath
            StagePath = $item.StagePath
            InstallPath = $item.InstallPath
            Status = $item.Disposition
            Preparation = $null
            CacheHit = $false
            Error = $null
        }
    }

    for ($offset = 0; $offset -lt $total; $offset += $batchSize) {
        $CancellationToken.ThrowIfCancellationRequested()

        $lastIndex = [Math]::Min(
            $offset + $batchSize - 1,
            $total - 1
        )

        $batchItems = @($convertItems[$offset..$lastIndex])
        $batchNumber = [int][Math]::Floor($offset / $batchSize) + 1
        $batchRoot = Join-Path `
            $tempRoot `
            ("batch-{0}-{1}" -f
                $batchNumber,
                [guid]::NewGuid().ToString("N"))

        $preparedRoot = Join-Path $batchRoot "prepared"
        $convertedRoot = Join-Path $batchRoot "converted"

        New-Item -ItemType Directory -Path $preparedRoot -Force |
            Out-Null
        New-Item -ItemType Directory -Path $convertedRoot -Force |
            Out-Null

        $preparedItems = @()
        $batchFailures = @()

        try {
            foreach ($item in @($batchItems)) {
                $CancellationToken.ThrowIfCancellationRequested()
                $processedCount++

                if ($null -ne $ProgressCallback) {
                    & $ProgressCallback ([PSCustomObject]@{
                        Phase = (
                            "{0} batch {1}/{2}" -f
                            $Mode,
                            $batchNumber,
                            $batchCount
                        )
                        Current = $processedCount
                        Total = $total
                        Name = $item.RelativePath
                        Percent = if ($total -gt 0) {
                            [Math]::Floor(
                                (($processedCount - 1) / $total) * 100
                            )
                        }
                        else {
                            100
                        }
                    })
                }

                try {
                    $signature = Get-RimForgeTextureFileSignature `
                        -Path ([string]$item.SourcePath) `
                        -Item $item `
                        -Rules $Rules `
                        -TexconvPath $resolvedTexconv

                    $cacheKey = ([string]$item.SourcePath).ToLowerInvariant()
                    $expectedOutput = if ($Mode -eq "Install") {
                        [string]$item.InstallPath
                    }
                    else {
                        [string]$item.StagePath
                    }

                    $cacheEntry = if ($cache.ContainsKey($cacheKey)) {
                        $cache[$cacheKey]
                    }
                    else {
                        $null
                    }

                    if (
                        $useCache -and
                        $skipValidated -and
                        (Test-RimForgeCachedTextureOutput `
                            -CacheEntry $cacheEntry `
                            -ExpectedSignature $signature `
                            -ExpectedPath $expectedOutput `
                            -ValidateHeader $validateCached `
                            -ExpectedWidth ([int]$item.CanvasWidth) `
                            -ExpectedHeight ([int]$item.CanvasHeight) `
                            -ExpectMipmaps ([bool]$item.GenerateMipmaps)
                        )
                    ) {
                        $cacheHitCount++
                        $updatedCache[$cacheKey] = $cacheEntry

                        $results += [PSCustomObject][ordered]@{
                            PackageId = $item.PackageId
                            ModName = $item.ModName
                            SourcePath = $item.SourcePath
                            RelativePath = $item.RelativePath
                            StagePath = $item.StagePath
                            InstallPath = $item.InstallPath
                            BackupPath = Get-RimForgeTextureSafeProperty `
                                -InputObject $cacheEntry `
                                -PropertyName "BackupPath"
                            OriginalWidth = $item.OriginalWidth
                            OriginalHeight = $item.OriginalHeight
                            CanvasWidth = $item.CanvasWidth
                            CanvasHeight = $item.CanvasHeight
                            Format = "BC7_UNORM"
                            Installed = ($Mode -eq "Install")
                            Status = "Cached"
                            Preparation = Get-RimForgeTextureSafeProperty `
                                -InputObject $cacheEntry `
                                -PropertyName "Preparation"
                            CacheHit = $true
                            Error = $null
                        }

                        continue
                    }

                    $prepared = New-RimForgeFastPreparedInput `
                        -Item $item `
                        -PreparedFolder $preparedRoot

                    $preparedItems += [PSCustomObject]@{
                        Item = $item
                        Token = $prepared.Token
                        PreparedPath = $prepared.PreparedPath
                        Preparation = $prepared.Preparation
                        Signature = $signature
                        CacheKey = $cacheKey
                    }
                }
                catch {
                    $batchFailures += [PSCustomObject]@{
                        Item = $item
                        Error = $_.Exception.Message
                    }
                }
            }

            # Split by mipmap setting so each texconv invocation has one option set.
            foreach ($mipmapGroup in @(
                $preparedItems |
                Group-Object {
                    [bool]$_.Item.GenerateMipmaps
                }
            )) {
                $groupItems = @($mipmapGroup.Group)

                if ($null -ne $ProgressCallback) {
                    & $ProgressCallback ([PSCustomObject]@{
                        Phase = (
                            "Encoding BC7 batch {0}/{1}" -f
                            $batchNumber,
                            $batchCount
                        )
                        Current = $batchNumber
                        Total = $batchCount
                        Name = (
                            "{0} texture(s); texconv is working..." -f
                            @($groupItems).Count
                        )
                        Percent = if ($batchCount -gt 0) {
                            [Math]::Floor(
                                (($batchNumber - 1) / $batchCount) * 100
                            )
                        }
                        else {
                            100
                        }
                    })
                }

                Write-Host (
                    "Encoding BC7 batch {0}/{1} ({2} texture(s))..." -f
                    $batchNumber,
                    $batchCount,
                    @($groupItems).Count
                )

                $batchStarted = Get-Date

                Invoke-RimForgeTexconvBatch `
                    -TexconvPath $resolvedTexconv `
                    -PreparedItems $groupItems `
                    -OutputFolder $convertedRoot `
                    -Format "BC7_UNORM" `
                    -GenerateMipmaps ([bool]$groupItems[0].Item.GenerateMipmaps) `
                    -TimeoutSeconds $batchTimeoutSeconds

                $elapsed = (Get-Date) - $batchStarted

                Write-Host (
                    "Completed BC7 batch {0}/{1} in {2:n1}s." -f
                    $batchNumber,
                    $batchCount,
                    $elapsed.TotalSeconds
                )
            }

            foreach ($prepared in @($preparedItems)) {
                $item = $prepared.Item
                $producedPath = Join-Path `
                    $convertedRoot `
                    ("{0}.dds" -f $prepared.Token)

                try {
                    if (-not (Test-Path -LiteralPath $producedPath -PathType Leaf)) {
                        throw "texconv.exe did not produce the expected DDS file."
                    }

                    $stageParent = Split-Path -Parent $item.StagePath

                    if (-not (Test-Path -LiteralPath $stageParent -PathType Container)) {
                        New-Item -ItemType Directory -Path $stageParent -Force |
                            Out-Null
                    }

                    Move-Item `
                        -LiteralPath $producedPath `
                        -Destination $item.StagePath `
                        -Force

                    $validation = Test-RimForgeDdsFile `
                        -Path $item.StagePath `
                        -ExpectedWidth ([int]$item.CanvasWidth) `
                        -ExpectedHeight ([int]$item.CanvasHeight) `
                        -ExpectMipmaps ([bool]$item.GenerateMipmaps)

                    if (-not $validation.IsValid) {
                        throw "DDS validation failed: $($validation.Error)"
                    }

                    $safePackage = (
                        [string]$item.PackageId
                    ) -replace '[^a-zA-Z0-9._-]', "_"

                    $relativeDds = [System.IO.Path]::ChangeExtension(
                        [string]$item.RelativePath,
                        ".dds"
                    )

                    $backupPath = Join-Path `
                        (Join-Path $backupRoot $safePackage) `
                        $relativeDds

                    $installed = $false

                    if ($Mode -eq "Install") {
                        Install-RimForgeConvertedTexture `
                            -StagePath ([string]$item.StagePath) `
                            -InstallPath ([string]$item.InstallPath) `
                            -BackupPath $backupPath `
                            -InstallStrategy $installStrategy `
                            -BackupStrategy $backupStrategy `
                            -OverwriteExistingDds ([bool]$Rules.OverwriteExistingDds) `
                            -BackupExistingDds $backupExistingDds

                        $installed = $true
                    }

                    $finalPath = if ($installed) {
                        [string]$item.InstallPath
                    }
                    else {
                        [string]$item.StagePath
                    }

                    $finalFile = Get-Item -LiteralPath $finalPath -ErrorAction Stop

                    $cacheEntry = [PSCustomObject][ordered]@{
                        SourcePath = [string]$item.SourcePath
                        Signature = $prepared.Signature
                        OutputPath = $finalPath
                        OutputLength = [long]$finalFile.Length
                        OutputLastWriteUtc = $finalFile.LastWriteTimeUtc.ToString("o")
                        Width = [int]$item.CanvasWidth
                        Height = [int]$item.CanvasHeight
                        Format = "BC7_UNORM"
                        MipmapCount = [int]$validation.MipmapCount
                        Mode = $Mode
                        Preparation = $prepared.Preparation
                        BackupPath = if ($installed) { $backupPath } else { $null }
                    }

                    if ($useCache) {
                        $updatedCache[$prepared.CacheKey] = $cacheEntry
                    }

                    $results += [PSCustomObject][ordered]@{
                        PackageId = $item.PackageId
                        ModName = $item.ModName
                        SourcePath = $item.SourcePath
                        RelativePath = $item.RelativePath
                        StagePath = $item.StagePath
                        InstallPath = $item.InstallPath
                        BackupPath = if ($installed) { $backupPath } else { $null }
                        OriginalWidth = $item.OriginalWidth
                        OriginalHeight = $item.OriginalHeight
                        CanvasWidth = $item.CanvasWidth
                        CanvasHeight = $item.CanvasHeight
                        ScaledWidth = $item.ScaledWidth
                        ScaledHeight = $item.ScaledHeight
                        PaddingLeft = $item.PaddingLeft
                        PaddingRight = $item.PaddingRight
                        PaddingTop = $item.PaddingTop
                        PaddingBottom = $item.PaddingBottom
                        Format = "BC7_UNORM"
                        MipmapCount = $validation.MipmapCount
                        Installed = $installed
                        Status = if ($installed) { "Installed" } else { "Staged" }
                        Preparation = $prepared.Preparation
                        CacheHit = $false
                        Error = $null
                    }
                }
                catch {
                    $results += [PSCustomObject][ordered]@{
                        PackageId = $item.PackageId
                        ModName = $item.ModName
                        SourcePath = $item.SourcePath
                        RelativePath = $item.RelativePath
                        StagePath = $item.StagePath
                        InstallPath = $item.InstallPath
                        BackupPath = $null
                        Installed = $false
                        Status = "Failed"
                        Preparation = $prepared.Preparation
                        CacheHit = $false
                        Error = $_.Exception.Message
                    }
                }
            }

            foreach ($failure in @($batchFailures)) {
                $item = $failure.Item

                $results += [PSCustomObject][ordered]@{
                    PackageId = $item.PackageId
                    ModName = $item.ModName
                    SourcePath = $item.SourcePath
                    RelativePath = $item.RelativePath
                    StagePath = $item.StagePath
                    InstallPath = $item.InstallPath
                    BackupPath = $null
                    Installed = $false
                    Status = "Failed"
                    Preparation = $null
                    CacheHit = $false
                    Error = $failure.Error
                }
            }
        }
        catch {
            # If a batch-level texconv invocation fails, mark only unresolved
            # prepared items as failed and continue to the next batch.
            foreach ($prepared in @($preparedItems)) {
                $item = $prepared.Item

                if (@(
                    $results |
                    Where-Object {
                        $_.SourcePath -eq $item.SourcePath
                    }
                ).Count -gt 0) {
                    continue
                }

                $results += [PSCustomObject][ordered]@{
                    PackageId = $item.PackageId
                    ModName = $item.ModName
                    SourcePath = $item.SourcePath
                    RelativePath = $item.RelativePath
                    StagePath = $item.StagePath
                    InstallPath = $item.InstallPath
                    BackupPath = $null
                    Installed = $false
                    Status = "Failed"
                    Preparation = $prepared.Preparation
                    CacheHit = $false
                    Error = $_.Exception.Message
                }
            }
        }
        finally {
            if (Test-Path -LiteralPath $batchRoot -PathType Container) {
                Remove-Item `
                    -LiteralPath $batchRoot `
                    -Recurse `
                    -Force `
                    -ErrorAction SilentlyContinue
            }
        }
    }

    if ($useCache) {
        # Keep still-valid old entries that were not part of this plan.
        foreach ($key in @($cache.Keys)) {
            if (-not $updatedCache.ContainsKey($key)) {
                $updatedCache[$key] = $cache[$key]
            }
        }

        Save-RimForgeTextureConversionCache `
            -Path $cachePath `
            -Lookup $updatedCache
    }

    $manifest = [PSCustomObject][ordered]@{
        SchemaVersion = 2
        GeneratedAtUtc = [DateTime]::UtcNow.ToString("o")
        Mode = $Mode
        Format = "BC7_UNORM"
        KeepOriginalPng = $true
        Performance = [PSCustomObject][ordered]@{
            BatchSize = $batchSize
            BatchCount = $batchCount
            ConversionCacheEnabled = $useCache
            CacheHitCount = $cacheHitCount
            InstallStrategy = $installStrategy
            BackupStrategy = $backupStrategy
            BackupExistingDds = $backupExistingDds
        }
        Items = @($results)
        Summary = [PSCustomObject][ordered]@{
            Total = @($results).Count
            Staged = @($results | Where-Object Status -eq "Staged").Count
            Installed = @($results | Where-Object Status -eq "Installed").Count
            Cached = @($results | Where-Object Status -eq "Cached").Count
            Skipped = @(
                $results |
                Where-Object { $_.Status -like "Skip*" }
            ).Count
            Failed = @($results | Where-Object Status -eq "Failed").Count
            HardLinkedInputs = @(
                $results |
                Where-Object Preparation -eq "HardLink"
            ).Count
            PaddedInputs = @(
                $results |
                Where-Object Preparation -eq "ResizeAndPad"
            ).Count
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($ManifestPath)) {
        $parent = Split-Path -Parent $ManifestPath

        if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
            New-Item -ItemType Directory -Path $parent -Force |
                Out-Null
        }

        $manifest |
            ConvertTo-Json -Depth 14 |
            Set-Content `
                -LiteralPath $ManifestPath `
                -Encoding UTF8
    }

    return $manifest
}

function Restore-RimForgeTextures {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ManifestPath,
        [scriptblock]$ProgressCallback,
        [System.Threading.CancellationToken]$CancellationToken = (
            [System.Threading.CancellationToken]::None
        )
    )

    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Texture manifest not found: $ManifestPath"
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw |
        ConvertFrom-Json

    $installedItems = @(
        $manifest.Items |
        Where-Object { [bool]$_.Installed }
    )

    $results = @()
    $index = 0
    $total = @($installedItems).Count

    foreach ($item in @($installedItems)) {
        $CancellationToken.ThrowIfCancellationRequested()
        $index++

        if ($null -ne $ProgressCallback) {
            & $ProgressCallback ([PSCustomObject]@{
                Phase = "Restore"
                Current = $index
                Total = $total
                Name = $item.RelativePath
                Percent = if ($total -gt 0) {
                    [Math]::Floor((($index - 1) / $total) * 100)
                } else { 100 }
            })
        }

        try {
            if (Test-Path -LiteralPath $item.InstallPath -PathType Leaf) {
                Remove-Item -LiteralPath $item.InstallPath -Force
            }

            if (
                -not [string]::IsNullOrWhiteSpace([string]$item.BackupPath) -and
                (Test-Path -LiteralPath $item.BackupPath -PathType Leaf)
            ) {
                Copy-Item `
                    -LiteralPath $item.BackupPath `
                    -Destination $item.InstallPath `
                    -Force
            }

            $results += [PSCustomObject]@{
                InstallPath = $item.InstallPath
                Status = "Restored"
                Error = $null
            }
        }
        catch {
            $results += [PSCustomObject]@{
                InstallPath = $item.InstallPath
                Status = "Failed"
                Error = $_.Exception.Message
            }
        }
    }

    return [PSCustomObject]@{
        Restored = @($results | Where-Object Status -eq "Restored").Count
        Failed = @($results | Where-Object Status -eq "Failed").Count
        Items = @($results)
    }
}

Export-ModuleMember -Function `
    Import-RimForgeTextureRules,
    Resolve-RimForgeTexturePath,
    Test-RimForgePortableExecutable,
    Test-RimForgeTexconvExecutable,
    Get-RimForgeTexconvStatus,
    Request-RimForgeTexconvInstallApproval,
    Install-RimForgeTexconv,
    Resolve-RimForgeTexconvPath,
    Get-RimForgeNearestMultiple,
    Get-RimForgeTextureGeometry,
    Get-RimForgeTextureInventory,
    New-RimForgeTextureConversionPlan,
    Get-RimForgeDdsHeaderInfo,
    Get-RimForgeInvalidDdsInventory,
    New-RimForgeInvalidDdsRepairPlan,
    Invoke-RimForgeTextureConversion,
    Test-RimForgeDdsFile,
    Restore-RimForgeTextures
