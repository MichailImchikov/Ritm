# Shared by post-merge / post-checkout / post-rewrite (sourced, not run directly).
# Rebuilds the desktop app via desktop/build.ps1 when its sources changed
# between two commits. Never fails the git operation: post-* hooks only report.
#
# Usage: ritm_rebuild <old-commit> <new-commit|empty for working tree> <reason>
# Set RITM_SKIP_AUTOBUILD=1 to skip once, e.g. RITM_SKIP_AUTOBUILD=1 git pull

ritm_rebuild() {
	old=$1 new=$2 reason=$3
	if [ "${RITM_SKIP_AUTOBUILD:-0}" = 1 ]; then
		echo "[ritm] автосборка пропущена (RITM_SKIP_AUTOBUILD=1)"
		return 0
	fi
	root=$(git rev-parse --show-toplevel 2>/dev/null) || return 0
	script="$root/desktop/build.ps1"
	[ -f "$script" ] || return 0

	# Same inputs build.ps1 uses: top-level desktop/*.cs plus manifest, config, icon and the script itself
	if [ -n "$old" ] && git cat-file -e "$old^{commit}" 2>/dev/null; then
		changed=$(git diff --name-only "$old" $new -- desktop |
			grep -E '^desktop/([^/]+\.cs|build\.ps1|app\.manifest|Ritm\.exe\.config|ritm\.ico)$')
		if [ -z "$changed" ]; then
			echo "[ritm] $reason: исходники desktop не менялись — пересборка не нужна"
			return 0
		fi
		count=$(printf '%s\n' "$changed" | wc -l | tr -d ' ')
		echo "[ritm] $reason: изменено файлов desktop: $count — пересобираю Ритм…"
		printf '%s\n' "$changed" | head -n 10 | sed 's/^/[ritm]   /'
		[ "$count" -gt 10 ] && echo "[ritm]   …и ещё $((count - 10))"
	else
		echo "[ritm] $reason: не удалось определить изменения — пересобираю Ритм…"
	fi

	if command -v cygpath >/dev/null 2>&1; then script=$(cygpath -w "$script"); fi
	started=$(date +%s)
	if output=$(powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$script" 2>&1); then
		echo "[ritm] сборка прошла за $(( $(date +%s) - started )) с: $(printf '%s\n' "$output" | tail -n 1)"
	else
		printf '%s\n' "$output" | sed 's/^/[ritm]   /'
		echo "[ritm] ОШИБКА сборки — git-операция выполнена, исправьте код и запустите desktop/build.ps1 вручную"
	fi
	return 0
}
