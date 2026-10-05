# Lifecycle regression harness

Run from the package root with the .NET 8 SDK or later:

```sh
dotnet run --project Tests~/RegressionHarness/RegressionHarness.csproj
```

The harness compiles the actual runtime sources, excluding the VContainer integration.
It uses deterministic Unity/Addressables stand-ins and a Task-backed UniTask adapter.
No NuGet packages or network access are required. The `Tests~` folder is ignored by Unity.

It checks completed and shared asset loads, cancellation, startup/operation failures,
retry, scope ownership, diagnostics, progress listeners, scene activation/concurrent
unloading/retry, and instance disposal. These are lifecycle regression checks, not
Unity Play Mode tests: they do not validate the engine's PlayerLoop, native scene
queue, bundle IO, provider behavior, or real UniTask cancellation scheduling.

Production sources were also compiled separately against the project's installed
Unity 6000.3.16f1, Addressables 2.9.1, UniTask and VContainer assemblies. Runtime,
Editor and VContainer assemblies compiled successfully. Unity 2021.3 and the
minimum declared Addressables 1.21.21 version were not executed in this verification.
