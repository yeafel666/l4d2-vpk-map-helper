# 一键构建脚本
# 依赖：Windows 自带的 .NET Framework 编译器（无需安装任何 SDK）
# 用法：pwsh -File build.ps1

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- 定位 C# 编译器 ---
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $cmd = Get-Command csc -ErrorAction SilentlyContinue
    if ($cmd) {
        $csc = $cmd.Source
    } else {
        throw "找不到 C# 编译器 csc.exe。请确认 .NET Framework 已安装（Win10/11 自带）。"
    }
}

$build = Join-Path $root "build"
$dist  = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path $dist  | Out-Null

Write-Host "==> 1/3 生成多分辨率图标"
& $csc /nologo "/r:System.Drawing.dll" "/out:$build\IconGen.exe" (Join-Path $root "tools\IconGen.cs")
if ($LASTEXITCODE -ne 0) { throw "IconGen 编译失败" }
& (Join-Path $build "IconGen.exe") (Join-Path $build "l4d2icon.ico")

Write-Host "==> 2/3 编译主程序"
$srcs = @("MainForm.cs", "Vpk.cs", "KeyValues.cs", "MapCollector.cs", "Mission.cs", "Program.cs") |
        ForEach-Object { Join-Path $root "src\$_" }
& $csc /nologo /target:winexe "/out:$build\L4D2MapHelper.exe" "/win32icon:$build\l4d2icon.ico" @srcs
if ($LASTEXITCODE -ne 0) { throw "主程序编译失败" }

Write-Host "==> 3/3 打包发布产物"
Copy-Item (Join-Path $build "L4D2MapHelper.exe") (Join-Path $dist "L4D2开图助手.exe") -Force
Copy-Item (Join-Path $root "docs\使用说明.txt")  (Join-Path $dist "使用说明.txt")  -Force
Copy-Item (Join-Path $build "l4d2icon.ico")     (Join-Path $dist "l4d2icon.ico") -Force
Compress-Archive -Path (Join-Path $dist "*") -DestinationPath (Join-Path $root "L4D2开图助手.zip") -Force

Write-Host ""
Write-Host "构建完成："
Write-Host ("  exe : " + (Join-Path $dist "L4D2开图助手.exe"))
Write-Host ("  zip : " + (Join-Path $root "L4D2开图助手.zip"))
