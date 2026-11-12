# usage: powershell -NoProfile -ExecutionPolicy Bypass -File scripts\Publish-Two.ps1 [-Out <folder>]
#
# Publishes the two single-purpose launchers into <folder> (bin\solo by default), one file each. Their assembly names are
# set in their .csproj files (the server one and the login one), because both are Chinese now:
#   the server launcher  Waypoint's window with the servers' sections: Control (service table, Start/Stop per server,
#                        Stop all / Stop servers / Start servers, live.ps1's switches and output, setup checks), Versions,
#                        Mods, Backups, Accounts, Performance, Timings, Logs, Console, Tools, Settings. Play, Install,
#                        Setup, Releases and the folder picker are left out. Needs a source checkout.
#   the login launcher   a release install, and nothing else: registers an account through app\GameServer.Auth.exe and
#                        starts the game. Reads no checkout, so it can be handed out with a release.
param([string]$Out = "")
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($Out)) { $Out = Join-Path $repo 'bin\solo' }
New-Item -ItemType Directory -Force $Out | Out-Null
Set-Location $repo
foreach ($project in 'src\ServerLauncher', 'src\ClientLauncher') {
    Write-Host "[$(Get-Date -Format 'HH:mm:ss')] publishing $project ..."
    # 流放之路服务端.exe is handed to other PCs on its own: self-contained, so no .NET Desktop Runtime has to be installed
    # there first (it is ~150 MB instead of ~23 MB for that reason). The switch has to be a global property, because a
    # self-contained executable may not reference the framework-dependent Waypoint.exe (NETSDK1150), so the whole
    # reference chain is built self-contained for this project only. The login launcher stays as it was: it lives inside
    # the client folder, next to everything else.
    $carry = @()
    if ($project -eq 'src\ServerLauncher') { $carry = @('-p:SelfContained=true') }
    dotnet publish $project -c Release -o $Out --nologo -v q @carry
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed (exit $LASTEXITCODE)" }
}
# Their names are the projects' assembly names, so list whatever landed rather than spelling them out here.
Get-ChildItem $Out -Filter *.exe | Sort-Object Name |
    ForEach-Object { Write-Host ("{0}  {1:n0} bytes" -f $_.Name, $_.Length) }
