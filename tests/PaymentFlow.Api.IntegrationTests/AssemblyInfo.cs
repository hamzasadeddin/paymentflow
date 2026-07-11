using Xunit;

// Each integration test class spins up its own WebApplicationFactory host. xUnit
// runs test classes in parallel by default, which makes the fixtures race on the
// shared HostFactoryResolver machinery and intermittently fail with
// "The entry point exited without ever building an IHost". Serializing the classes
// makes each host build one at a time and removes the flakiness.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
