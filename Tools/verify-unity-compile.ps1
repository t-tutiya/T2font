#requires -Version 7.0
<#
.SYNOPSIS
    Unity プロジェクトをヘッドレス (batchmode) でコンパイル検証する。

.DESCRIPTION
    Unity Editor を batchmode + Test Runner で起動し、本体および全テストコード
    (PlayMode テストアセンブリを含む) のコンパイルが通るかを検証する。

    Claude による実装後、Unity Editor を開く前にテストコードのコンパイル不通過を
    早期検出する目的のローカル検証スクリプト。CI の Job B（PR でだけ回る）より速いローカルの代替。

    Test Runner を起動するのは UNITY_INCLUDE_TESTS を有効化し、テスト asmdef も
    コンパイル対象に含めるため。既定では一致 0 件のフィルタを渡して「コンパイルのみ」
    を高速に検証する (実テストは走らせない)。-RunTests でテストも実行する。

    前提: 同一プロジェクトを Unity Editor で開いていないこと (Library ロック競合のため)。

.PARAMETER TestPlatform
    EditMode | PlayMode。既定 EditMode。PlayMode テストアセンブリの
    コンパイルを確実に検証したい場合は PlayMode を指定する。

.PARAMETER RunTests
    指定時は実際にテストを実行する。未指定時はコンパイル検証のみ (一致 0 件フィルタ)。

.PARAMETER TestFilter
    -RunTests と併用し、実行するテストを Unity の -testFilter で絞り込む
    (例: テストクラスの完全修飾名)。-RunTests 未指定時は無視される。

.PARAMETER UnityPath
    Unity.exe のパス。未指定時は ProjectSettings/ProjectVersion.txt から自動解決。

.PARAMETER TimeoutSec
    Unity プロセスのタイムアウト秒数。既定 900 (初回はアセット import で長くなる)。

.EXAMPLE
    pwsh Tools/verify-unity-compile.ps1
    既定 (EditMode・コンパイルのみ) で検証。

.EXAMPLE
    pwsh Tools/verify-unity-compile.ps1 -TestPlatform PlayMode
    PlayMode テストアセンブリを含めてコンパイル検証。

.OUTPUTS
    exit 0 = コンパイル成功 (RunTests 指定時はテストも全通過)
    exit 1 = コンパイルエラー
    exit 2 = テスト失敗 (RunTests 指定時のみ)
    exit 3 = 実行エラー (Unity 未検出 / 起動失敗 / タイムアウト 等)
#>
[CmdletBinding()]
param(
    [ValidateSet('EditMode', 'PlayMode')]
    [string]$TestPlatform = 'EditMode',
    [switch]$RunTests,
    [string]$TestFilter,
    [string]$UnityPath,
    [int]$TimeoutSec = 900
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- パス解決 ---------------------------------------------------------------

# スクリプトは <ProjectRoot>/Tools/ に置かれている前提
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt'

function Resolve-UnityExe {
    param([string]$Explicit, [string]$VersionFilePath)

    if ($Explicit) {
        if (-not (Test-Path $Explicit)) {
            throw "指定された Unity.exe が見つかりません: $Explicit"
        }
        return $Explicit
    }

    if (-not (Test-Path $VersionFilePath)) {
        throw "ProjectVersion.txt が見つかりません: $VersionFilePath"
    }
    $line = Get-Content $VersionFilePath | Where-Object { $_ -match '^m_EditorVersion:\s*(.+)$' } | Select-Object -First 1
    if (-not $line -or $line -notmatch '^m_EditorVersion:\s*(.+)$') {
        throw "ProjectVersion.txt から Unity バージョンを読み取れませんでした。"
    }
    $version = $Matches[1].Trim()

    $candidates = @(
        "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "D:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe",
        "C:\Program Files\Unity\Editor\Unity.exe"
    )
    $hit = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $hit) {
        throw "Unity $version の Unity.exe が既定パスに見つかりません。-UnityPath で明示してください。`n探索: $($candidates -join '; ')"
    }
    return $hit
}

try {
    $unityExe = Resolve-UnityExe -Explicit $UnityPath -VersionFilePath $versionFile
}
catch {
    Write-Host "[ERROR] $_" -ForegroundColor Red
    exit 3
}

# --- ログ/結果の出力先 ------------------------------------------------------
# 先頭ドットのフォルダ名 (.artifacts 等) は Unity が "not a valid directory name"
# 警告を出すため避ける。Logs/ は .gitignore 済みかつ Unity 慣用の非 Asset フォルダ。

