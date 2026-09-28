param([int]$Port = 5117)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$runtime = Join-Path $workspace '.tunnel'
$executable = Join-Path $runtime 'cloudflared.exe'
$statePath = Join-Path $runtime 'state.json'
if (!(Test-Path -LiteralPath $executable)) { throw 'Falta .tunnel/cloudflared.exe. Descarga la herramienta oficial de Cloudflare.' }
if (Test-Path -LiteralPath $statePath) {
    $previous = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    if (Get-Process -Id $previous.TunnelPid -ErrorAction SilentlyContinue) {
        Write-Output "El túnel ya está iniciado: $($previous.PublicUrl)"
        exit
    }
    if (Get-Process -Id $previous.AppPid -ErrorAction SilentlyContinue) {
        throw 'La instancia de prueba anterior sigue activa. Ejecuta Scripts/Stop-TestTunnel.ps1 antes de iniciar otro túnel.'
    }
}
if (!(Test-Path -LiteralPath (Join-Path $workspace 'bin/TunnelTest/TicketSales.dll'))) {
    throw 'Compila primero: dotnet build TicketSales.csproj --no-restore -p:OutputPath=bin/TunnelTest/'
}
$tunnelLog = Join-Path $runtime 'cloudflared.log'
$tunnel = Start-Process -FilePath $executable -ArgumentList @('tunnel', '--no-autoupdate', '--protocol', 'http2', '--edge-ip-version', '4', '--url', "http://127.0.0.1:$Port") -WorkingDirectory $workspace -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'cloudflared.stdout.log') -RedirectStandardError $tunnelLog
try {
    $publicUrl = $null
    for ($attempt = 0; $attempt -lt 45; $attempt++) {
        Start-Sleep -Seconds 1
        if ($tunnel.HasExited) { throw 'Cloudflare no pudo iniciar. Revisa .tunnel/cloudflared.log.' }
        $logText = Get-Content -LiteralPath $tunnelLog -Raw -ErrorAction SilentlyContinue
        if ($logText -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $publicUrl = $Matches[0]; break }
    }
    if (!$publicUrl) { throw 'Cloudflare no entregó una URL. Revisa .tunnel/cloudflared.log.' }
    $settingsFile = Join-Path $workspace 'appsettings.Tunnel.json'
    $settings = if (Test-Path -LiteralPath $settingsFile) { Get-Content -LiteralPath $settingsFile -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
    if (!$settings.ContainsKey('Recurrente')) { $settings['Recurrente'] = @{} }
    $settings['Recurrente']['PublicBaseUrl'] = $publicUrl
    $settings['AllowedHosts'] = ([uri]$publicUrl).Host + ';localhost;127.0.0.1'
    $settings | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $settingsFile -Encoding utf8

    # Reuse local encryption/admin settings without copying or displaying their secrets.
    $development = Get-Content -LiteralPath (Join-Path $workspace 'appsettings.Development.json') -Raw | ConvertFrom-Json
    $variableNames = @('ASPNETCORE_ENVIRONMENT','Encryption__PersonalDataKey','AdminSeed__UserName','AdminSeed__Password')
    $original = @{}
    foreach ($name in $variableNames) { $original[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
    try {
        $env:ASPNETCORE_ENVIRONMENT = 'Tunnel'
        if (!$env:Encryption__PersonalDataKey) { $env:Encryption__PersonalDataKey = $development.Encryption.PersonalDataKey }
        if (!$env:AdminSeed__UserName) { $env:AdminSeed__UserName = $development.AdminSeed.UserName }
        if (!$env:AdminSeed__Password) { $env:AdminSeed__Password = $development.AdminSeed.Password }
        $application = Start-Process -FilePath 'dotnet' -ArgumentList @('bin/TunnelTest/TicketSales.dll', '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $workspace -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime 'application.log') -RedirectStandardError (Join-Path $runtime 'application.error.log')
    }
    finally { foreach ($name in $variableNames) { [Environment]::SetEnvironmentVariable($name, $original[$name], 'Process') } }
    @{ PublicUrl = $publicUrl; WebhookUrl = "$publicUrl/webhooks/recurrente"; TunnelPid = $tunnel.Id; AppPid = $application.Id; Port = $Port; StartedAt = (Get-Date).ToString('O') } | ConvertTo-Json | Set-Content -LiteralPath $statePath -Encoding utf8
    Write-Output "Aplicación: $publicUrl"
    Write-Output "Webhook: $publicUrl/webhooks/recurrente"
    Write-Output "Procesos: aplicación $($application.Id), túnel $($tunnel.Id)"
}
catch {
    if (!$tunnel.HasExited) { Stop-Process -Id $tunnel.Id -Force }
    throw
}
