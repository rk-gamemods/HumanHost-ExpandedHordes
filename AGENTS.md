# Expanded Hordes development and release workflow

## Issue and pull request completion

- The agent completing or merging an implementation PR owns the disposition of
  every issue it addresses. The work is incomplete until that agent verifies
  the merged result and the resulting GitHub issue states. Do not leave issue
  closure to GitHub automation, a future agent, or a separate user reminder.
- Identify addressed issues and their expected disposition in the PR. After
  merging into `develop`, close each fully resolved issue as `completed`, with
  a comment linking the merged PR or commit and its acceptance evidence. Read
  the issue back from GitHub to verify closure; a closing keyword or successful
  merge is not proof that the issue closed.
- Keep an issue open only for concrete remaining work required by its agreed
  scope. Record the unmet acceptance criteria, next action and any blocker in
  the issue. General alpha play-test checklists do not justify leaving completed
  implementation issues open. Do not claim unperformed runtime checks passed.
- The release agent must reconcile the issues addressed by every PR included
  since the previous published release before Workshop publication, and verify
  their states again before reporting the release complete. Completed shipped
  work must have closed issues; any genuinely unresolved work and whether it
  blocks publication must be explicit.
- Report addressed issue IDs and verified dispositions in the completion
  handoff. If a GitHub operation fails, report the failure and remaining action;
  do not report issue management or release completion as finished.

Use the [development completion checklist](docs/DEVELOPMENT.md#completing-issue-work)
to apply these rules. PR closing keywords for `develop` do not replace them.

## Release workflow

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
