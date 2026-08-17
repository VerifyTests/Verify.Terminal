global using System.Reflection;
global using Shouldly;
global using Spectre.IO;
global using Verify.Terminal;
global using VerifyTests;
global using VerifyXunit;
global using Xunit;
global using Xunit.Sdk;
global using Xunit.v3;

// Verify keeps global static naming/uniqueness state and these tests hit the real filesystem, so run
// them serially to keep scenarios isolated.
[assembly: Parallelization(Mode = ParallelMode.None)]
