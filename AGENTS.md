# Expanded Hordes release workflow

- Integrate changes into `develop` first. Merge `develop` into `main` before
  releasing, then fast-forward `develop` to the same commit as `main`.
- Publish Steam Workshop updates only from `main`. Never publish from
  `develop`, a feature branch, or a detached test checkout.
- Before publication, require a clean `main` checkout at `origin/main` and
  verify that `develop` and `main` identify the same commit. Build the release
  payload from that checkout and verify its identity and hashes.
- Keep the plugin version in `src/ModIdentity.cs` consistent with the package
  version in `src/ExpandedHordes.csproj`.
- Preserve the tested gameplay changes during release preparation. Do not
  distribute game assemblies, decompiled game code, local configuration or logs.
