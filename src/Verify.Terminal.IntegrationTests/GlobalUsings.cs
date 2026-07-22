global using System.Reflection;
global using Shouldly;
global using Spectre.IO;
global using Verify.Terminal;
global using VerifyTests;
global using VerifyXunit;
global using Xunit;

// Verify keeps global static naming/uniqueness state and these tests hit the real filesystem, so run
// them serially to keep scenarios isolated.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
