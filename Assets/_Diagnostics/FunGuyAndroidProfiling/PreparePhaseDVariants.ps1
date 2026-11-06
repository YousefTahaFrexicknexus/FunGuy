param(
    [string]$ProjectRoot = 'D:\Work\FunGuy'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$sourceURP = Join-Path $ProjectRoot 'Assets\Settings\Mobile_RPAsset.asset'
$sourceProfile = Join-Path $ProjectRoot 'Assets\Settings\Build Profiles\FunGuy Vulkan Targeted Profiling.asset'
$resourceDir = Join-Path $ProjectRoot 'Assets\_Diagnostics\FunGuyAndroidProfiling\Resources'
$profileDir = Join-Path $ProjectRoot 'Assets\Settings\Build Profiles'
$evidence = 'C:\Users\USER\Documents\Codex\2026-11-06\do-you-remember-when-we-profiled\work\funguy-profiling\phase-d-visual-ab'

$sourceText = [System.IO.File]::ReadAllText($sourceURP)
$profileText = [System.IO.File]::ReadAllText($sourceProfile)
$requiredURP = @(
    'm_Name: Mobile_RPAsset',
    'm_ShadowDistance: 160',
    'm_RenderScale: 0.75',
    'm_UseSRPBatcher: 1',
    'm_GPUResidentDrawerMode: 1',
    'm_GPUResidentDrawerEnableOcclusionCullingInCameras: 0'
)
$requiredProfile = @('m_Name: FunGuy Vulkan Targeted Profiling', 'm_HasScriptingDefines: 0', 'm_ScriptingDefines: []')
foreach ($value in $requiredURP) {
    $count = ([regex]::Matches($sourceText, [regex]::Escape($value))).Count
    if ($count -ne 1) { throw "Unexpected URP source occurrence ($count) for: $value" }
}
foreach ($value in $requiredProfile) {
    $count = ([regex]::Matches($profileText, [regex]::Escape($value))).Count
    if ($count -ne 1) { throw "Unexpected BuildProfile occurrence ($count) for: $value" }
}

$variants = @(
    @{ Name='Baseline'; Shadow120=$false; OcclusionOn=$false; SmallShadowOff=$false },
    @{ Name='Shadow120'; Shadow120=$true; OcclusionOn=$false; SmallShadowOff=$false },
    @{ Name='OcclusionOn'; Shadow120=$false; OcclusionOn=$true; SmallShadowOff=$false },
    @{ Name='GrassFlowerShadowOff'; Shadow120=$false; OcclusionOn=$false; SmallShadowOff=$true },
    @{ Name='Combined'; Shadow120=$true; OcclusionOn=$true; SmallShadowOff=$true }
)

foreach ($v in $variants) {
    $assetPath = Join-Path $resourceDir ("FunGuy_PhaseD_{0}.asset" -f $v.Name)
    $profilePath = Join-Path $profileDir ("FunGuy Vulkan PhaseD {0}.asset" -f $v.Name)
    foreach ($path in @($assetPath, "$assetPath.meta", $profilePath, "$profilePath.meta")) {
        if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite existing file $path" }
    }
}

New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$originals = @($sourceURP, "$sourceURP.meta", $sourceProfile, "$sourceProfile.meta")
foreach ($file in $originals) {
    Copy-Item -LiteralPath $file -Destination (Join-Path $evidence ([System.IO.Path]::GetFileName($file))) -Force
}
$utf8 = [System.Text.UTF8Encoding]::new($false)
$result = [System.Collections.Generic.List[object]]::new()

foreach ($v in $variants) {
    $name = [string]$v.Name
    $resourceName = "FunGuy_PhaseD_$name"
    $assetPath = Join-Path $resourceDir "$resourceName.asset"
    $profileName = "FunGuy Vulkan PhaseD $name"
    $profilePath = Join-Path $profileDir "$profileName.asset"

    $urp = $sourceText.Replace('m_Name: Mobile_RPAsset', "m_Name: $resourceName")
    if ($v.Shadow120) { $urp = $urp.Replace('m_ShadowDistance: 160', 'm_ShadowDistance: 120') }
    if ($v.OcclusionOn) {
        $urp = $urp.Replace(
            'm_GPUResidentDrawerEnableOcclusionCullingInCameras: 0',
            'm_GPUResidentDrawerEnableOcclusionCullingInCameras: 1'
        )
    }
    [System.IO.File]::WriteAllText($assetPath, $urp, $utf8)
    $urpMeta = [System.IO.File]::ReadAllText("$sourceURP.meta")
    $urpMeta = [regex]::Replace($urpMeta, '(?m)^guid: [a-f0-9]{32}$', 'guid: ' + [guid]::NewGuid().ToString('N'))
    [System.IO.File]::WriteAllText("$assetPath.meta", $urpMeta, $utf8)

    $defines = @('FUNG_PHASED_TEST')
    if ($v.Shadow120) { $defines += 'FUNG_PHASED_SHADOW120' }
    if ($v.OcclusionOn) { $defines += 'FUNG_PHASED_OCCLUSION_ON' }
    if ($v.SmallShadowOff) { $defines += 'FUNG_PHASED_SMALL_SHADOWS_OFF' }
    $serializedDefines = 'm_ScriptingDefines:' + [Environment]::NewLine +
        (($defines | ForEach-Object { '  - ' + $_ }) -join [Environment]::NewLine)

    $profile = $profileText.Replace('m_Name: FunGuy Vulkan Targeted Profiling', "m_Name: $profileName")
    $profile = $profile.Replace('m_HasScriptingDefines: 0', 'm_HasScriptingDefines: 1')
    $profile = $profile.Replace('m_ScriptingDefines: []', $serializedDefines)
    [System.IO.File]::WriteAllText($profilePath, $profile, $utf8)
    $profileMeta = [System.IO.File]::ReadAllText("$sourceProfile.meta")
    $profileMeta = [regex]::Replace($profileMeta, '(?m)^guid: [a-f0-9]{32}$', 'guid: ' + [guid]::NewGuid().ToString('N'))
    [System.IO.File]::WriteAllText("$profilePath.meta", $profileMeta, $utf8)

    $result.Add([pscustomobject]@{
        Name=$name
        URP=$assetPath
        BuildProfile=$profilePath
        ShadowDistance=$(if ($v.Shadow120) { 120 } else { 160 })
        GPUOcclusion=[bool]$v.OcclusionOn
        SmallFoliageShadowsOff=[bool]$v.SmallShadowOff
        Defines=$defines
    })
}

$manifest = Join-Path $evidence 'variants.json'
[System.IO.File]::WriteAllText($manifest, ($result | ConvertTo-Json -Depth 5), $utf8)
$originalURPHash = (Get-FileHash -LiteralPath $sourceURP).Hash
$backupURPHash = (Get-FileHash -LiteralPath (Join-Path $evidence 'Mobile_RPAsset.asset')).Hash
if ($originalURPHash -ne $backupURPHash) { throw 'Production URP changed unexpectedly!' }

$result | Format-Table Name, ShadowDistance, GPUOcclusion, SmallFoliageShadowsOff -AutoSize
Write-Output "Generated $($result.Count) isolated URP+BuildProfile variants, original URP hash unchanged."
