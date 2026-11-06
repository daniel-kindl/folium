# Folium quality gates

Folium follows [dkstack's coding standards](https://github.com/daniel-kindl/dkstack/blob/v1/coding/index.md). All enabled Roslyn diagnostics are errors. Do not suppress a rule to make CI pass.

## Static analysis

- `dotnet format --verify-no-changes --no-restore`
- `dotnet build --configuration Release --no-restore`
- `AnalysisMode=All` enables the broad built-in analyzer set.
- `GenerateDocumentationFile` is on so IDE0005 can run on build. See [dotnet/roslyn 41640](https://github.com/dotnet/roslyn/issues/41640).
- Folium does not require XML documentation comments. `NoWarn` includes CS1591 only. `dotnet format` fails when the compiler reports that warning.
- Meziantou.Analyzer and Roslynator.Analyzers are applied across projects.
- xUnit v3 already includes xunit.analyzers as a dependency.
- `CodeMetricsConfig.txt` defines thresholds for CA1501/1502/1505/1506; all are errors.
- The repository uses central package versions and NuGet lock files. When dependencies change, intentionally regenerate and commit all affected `packages.lock.json` files. CI keeps `--locked-mode`.

## Coverage

The `build` CI job runs tests with the Microsoft Testing Platform.
Coverlet writes a Cobertura report for `tests/Folium.Tests`.
`tools/check-coverage.py` gates C# in the production `Folium` assembly:

- Line coverage at least 80%.
- Branch coverage at least 70%.
- Missing, ambiguous or malformed coverage reports fail.
- `.axaml` files are not in the rate. The unit tests do not load the view.

These are minimum floors, not substitutes for meaningful assertions.

## Mutation testing

The `mutation` CI job installs pinned Stryker.NET 5.0.0 and runs from
`tests/Folium.Tests`. It uses the Microsoft Testing Platform runner.
VSTest does not activate mutants in this xUnit project.
`MainView` loads its XAML with `AvaloniaXamlLoader`.
The mutation compile does not run the Avalonia source generator.
Its config mutates only `Books` and `Storage` source files.
The mutation score must be at least 70%. An 85% score is the preferred target.

A surviving mutant is a prompt to inspect test effectiveness; write a useful test or
explain why the mutation is equivalent. Do not bypass the gate.

## CRAP

The `crap` CI job checks out a pinned commit of
[microsoft/crap4csharp](https://github.com/microsoft/crap4csharp).
The job passes each `.cs` file under `src/Folium` to the tool.
A directory argument reads `<directory>/src`. Folium has no such folder. An empty file list exits 0.
The tool's method CRAP threshold is 8. Exit code 2 means a method exceeds that threshold.
The tool creates and analyzes its own Cobertura coverage report.

The CRAP tool excludes constructors and reports unmeasured members separately.
Investigate those gaps; a missing measurement is not evidence of safety.

## Merge requirements

All required CI jobs (build/coverage, mutation, CRAP) must pass. PR #14 is
stacked on #13 and must be retargeted to `main` after #13 merges.
Android UI usability also needs an emulator/device smoke test.

The first run after enabling the gates may be red. Fix root causes, not thresholds.
