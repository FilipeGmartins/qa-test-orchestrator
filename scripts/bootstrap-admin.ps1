$ErrorActionPreference = 'Stop'
$previousLogin = $env:QA_ADMIN_LOGIN
$previousName = $env:QA_ADMIN_NAME
$previousPassword = $env:QA_ADMIN_PASSWORD
$projectRoot = Split-Path $PSScriptRoot
$localDotnet = Join-Path $projectRoot '.tools/dotnet/dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localDotnet) { $localDotnet } else { 'dotnet' }
Push-Location $projectRoot
try {
    $env:QA_ADMIN_LOGIN = Read-Host 'Login do primeiro administrador'
    $env:QA_ADMIN_NAME = Read-Host 'Nome'
    $securePassword = Read-Host 'Senha (12 a 128 caracteres)' -AsSecureString
    $credential = [System.Management.Automation.PSCredential]::new('bootstrap', $securePassword)
    $env:QA_ADMIN_PASSWORD = $credential.GetNetworkCredential().Password
    & $dotnetCommand run --project backend/src/Api -- --bootstrap-admin
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível criar o administrador. Confira as migrações e se já existe uma conta.' }
}
finally {
    $env:QA_ADMIN_LOGIN = $previousLogin
    $env:QA_ADMIN_NAME = $previousName
    $env:QA_ADMIN_PASSWORD = $previousPassword
    $credential = $null
    if ($securePassword) { $securePassword.Dispose() }
    Pop-Location
}
