#!/usr/bin/env bash
#
# the-shedding — 개발 환경 초기 설정 스크립트
#
# 저장소를 clone한 뒤 한 번만 실행하세요.
#   ./setup-lfs.sh
#
# 하는 일:
#   1. Git LFS 설치 확인 및 활성화  (필수 — 안 하면 이미지가 깨져 보입니다)
#   2. Unity 씬 머지 도구 등록      (권장 — 씬/프리팹 충돌 시 자동 머지)
#
# 이 설정들은 각자 로컬 .git/config 에 저장되므로 커밋되지 않습니다.
# 그래서 팀원 각자가 한 번씩 실행해야 합니다.

set -uo pipefail

BOLD=$'\033[1m'; RED=$'\033[31m'; GREEN=$'\033[32m'; YELLOW=$'\033[33m'; DIM=$'\033[2m'; OFF=$'\033[0m'
ok()   { printf '%s✓%s %s\n' "$GREEN" "$OFF" "$1"; }
warn() { printf '%s!%s %s\n' "$YELLOW" "$OFF" "$1"; }
fail() { printf '%s✗%s %s\n' "$RED" "$OFF" "$1"; }
step() { printf '\n%s%s%s\n' "$BOLD" "$1" "$OFF"; }
dim()  { printf '%s  %s%s\n' "$DIM" "$1" "$OFF"; }

# 저장소 루트에서 실행되도록 보정
cd "$(dirname "$0")" || exit 1
if ! git rev-parse --git-dir >/dev/null 2>&1; then
  fail "git 저장소가 아닙니다. 저장소 안에서 실행해주세요."
  exit 1
fi

WARNINGS=0

# ---------------------------------------------------------------------------
# 1. Git LFS
# ---------------------------------------------------------------------------
step "[1/2] Git LFS 설정"

if ! git lfs version >/dev/null 2>&1; then
  fail "git-lfs 가 설치되어 있지 않습니다."
  echo
  echo "  아래 명령으로 설치한 뒤 이 스크립트를 다시 실행해주세요."
  echo
  case "$(uname -s)" in
    Darwin)            echo "    brew install git-lfs" ;;
    Linux)             echo "    sudo apt install git-lfs    # 또는 dnf/pacman" ;;
    MINGW*|MSYS*|CYGWIN*) echo "    https://git-lfs.com 에서 설치 프로그램 다운로드" ;;
    *)                 echo "    https://git-lfs.com" ;;
  esac
  echo
  exit 1
fi
ok "git-lfs 설치됨 ($(git lfs version | awk '{print $1}'))"

if git lfs install --local >/dev/null 2>&1; then
  ok "저장소에 LFS 활성화됨"
else
  fail "git lfs install 실패"
  exit 1
fi

# 포인터만 있고 실제 파일이 없는 경우 내려받기
echo "  LFS 파일 내려받는 중..."
if git lfs pull >/dev/null 2>&1; then
  ok "LFS 파일 동기화 완료"
else
  warn "git lfs pull 실패 — 네트워크나 원격 저장소 접근 권한을 확인해주세요."
  WARNINGS=$((WARNINGS + 1))
fi

# ---------------------------------------------------------------------------
# 2. UnityYAMLMerge (씬/프리팹 머지 도구)
# ---------------------------------------------------------------------------
step "[2/2] Unity 씬 머지 도구 설정"

# 프로젝트가 요구하는 Unity 버전 — 같은 버전을 최우선으로 고른다
PROJECT_VERSION=""
if [ -f ProjectSettings/ProjectVersion.txt ]; then
  PROJECT_VERSION=$(awk '/^m_EditorVersion:/ {print $2}' ProjectSettings/ProjectVersion.txt)
  [ -n "$PROJECT_VERSION" ] && dim "프로젝트 Unity 버전: $PROJECT_VERSION"
fi

