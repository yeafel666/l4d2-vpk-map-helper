<#
    发布到 GitHub —— 用 REST API 完成，不依赖 git / gh CLI
    （GitHub Contents API 可直接建仓、传文件；Releases API 可挂编译好的发布包）

    前置：设置 token 到用户级环境变量（脚本从注册表读，不会写盘到任何配置文件）
        [Environment]::SetEnvironmentVariable('GITHUB_TOKEN', 'ghp_xxx', 'User')

    用法：
        pwsh -File tools\publish-github.ps1
        pwsh -File tools\publish-github.ps1 -RepoName MyRepo -Tag v1.1.0 -Private
#>
param(
    [string]$RepoName    = "l4d2-vpk-map-helper",
    [string]$Description = "求生之路2 第三方战役 VPK 开图指令助手 —— 拖入 VPK，自动列出每一关的 map 开图指令",
    [string]$Tag         = "v1.0.0",
    [string]$ReleaseName = "",
    [string]$Root        = "",
    [switch]$Private,
    [string]$Proxy       = ""     # 例如 http://127.0.0.1:7897
)

$ErrorActionPreference = "Stop"
if (-not $Root) { $Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path) }

# ---------------- token ----------------
$token = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "User")
if (-not $token) { $token = $env:GITHUB_TOKEN }
if (-not $token) {
    throw "未找到 GITHUB_TOKEN。请先执行：`[Environment]::SetEnvironmentVariable('GITHUB_TOKEN','<你的token>','User')`"
}

$headers = @{
    Authorization = "Bearer $token"
    "User-Agent"  = "l4d2-vpk-tool"
    "Accept"      = "application/vnd.github+json"
}
$api = "https://api.github.com"

function Invoke-Gh {
    param([string]$Method, [string]$Uri, $Body)
    $ps = @{
        Uri             = $Uri
        Method          = $Method
        Headers         = $headers
        TimeoutSec      = 60
        ErrorAction     = "Stop"
    }
    if ($Proxy) { $ps.Proxy = $Proxy }
    if ($null -ne $Body) {
        $json = $Body | ConvertTo-Json -Depth 10 -Compress
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
        $ps.Body = $bytes
        $ps.ContentType = "application/json; charset=utf-8"
    }
    try {
        Invoke-RestMethod @ps
    } catch {
        # 直连失败时自动回退到本机代理
        if ($Proxy) { throw }
        Write-Host "  ! 直连失败，改走代理重试: $($_.Exception.Message)" -ForegroundColor Yellow
        $global:Proxy = "http://127.0.0.1:7897"
        $ps.Proxy = $global:Proxy
        Invoke-RestMethod @ps
    }
}

function Put-GhFile {
    param([string]$Owner, [string]$Repo, [string]$Path, [string]$LocalFile, [string]$Branch)
    $bytes = [System.IO.File]::ReadAllBytes($LocalFile)
    $b64 = [Convert]::ToBase64String($bytes)
    $body = @{
        message = "上传 $Path"
        content = $b64
        branch  = $Branch
    }
    $enc = [System.Text.Encoding]::UTF8.GetBytes($Path)
    $encPath = [System.Uri]::EscapeDataString($Path)
    Invoke-Gh -Method PUT -Uri "$api/repos/$Owner/$Repo/contents/$encPath" -Body $body | Out-Null
    Write-Host "  + $Path" -ForegroundColor DarkGreen
}

# ---------------- 1. 身份 ----------------
Write-Host "==> 1/5 验证身份"
$me = Invoke-Gh -Method GET -Uri "$api/user"
Write-Host "  账号: $($me.login)  ($($me.name))"

# ---------------- 2. 建仓 ----------------
Write-Host "==> 2/5 检查/创建仓库 $RepoName"
$repo = $null
try {
    $repo = Invoke-Gh -Method GET -Uri "$api/repos/$($me.login)/$RepoName"
    Write-Host "  仓库已存在，沿用" -ForegroundColor Yellow
} catch {
    $repo = Invoke-Gh -Method POST -Uri "$api/user/repos" -Body @{
        name        = $RepoName
        description = $Description
        private     = [bool]$Private
        auto_init   = $false
    }
    Write-Host "  已创建" -ForegroundColor Green
}
$owner = $repo.owner.login
$branch = "main"

