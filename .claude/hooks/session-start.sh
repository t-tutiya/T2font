#!/bin/bash
# SessionStart フック（リモートセッション専用・D-ENV3／D-ENV4）
#
# 目的: リモート環境（claude.ai/code = Web/デスクトップ/スマホ）には dotnet/Unity が無く、
# Claude が毎回テストを試みて失敗する問題を防ぐ。
#  1. .NET SDK 8 の導入を試みる（成功すれば外部 NUnit `dotnet test Tests/` が実行可能になる）
#  2. 成否にかかわらず「この環境で実行できる検証」をセッション冒頭のコンテキストへ出力する
#     （Claude はこの出力を環境判定の正とし、実行不可な検証を試行しない）
#
# ネットワークポリシーが SDK 取得元を遮断している場合、導入は失敗する。その場合も本フックは
# エラー終了せず「コンテナ内では実行不可」を通知して正常終了する（graceful degradation）。
#
# 導入が失敗しても検証手段が無くなるわけではない——**push → CI（.github/workflows/quality.yml）で
# NUnit と Unity 側コンパイルの両方を回せる**（検証経路③・D-ENV4）。CI のほうが検証範囲が広いため、
# 下のドメインを許可してまでコンテナに SDK を入れる必要は通常ない。許可する場合の宛先は:
#   公式スクリプト経路: dot.net / builds.dotnet.microsoft.com（apt 経路は 80 番ポートのため通らない）
#   NuGet 復元: api.nuget.org
set -uo pipefail

TEST_PROJECT="Tests/TemplateProject.Tests.csproj"

# ローカル PC（dotnet/Unity 完備）では何もしない
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

LOG=/tmp/session-start.log
: > "$LOG"

# 経路③は dotnet の有無と無関係に常に使えるので、3 つの判定すべてに添える。
note_ci_route() {
  cat <<'MSG'
- コンテナ内で回せない検証も「検証できない」ではない。push → CI で NUnit と Unity コンパイルを回せる
  （検証経路③・D-ENV4。正典は docs/guides/working-agreement.md「実行環境（3 環境運用）」）:
    1. 作業ブランチを push する
    2. GitHub MCP actions_run_trigger（method=run_workflow / workflow_id=quality.yml / ref=そのブランチ）で起動
       ＊inputs は渡さない（入力を持たないので 422 で拒まれる）
       ＊手動実行で回るのは Job A だけ（restore → format whitespace → dotnet test → editorconfig-checker）
       ＊Job B（Unity コンパイルゲート）は PR でだけ回る（D-JOBBPR）。Claude は監視しない（ユーザーが
         GitHub 上で目視で確かめる・D-AGENTOPS）。Unity 側に触れた段も Job A の緑を見て次の段へ進み、
         段ごとにコミットを分けて push しておく
    3. actions_list（list_workflow_jobs）と get_job_logs で Job A の結果を読む
- 生成物の作り直しが要る変更は、コミットメッセージに [regen] を含めて push
  → regenerate.yml が書き出して同じブランチへコミット・push する。完了後に git pull してから次を積む（D-REGEN）
- ①（ユーザーのローカル PC）へ委譲が必要なのは Play Mode テストと実機目視だけ
- 未実施の検証は PR 本文のチェックリストに記載する（docs には書かない・D-ENV3）
MSG
}

note_unavailable() {
  cat <<'MSG'
[TemplateProject 環境判定] リモートセッション: dotnet 導入失敗（ネットワークポリシーで SDK 取得元が遮断）。
- コンテナ内で実行可能な検証: なし
- NUnit / dotnet build / pwsh Tools/verify-unity-compile.ps1 を試行しないこと（必ず失敗する）
MSG
  note_ci_route
}

note_nunit_ok() {
  cat <<MSG
[TemplateProject 環境判定] リモートセッション: dotnet 利用可・NuGet 復元済み。
- コンテナ内で実行可能な検証: 外部 NUnit（dotnet test ${TEST_PROJECT}）
- コンテナ内で実行不可: Unity 系（verify-unity-compile.ps1 / Play Mode。Unity Editor 必須）
MSG
  note_ci_route
}

ensure_dotnet() {
  command -v dotnet >/dev/null 2>&1 && return 0
  # 経路 1: Ubuntu apt（ネットワークポリシーが 80 番ポートを通す環境でだけ成功する）
  if apt-get update -qq >>"$LOG" 2>&1 && apt-get install -y -qq dotnet-sdk-8.0 >>"$LOG" 2>&1; then
    return 0
  fi
  # 経路 2: 公式 dotnet-install スクリプト
  if curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh >>"$LOG" 2>&1 \
     && bash /tmp/dotnet-install.sh --channel 8.0 --install-dir "$HOME/.dotnet" >>"$LOG" 2>&1; then
    export PATH="$HOME/.dotnet:$PATH"
    if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
      echo 'export PATH="$HOME/.dotnet:$PATH"' >>"$CLAUDE_ENV_FILE"
    fi
    command -v dotnet >/dev/null 2>&1 && return 0
  fi
  return 1
}

if ! ensure_dotnet; then
  note_unavailable
  exit 0
fi

# NuGet 復元（NUnit 等）。ここが通れば dotnet test が完走できる
if dotnet restore "${CLAUDE_PROJECT_DIR:-.}/${TEST_PROJECT}" >>"$LOG" 2>&1; then
  note_nunit_ok
else
  cat <<'MSG'
[TemplateProject 環境判定] リモートセッション: dotnet はあるが NuGet 復元に失敗（api.nuget.org 遮断の可能性）。
- コンテナ内で実行可能な検証: なし（dotnet test はパッケージ復元不能のため必ず失敗する。試行しないこと）
MSG
  note_ci_route
fi
exit 0