$artifactDir = Join-Path $projectRoot 'Logs/headless-compile'
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$stamp      = Get-Date -Format 'yyyyMMdd_HHmmss'
$logFile    = Join-Path $artifactDir "compile_${TestPlatform}_${stamp}.log"
$resultFile = Join-Path $artifactDir "results_${TestPlatform}_${stamp}.xml"

# --- Unity 引数組み立て -----------------------------------------------------

$unityArgs = @(
    '-batchmode'
    '-nographics'
    '-projectPath', $projectRoot
    '-logFile', $logFile
    '-runTests'
    '-testPlatform', $TestPlatform
    '-testResults', $resultFile
)
if (-not $RunTests) {
    # 一致 0 件のフィルタ。コンパイルはフィルタ前に行われるため、テスト asmdef は
    # コンパイルされるが実テストは走らない (高速・グラフィックス非依存)。
    $unityArgs += @('-testFilter', '__T2_HEADLESS_COMPILE_ONLY_NO_MATCH__')
}
elseif ($TestFilter) {
    $unityArgs += @('-testFilter', $TestFilter)
}

Write-Host "Unity        : $unityExe"
Write-Host "Project      : $projectRoot"
Write-Host "TestPlatform : $TestPlatform"
Write-Host "Mode         : $(if ($RunTests) { 'テスト実行' } else { 'コンパイルのみ' })"
Write-Host "Log          : $logFile"
Write-Host "----------------------------------------------------------------"
Write-Host "Unity を batchmode で起動中... (初回はアセット import で数分かかる場合があります)"

# --- 実行 -------------------------------------------------------------------

$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
if (-not $proc.WaitForExit($TimeoutSec * 1000)) {
    try { $proc.Kill($true) } catch { }
    Write-Host "[ERROR] Unity がタイムアウトしました (${TimeoutSec}s)。" -ForegroundColor Red
    Write-Host "ログ: $logFile"
    exit 3
}
$exitCode = $proc.ExitCode

# --- ログ解析 ---------------------------------------------------------------

# @() で必ず配列化する (StrictMode 下でスカラー/$null に .Count を呼ぶと例外になるため)
$compileErrors = @()
$lockHit = $false
if (Test-Path $logFile) {
    $logLines = Get-Content $logFile
    # C# コンパイラエラー形式: Path.cs(line,col): error CSxxxx: message
    $compileErrors = @($logLines |
        Where-Object { $_ -match '\)\s*:\s*error\s' -or $_ -match ':\s*error\s+CS\d+' } |
        Select-Object -Unique)
    # Library ロック競合 (Editor で同一プロジェクトを開いている場合)
    $lockHit = @($logLines | Where-Object {
        $_ -match 'It looks like another Unity instance is running' -or
        $_ -match 'Multiple Unity instances cannot open the same project'
    }).Count -gt 0
}

Write-Host "----------------------------------------------------------------"
Write-Host "Unity 終了コード: $exitCode"

if ($lockHit) {
    Write-Host "[ERROR] 同一プロジェクトを Unity Editor で開いている可能性があります。" -ForegroundColor Red
    Write-Host "        Editor を閉じてから再実行してください。" -ForegroundColor Red
    exit 3
}

if ($compileErrors.Count -gt 0) {
    Write-Host "[FAIL] コンパイルエラーを $($compileErrors.Count) 件検出しました:" -ForegroundColor Red
    $compileErrors | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    Write-Host ""
    Write-Host "詳細ログ: $logFile"
    exit 1
}

# テスト結果の解析 (RunTests 指定時のみ意味がある)
if ($RunTests -and (Test-Path $resultFile)) {
    try {
        [xml]$xml = Get-Content $resultFile
        $tr = $xml.SelectSingleNode('//test-run')
        if ($tr) {
            $total  = $tr.total
            $passed = $tr.passed
            $failed = $tr.failed
            Write-Host "テスト結果: total=$total passed=$passed failed=$failed"
            if ([int]$failed -gt 0) {
                Write-Host "[FAIL] テストが $failed 件失敗しました。詳細: $resultFile" -ForegroundColor Red
                exit 2
            }
        }
    }
    catch {
        Write-Host "[WARN] テスト結果 XML の解析に失敗しました: $_" -ForegroundColor Yellow
    }
}

Write-Host "[OK] コンパイル成功。エラーはありません。" -ForegroundColor Green
exit 0