# ---------------- 3. 上传源码 ----------------
Write-Host "==> 3/5 上传源码"
$files = @(
    "README.md", ".gitignore", "LICENSE", "build.ps1",
    "docs\使用说明.txt"
)
foreach ($f in @("MainForm.cs","Vpk.cs","KeyValues.cs","MapCollector.cs","Mission.cs","Program.cs")) {
    $files += "src\$f"
}
foreach ($f in (Get-ChildItem (Join-Path $Root "tools") -Filter *.cs | Select-Object -ExpandProperty Name)) {
    $files += "tools\$f"
}
foreach ($f in $files) {
    $local = Join-Path $Root $f
    if (-not (Test-Path $local)) { Write-Host "  ! 缺少 $f，跳过" -ForegroundColor Yellow; continue }
    Put-GhFile -Owner $owner -Repo $RepoName -Path ($f -replace '\\','/') -LocalFile $local -Branch $branch
}

# ---------------- 4. 打 tag / 建 Release ----------------
Write-Host "==> 4/5 创建 Release $Tag"
if (-not $ReleaseName) { $ReleaseName = $Tag }
$releaseBody = @"
## 求生之路2 · 第三方战役开图指令助手 $Tag

把第三方战役的 ``.vpk`` 拖进来，自动解析出每一关的 ``map`` 开图指令，按第一关到最后一关排序。

**下载**：``L4D2开图助手.zip`` —— 解压双击 ``L4D2开图助手.exe`` 即可。
Windows 10/11 自带的 .NET Framework 就能跑，无需安装任何东西，全程离线。

### 用法
1. 把 ``.vpk`` 拖到窗口中间的白框里（可以一次拖多个，同一张大地图常被拆成多个 VPK）
2. 或者直接拖到 exe 图标上
3. 结果表按战役分组，点「复制全部开图指令」直接粘到服务器控制台

### 排序依据
``missions/*.txt`` 里的权威关卡序 → 地图命名规则 → VPK 内存储顺序。

源码： https://github.com/$owner/$RepoName
"@
$rel = Invoke-Gh -Method POST -Uri "$api/repos/$owner/$RepoName/releases" -Body @{
    tag_name         = $Tag
    target_commitish = $branch
    name             = $ReleaseName
    body             = $releaseBody
    draft            = $false
    prerelease       = $false
}
Write-Host "  Release 已创建（id=$($rel.id)）" -ForegroundColor Green

# ---------------- 5. 上传发布包 ----------------
Write-Host "==> 5/5 上传发布包"
$zip = Join-Path $Root "L4D2开图助手.zip"
if (Test-Path $zip) {
    $assetName = [System.Uri]::EscapeDataString("L4D2开图助手.zip")
    $uploadUrl = "https://uploads.github.com/repos/$owner/$RepoName/releases/$($rel.id)/assets?name=$assetName"
    $bytes = [System.IO.File]::ReadAllBytes($zip)
    $ps = @{
        Uri         = $uploadUrl
        Method      = "POST"
        Headers     = $headers
        Body        = $bytes
        ContentType = "application/zip"
        TimeoutSec  = 180
        ErrorAction = "Stop"
    }
    if ($Proxy) { $ps.Proxy = $Proxy }
    Invoke-RestMethod @ps | Out-Null
    Write-Host "  + L4D2开图助手.zip（$([math]::Round($bytes.Length/1KB,1)) KB）" -ForegroundColor DarkGreen
} else {
    Write-Host "  ! 未找到 $zip，跳过附件上传（先跑 build.ps1）" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "完成 → https://github.com/$owner/$RepoName"
Write-Host "Release → https://github.com/$owner/$RepoName/releases/tag/$Tag"
