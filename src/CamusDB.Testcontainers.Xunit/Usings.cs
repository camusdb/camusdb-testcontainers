global using System;
global using System.Threading;
global using System.Threading.Tasks;
global using JetBrains.Annotations;
global using Testcontainers.Xunit;
global using Xunit;
global using Xunit.Sdk;
#if XUNIT_V3
global using LifetimeTask = System.Threading.Tasks.ValueTask;
#else
global using Xunit.Abstractions;
global using LifetimeTask = System.Threading.Tasks.Task;
#endif
