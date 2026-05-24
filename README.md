# Actions

Centralized GitHub Actions for use across the AndanteTribe organization.
Marketplace actions are wrapped here so version management is consolidated in this repository and Dependabot keeps pinned SHAs up to date.

## Available Actions

### Wrapper Actions (Marketplace)

| Action | Description | Wraps |
|--------|-------------|-------|
| [`actions/checkout`](./actions/checkout) | Checkout a Git repository | [`actions/checkout`](https://github.com/actions/checkout) |
| [`actions/setup-dotnet`](./actions/setup-dotnet) | Set up a .NET SDK | [`actions/setup-dotnet`](https://github.com/actions/setup-dotnet) |
| [`actions/upload-artifact`](./actions/upload-artifact) | Upload a build artifact | [`actions/upload-artifact`](https://github.com/actions/upload-artifact) |
| [`actions/unity-meta-check`](./actions/unity-meta-check) | Check missing/dangling Unity meta files | [`DeNA/unity-meta-check`](https://github.com/DeNA/unity-meta-check) |
| [`actions/unity-test-runner`](./actions/unity-test-runner) | Run tests for a Unity project | [`game-ci/unity-test-runner`](https://github.com/game-ci/unity-test-runner) |

### Original Actions

| Action | Description |
|--------|-------------|
| [`actions/code-coverage-report-update`](./actions/code-coverage-report-update) | Update Unity test code coverage report to GitHub |
| [`actions/fetch-unity-info`](./actions/fetch-unity-info) | Fetch Unity project name from project path |
| [`actions/get-unity-serial`](./actions/get-unity-serial) | Extract Unity serial from UNITY_LICENSE |
| [`actions/nugetforunity-restore`](./actions/nugetforunity-restore) | Restore NuGet packages using NuGetForUnity CLI |
| [`actions/set-outputs`](./actions/set-outputs) | Write cache-hit outputs for Unity and NuGet cache |
| [`actions/unity-cache`](./actions/unity-cache) | Unity project cache control wrapper |

## Usage

Reference actions using the full path with a pinned commit or `@main`:

```yaml
steps:
  - uses: AndanteTribe/Actions/actions/checkout@main
    with:
      fetch-depth: 0

  - uses: AndanteTribe/Actions/actions/setup-dotnet@main
    with:
      dotnet-version: 9.0.x

  - uses: AndanteTribe/Actions/actions/unity-meta-check@main
    with:
      target_path: Assets

  - uses: AndanteTribe/Actions/actions/unity-cache@main
    with:
      unity-project-path: .

  - uses: AndanteTribe/Actions/actions/unity-test-runner@main
    env:
      UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
      UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
      UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
    with:
      testMode: playmode

  - uses: AndanteTribe/Actions/actions/upload-artifact@main
    with:
      name: my-artifact
      path: artifacts/
```

## Dependabot

[`.github/dependabot.yml`](./.github/dependabot.yml) is configured to automatically open pull requests when pinned SHA versions of wrapped marketplace actions are updated.