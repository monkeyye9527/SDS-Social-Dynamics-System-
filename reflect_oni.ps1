param(
    # 游戏Managed目录。留空时依次回退: config/gamepaths.yaml > 常见Steam安装路径
    [string]$Managed = ''
)

if (-not $Managed) {
    $gp = Join-Path $PSScriptRoot 'config\gamepaths.yaml'
    if (Test-Path $gp) {
        foreach ($line in Get-Content $gp) {
            if ($line -match '^\s*managed_folder:\s*"?([^"]+)"?\s*$') { $Managed = $Matches[1]; break }
        }
    }
}
if (-not $Managed) {
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed'),
        (Join-Path $env:ProgramFiles 'Steam\steamapps\common\OxygenNotIncluded\OxygenNotIncluded_Data\Managed'),
        (Join-Path $env:ProgramFiles 'Epic Games\OxygenNotIncluded\OxygenNotIncluded_Data\Managed')
    )
    $Managed = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $Managed -or -not (Test-Path $Managed)) {
    [Console]::Out.WriteLine('ERR: 未找到游戏 Managed 目录，请用 -Managed "<路径>" 指定')
    exit 1
}

$handler = [System.ResolveEventHandler]{
    param($s, $e)
    $name = (New-Object System.Reflection.AssemblyName($e.Name)).Name
    $p = Join-Path $managed ($name + '.dll')
    if (Test-Path $p) { return [System.Reflection.Assembly]::LoadFrom($p) }
    return $null
}
[System.AppDomain]::CurrentDomain.add_AssemblyResolve($handler)
try {
    $a = [System.Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    [Console]::Out.WriteLine('LOADED A-CS OK')
    $t = $a.GetType('ModUtil')
    if ($t) {
        foreach ($mi in $t.GetMethods()) {
            if ($mi.Name -eq 'AddBuildingToPlanScreen') {
                $ps = $mi.GetParameters()
                $sig = ($ps | ForEach-Object { $_.ParameterType.Name }) -join ','
                [Console]::Out.WriteLine('ABTPS: ' + $sig)
            }
        }
    }
} catch {
    [Console]::Out.WriteLine('ERR: ' + $_.Exception.GetType().Name + ' :: ' + $_.Exception.Message)
    if ($_.Exception.InnerException) { [Console]::Out.WriteLine('INNER: ' + $_.Exception.InnerException.GetType().Name + ' :: ' + $_.Exception.InnerException.Message) }
    [Console]::Out.WriteLine($_.ScriptStackTrace)
}
[Console]::Out.WriteLine('DONE')