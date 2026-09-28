$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$statePath = Join-Path $workspace '.tunnel/state.json'
if (!(Test-Path -LiteralPath $statePath)) { Write-Output 'No hay un túnel registrado.'; exit }
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
foreach ($entry in @(@{ Id = $state.TunnelPid; Name = 'cloudflared' }, @{ Id = $state.AppPid; Name = 'dotnet' })) {
    $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
    if ($process -and $process.ProcessName -eq $entry.Name -and $process.StartTime -ge ([datetime]$state.StartedAt).AddMinutes(-2)) {
        Stop-Process -Id $process.Id
    }
}
Remove-Item -LiteralPath $statePath
Write-Output 'Túnel y aplicación de prueba detenidos.'
