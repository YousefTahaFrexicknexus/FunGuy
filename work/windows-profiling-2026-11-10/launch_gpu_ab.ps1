$ErrorActionPreference = "Stop"
$root = (Get-Location).Path
$buildProgress = Join-Path $root "work/windows-profiling-2026-11-10/build-result.txt"
if (!(Test-Path $buildProgress)) { throw "Build result missing" }
$report = Get-Content $buildProgress
if ($report -notcontains "result=Succeeded") { throw "Build has not succeeded" }
$exeLine = $report | Where-Object { $_ -like "output=*" } | Select-Object -First 1
$exe = $exeLine.Substring(7)
if (!(Test-Path $exe)) { throw "Build exe not found: $exe" }

$dir = Join-Path $root "work/windows-profiling-2026-11-10/gpu-ab-run"
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$log = Join-Path $dir "launch.txt"
function WriteLog($message) { ((Get-Date).ToString("o") + " " + $message) | Tee-Object -FilePath $log -Append }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class FunGuyAbWin32 {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT rect);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint process);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool attached);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h, int show);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint action, uint dx, uint dy, uint data, UIntPtr extra);
}
'@
$arguments = @("-force-d3d12","-screen-fullscreen","0","-screen-width","1920","-screen-height","1080","-funguy-profile-seconds=130","-funguy-run-ab")
$p = Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory (Split-Path $exe) -PassThru
WriteLog "START gamePID=$($p.Id) exe=$exe args=$($arguments -join ' ')"
$deadline = (Get-Date).AddSeconds(45)
do {
    Start-Sleep -Milliseconds 350
    $p.Refresh()
    if($p.HasExited) { throw "Player terminated during startup code=$($p.ExitCode)" }
} while ($p.MainWindowHandle -eq [IntPtr]::Zero -and (Get-Date) -lt $deadline)
if ($p.MainWindowHandle -eq [IntPtr]::Zero) { throw "Window not created" }
Start-Sleep -Seconds 4
$p.Refresh()
$h=$p.MainWindowHandle
$r=New-Object FunGuyAbWin32+RECT
[void][FunGuyAbWin32]::GetWindowRect($h,[ref]$r)
$old=[FunGuyAbWin32]::GetForegroundWindow()
[uint32]$oldPid=0
$oldThread=[FunGuyAbWin32]::GetWindowThreadProcessId($old,[ref]$oldPid)
[uint32]$gamePid=0
$gameThread=[FunGuyAbWin32]::GetWindowThreadProcessId($h,[ref]$gamePid)
$mine=[FunGuyAbWin32]::GetCurrentThreadId()
$a=[FunGuyAbWin32]::AttachThreadInput($mine,$oldThread,$true)
$b=[FunGuyAbWin32]::AttachThreadInput($mine,$gameThread,$true)
[void][FunGuyAbWin32]::ShowWindowAsync($h,9)
[void][FunGuyAbWin32]::BringWindowToTop($h)
$set=[FunGuyAbWin32]::SetForegroundWindow($h)
if($b){[void][FunGuyAbWin32]::AttachThreadInput($mine,$gameThread,$false)}
if($a){[void][FunGuyAbWin32]::AttachThreadInput($mine,$oldThread,$false)}
Start-Sleep -Milliseconds 450
[uint32]$activePid=0
[void][FunGuyAbWin32]::GetWindowThreadProcessId([FunGuyAbWin32]::GetForegroundWindow(),[ref]$activePid)
WriteLog "FOCUS previousPID=$oldPid gamePID=$($p.Id) foregroundPID=$activePid success=$set rect=$($r.Left),$($r.Top),$($r.Right),$($r.Bottom)"
if ($activePid -ne $p.Id) { WriteLog "No click: game is not foreground (perhaps Windows Firewall prompt)."; exit 2 }
$x=[int]($r.Left + ($r.Right - $r.Left) * 0.64)
$y=[int]($r.Top + ($r.Bottom - $r.Top) * 0.82)
[void][FunGuyAbWin32]::SetCursorPos($x,$y)
Start-Sleep -Milliseconds 160
[FunGuyAbWin32]::mouse_event(2,0,0,0,[UIntPtr]::Zero)
Start-Sleep -Milliseconds 180
[FunGuyAbWin32]::mouse_event(4,0,0,0,[UIntPtr]::Zero)
WriteLog "CLICKED Level-Based at $x,$y; automatic experiment begins when run ready"
Start-Sleep -Seconds 4
WriteLog "GAME pid=$($p.Id) responsive=$($p.Responding) handle=$($p.MainWindowHandle)"
