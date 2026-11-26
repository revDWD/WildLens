#!/usr/bin/env bash
# Compile-checks WildLens outside Unity in five configurations (needs the .NET SDK 6+ and internet once).
# Usage: Tools~/CompileCheck/compile-check.sh        Exit code 0 = all configurations compiled.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
ref="${WILDLENS_UNITYREF:-$HOME/.cache/wildlens-unityref}"
fetch() { # id version dir
  if [ ! -d "$ref/$3" ]; then
    mkdir -p "$ref/$3"
    curl -fsSL "https://api.nuget.org/v3-flatcontainer/$1/$2/$1.$2.nupkg" -o "$ref/$3.nupkg"
    (cd "$ref/$3" && unzip -q -o "../$3.nupkg")
  fi
}
fetch unityengine.modules 2021.3.33 mods   # UnityEngine module DLLs (runtime API)
fetch unity3d.sdk 2021.1.14.1 sdk          # UnityEditor.dll (editor API)

cd "$here"
base="UNITY_2021_3_OR_NEWER"
u6="$base;UNITY_2022_2_OR_NEWER;UNITY_2023_1_OR_NEWER"
build() { # part config defines [runtimeDll]
  printf '%-22s' "$2"
  if dotnet build Check.csproj -nologo -v q -p:Part="$1" -p:Config="$2" -p:Defines="${3//;/%3B}" -p:UnityRef="$ref" \
       -p:RuntimeDll="${4:-}" > "obj-$2.log" 2>&1; then echo "ok"; else echo "FAILED"; grep -E "error" "obj-$2.log" | sort -u | head -20; exit 1; fi
}
build Runtime Legacy      "$base;ENABLE_LEGACY_INPUT_MANAGER"
build Runtime InputSystem "$base;ENABLE_INPUT_SYSTEM;WILDLENS_INPUT_SYSTEM"
build Runtime Unity6URP   "$u6;ENABLE_INPUT_SYSTEM;WILDLENS_INPUT_SYSTEM;WILDLENS_INPUT_SYSTEM_1_9;WILDLENS_URP"
build Editor  Editor      "$base;UNITY_EDITOR"                    "$here/bin/Legacy/WildLens.Runtime.Legacy.dll"
build Editor  Editor6URP  "$u6;UNITY_EDITOR;WILDLENS_URP"         "$here/bin/Unity6URP/WildLens.Runtime.Unity6URP.dll"
rm -f obj-*.log
echo "All configurations compiled."
