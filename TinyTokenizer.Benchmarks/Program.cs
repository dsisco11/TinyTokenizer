using BenchmarkDotNet.Running;
using TinyTokenizer.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(LexerBenchmarks).Assembly).Run(args);
