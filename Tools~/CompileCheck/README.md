# Compile check (no Unity needed)

`compile-check.sh` builds the package's C# in five configurations with the .NET SDK, so you (or an AI agent working
without the Unity Editor) can catch compile errors before committing:

| Configuration | Simulates |
|---|---|
| Legacy | Unity 2021.3, Built-in pipeline, legacy Input Manager |
| InputSystem | Unity 2021.3 with the Input System package |
| Unity6URP | Unity 6 (2023.1+ APIs), URP, Input System 1.9+ |
| Editor | Editor assembly against the Legacy runtime |
| Editor6URP | Editor assembly, Unity 6 defines, URP |

It downloads reference assemblies from NuGet once (`UnityEngine.Modules` 2021.3.33 and `Unity3D.SDK` 2021.1.14.1) into
`~/.cache/wildlens-unityref` (override with `WILDLENS_UNITYREF`). uGUI, the Input System and URP aren't on NuGet, so
`stubs/` declares just the members WildLens uses. **If you use a new member of those APIs, add it to a stub** (an
error mentioning `UnityEngine.UI`, `InputSystem` or `Rendering.Universal` usually means a missing stub, not a bug).

Limits: Unity 6-only APIs are checked against 2021 assemblies, so code behind `UNITY_2022_2_OR_NEWER`/`UNITY_2023_1_OR_NEWER`
must stick to APIs that exist in both, or use reflection (as `TerrainPainter.Modern` does). This proves the code
compiles; it does not prove it works. Playtest in Unity before releasing.

```bash
Tools~/CompileCheck/compile-check.sh
```
