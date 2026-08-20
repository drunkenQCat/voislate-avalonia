#!/usr/bin/env bash
# P2 合并脚本（由主 Agent 在每个 agent 分支验证通过后逐步执行）。
# 用法: bash scripts/p2-merge.sh <step>   其中 step ∈ check|merge-a|merge-e|merge-d|merge-b|merge-c|verify
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1

step="${1:-check}"

check_one() { # $1=worktree path
  local wt="$1"
  echo "== check $wt =="
  (cd "$wt" && git status --porcelain | grep . && { echo "!! uncommitted changes in $wt"; exit 1; } || true)
  (cd "$wt" && dotnet build VoiSlate.slnx -c Debug --no-restore 2>&1 | tail -3)
  (cd "$wt" && dotnet test VoiSlate.slnx --no-build 2>&1 | tail -1)
}

case "$step" in
  check)
    for x in a b c d e; do check_one "$ROOT/../voislate-agent-$x"; done
    ;;
  merge-a|merge-e|merge-d|merge-b|merge-c)
    local br="agent-${step#merge-}"
    echo "== merge $br into main =="
    (cd "$ROOT" && git merge --no-ff -m "merge: $br (P2)" "$br")
    ;;
  verify)
    (cd "$ROOT" && dotnet build VoiSlate.slnx -c Debug --no-restore 2>&1 | tail -3)
    (cd "$ROOT" && dotnet test VoiSlate.slnx --no-build 2>&1 | tail -1)
    ;;
  *)
    echo "unknown step: $step"; exit 1;;
esac
echo "== done: $step =="