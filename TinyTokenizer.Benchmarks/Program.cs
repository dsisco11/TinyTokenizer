using BenchmarkDotNet.Running;
using TinyTokenizer.Benchmarks;

BenchmarkSwitcher.FromTypes([
	typeof(NewlineQueryBenchmarks)
]).Run(args);
