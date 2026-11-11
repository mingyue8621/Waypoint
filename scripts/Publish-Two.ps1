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
    dotnet publish $project -c Release -o $Out --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $project failed (exit $LASTEXITCODE)" }
}
# Their names are the projects' assembly names, so list whatever landed rather than spelling them out here.
Get-ChildItem $Out -Filter *.exe | Sort-Object Name |
    ForEach-Object { Write-Host ("{0}  {1:n0} bytes" -f $_.Name, $_.Length) }
