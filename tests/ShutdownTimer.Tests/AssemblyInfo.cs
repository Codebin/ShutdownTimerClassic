using Xunit;

// 本地化用例会改全局 UI 文化（CultureInfo.CurrentUICulture），
// 并行执行会让用例互相干扰，这里强制串行。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