# 플랫폼별 후보 경로.
# Unity 6 부터 Contents/Tools -> Contents/Helpers 로 위치가 바뀌었으므로 둘 다 확인한다.
CANDIDATES=()
case "$(uname -s)" in
  Darwin)
    for base in \
      "/Applications/Unity/Hub/Editor"/*/Unity.app/Contents \
      "/Applications/Unity"/*/Unity.app/Contents \
      "/Applications/Unity/Unity.app/Contents" \
      "$HOME/Applications/Unity/Hub/Editor"/*/Unity.app/Contents
    do
      [ -d "$base" ] || continue
      CANDIDATES+=("$base/Helpers/UnityYAMLMerge" "$base/Tools/UnityYAMLMerge")
    done
    ;;
  MINGW*|MSYS*|CYGWIN*)
    for base in \
      "/c/Program Files/Unity/Hub/Editor"/*/Editor/Data \
      "/c/Program Files/Unity/Editor/Data" \
      "/c/Program Files (x86)/Unity/Editor/Data"
    do
      [ -d "$base" ] || continue
      CANDIDATES+=("$base/Tools/UnityYAMLMerge.exe")
    done
    ;;
  Linux)
    for base in \
      "$HOME/Unity/Hub/Editor"/*/Editor/Data \
      "/opt/unity/Editor/Data"
    do
      [ -d "$base" ] || continue
      CANDIDATES+=("$base/Tools/UnityYAMLMerge")
    done
    ;;
esac

# 실제로 존재하는 실행 파일만 남기고, 프로젝트 버전과 일치하는 것을 앞으로
FOUND=()
for c in "${CANDIDATES[@]:-}"; do
  [ -n "$c" ] && [ -x "$c" ] && FOUND+=("$c")
done

MERGE_BIN=""
if [ -n "$PROJECT_VERSION" ]; then
  for f in "${FOUND[@]:-}"; do
    case "$f" in *"$PROJECT_VERSION"*) MERGE_BIN="$f"; break ;; esac
  done
fi
[ -z "$MERGE_BIN" ] && [ "${#FOUND[@]}" -gt 0 ] && MERGE_BIN="${FOUND[0]}"

if [ -z "$MERGE_BIN" ]; then
  warn "UnityYAMLMerge 를 찾지 못했습니다. (Unity가 설치되어 있나요?)"
  echo
  echo "  이 단계는 선택 사항이라 건너뛰어도 LFS는 정상 동작합니다."
  echo "  직접 등록하려면 아래로 경로를 찾은 뒤,"
  echo
  echo "    find /Applications -name 'UnityYAMLMerge*' 2>/dev/null"
  echo
  echo "  이 스크립트 안의 CANDIDATES 목록에 그 경로를 추가하거나 아래를 실행하세요."
  echo
  echo "    git config merge.unityyamlmerge.name \"Unity SmartMerge\""
  echo "    git config merge.unityyamlmerge.driver '<찾은경로> merge -p %O %A %B %A'"
  echo "    git config merge.unityyamlmerge.recursive binary"
  echo
  WARNINGS=$((WARNINGS + 1))
else
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "\"$MERGE_BIN\" merge -p %O %A %B %A"
  git config merge.unityyamlmerge.recursive binary
  ok "머지 도구 등록됨"
  dim "$MERGE_BIN"

  if [ -n "$PROJECT_VERSION" ]; then
    case "$MERGE_BIN" in
      *"$PROJECT_VERSION"*) : ;;
      *) warn "프로젝트($PROJECT_VERSION)와 다른 버전의 Unity 를 사용합니다. 머지는 동작하지만 버전을 맞추는 편이 안전합니다."
         WARNINGS=$((WARNINGS + 1)) ;;
    esac
  fi
fi

# ---------------------------------------------------------------------------
# 검증
# ---------------------------------------------------------------------------
step "설정 확인"

LFS_COUNT=$(git lfs ls-files 2>/dev/null | wc -l | tr -d ' ')
ok "LFS 관리 파일 ${LFS_COUNT}개"

# 포인터가 실제 파일로 풀렸는지 표본 검사 — 포인터 파일은 200바이트 남짓이다
SAMPLE=$(git lfs ls-files -n 2>/dev/null | head -1)
if [ -n "$SAMPLE" ] && [ -f "$SAMPLE" ]; then
  if head -c 40 "$SAMPLE" | grep -q "git-lfs.github.com/spec"; then
    fail "실제 파일이 아직 내려받아지지 않았습니다 (포인터 상태)."
    echo "    git lfs pull 을 다시 실행해주세요."
    WARNINGS=$((WARNINGS + 1))
  else
    ok "실제 파일 정상 (예: $(basename "$SAMPLE"))"
  fi
fi

echo
if [ "$WARNINGS" -eq 0 ]; then
  printf '%s%s설정 완료. Unity 를 열어 작업을 시작하세요.%s\n\n' "$BOLD" "$GREEN" "$OFF"
else
  printf '%s%s설정 완료 (확인 필요 %d건 — 위 메시지를 봐주세요).%s\n\n' "$BOLD" "$YELLOW" "$WARNINGS" "$OFF"
fi
