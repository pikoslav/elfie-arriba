﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace V5.Test.Performance
{
    /// <summary>
    ///  Benchmarker provides a simple interface for logging performance results quickly.
    ///  It automatically chooses an iteration count to get a reasonable measurement.
    ///  It logs via Trace.WriteLine and to a TSV file to keep historical data.
    ///  MeasureParallel can be used to test multi-threaded work for methods which take an index and length to share work in an array.
    ///
    ///  To validate the work, make the method return an object. ToString will be called on it.
    ///  Make sure that verification is done *in* ToString to avoid measuring the verification work.
    ///  
    ///  Usage:
    ///  using(Benchmarker b = new Benchmarker("HashSet Scenarios"))
    ///  {
    ///      b.Measure("Clear", () => set.Clear);
    ///      b.MeasureParallel("Count", (index, length) => set.Count(index, length));
    ///  }
    /// </summary>
    public class Benchmarker : IDisposable
    {
        public const string BenchmarkTsvPath = @"..\..\..\V5.Benchmarks.tsv";
        private StreamWriter _writer;

        public Benchmarker(string groupName)
        {
            _writer = File.AppendText(BenchmarkTsvPath);
            if (_writer.BaseStream.Length == 0) _writer.WriteLine("Name\tOutput\tX1\tX2\tX4");

            WriteHeading($"{groupName} on {Environment.MachineName} @{DateTime.UtcNow:u}");
        }

        private void WriteHeading(string message)
        {
            _writer.WriteLine(message);
            Trace.WriteLine(message);
        }
        
        private static string Pad(string value, int length)
        {
            if (value == null) return new string(' ', length);
            if (value.Length >= length) return value;
            return value + new string(' ', length - value.Length);
        }

        private void WriteEntry(params BenchmarkResult[] results)
        {
            object output = results[0].Output ?? "<null>";
            string outputString = (output is int ? string.Format("{0:n0}", output) : output.ToString());

            _writer.Write($"{results[0].Name}\t{outputString}");
            Trace.Write($" - {Pad(results[0].Name, 40)} -> {Pad(outputString, 15)}");

            foreach (BenchmarkResult r in results)
            {
                _writer.Write($"\t{r.ToResultCount()}");
                Trace.Write($"\t{r.ToResultCount()}");

                if(r.Iterations == 1)
                {
                    _writer.Write($" ({r.Elapsed.TotalMilliseconds:n0}ms)");
                    Trace.Write($" ({r.Elapsed.TotalMilliseconds:n0}ms)");
                }
            }

            _writer.WriteLine();
            Trace.WriteLine("");
        }

        public void Measure(string name, int itemCount, Func<object> method)
        {
            WriteEntry(BenchmarkResult.Measure(name, itemCount, method));
        }

        public void MeasureParallel(string name, int itemCount, Func<int, int, object> method)
        {
            WriteEntry(
                BenchmarkResult.Measure(name, itemCount, () => method(0, itemCount)),
                BenchmarkResult.MeasureParallel(name, itemCount, method, 2),            
                BenchmarkResult.MeasureParallel(name, itemCount, method, 4)
            );
        }

        public void Dispose()
        {
            if(_writer != null)
            {
                _writer.WriteLine();
                Trace.WriteLine("");

                _writer.Dispose();
                _writer = null;
            }
        }
    }

    internal class BenchmarkResult
    {
        public string Name { get; set; }
        public object Output { get; set; }
        public int ItemCount { get; set; }
        public int Iterations { get; set; }
        public TimeSpan Elapsed { get; set; }

        private BenchmarkResult()
        { }

        public string ToResultCount()
        {
            double itemsPerSecond = ((double)this.ItemCount * (double)this.Iterations) / this.Elapsed.TotalSeconds;

            if (itemsPerSecond > (1000 * 1000 * 1000))
            {
                return string.Format("{0:n1} B", itemsPerSecond / (double)(1000 * 1000 * 1000));
            }
            else if (itemsPerSecond > (1000 * 1000))
            {
                return string.Format("{0:n1} M", itemsPerSecond / (double)(1000 * 1000));
            }
            else if (itemsPerSecond > 1000)
            {
                return string.Format("{0:n1} K", itemsPerSecond / (double)(1000));
            }
            else
            {
                return string.Format("{0:n3}", itemsPerSecond / (double)(1000));
            }
        }

        public static BenchmarkResult Measure(string name, int itemCount, Func<object> method)
        {
            Stopwatch w = Stopwatch.StartNew();
            object output = method();
            w.Stop();

            if (w.Elapsed.TotalMilliseconds > 500)
            {
                // If over 500ms, one iteration will do
                return new BenchmarkResult() { Name = name, Output = output, ItemCount = itemCount, Iterations = 1, Elapsed = w.Elapsed };
            }

            int iterations = (w.Elapsed.TotalMilliseconds < 1 ? 1000 : (int)(500 / w.Elapsed.TotalMilliseconds));
            w.Restart();

            for (int iteration = 0; iteration < iterations; ++iteration)
            {
                output = method();
            }

            w.Stop();
            return new BenchmarkResult() { Name = name, Output = output, ItemCount = itemCount, Iterations = iterations, Elapsed = w.Elapsed };
        }

        public static BenchmarkResult MeasureParallel(string name, int itemCount, Func<int, int, object> method, int parallelCount)
        {
            return Measure(name, itemCount, () => RunParallel(itemCount, method, parallelCount));
        }

        private static object RunParallel(int itemCount, Func<int, int, object> method, int parallelCount)
        {
            object output = null;
            int segmentLength = ParallelLengthPart(itemCount, parallelCount);

            Parallel.For(0, parallelCount, (i) =>
            {
                int offset = i * segmentLength;
                int length = (i == parallelCount - 1 ? itemCount - offset : segmentLength);

                output = method(offset, length);
            });

            return output;
        }

        public static int ParallelLengthPart(int totalCount, int parallelCount)
        {
            int portionLength = totalCount / parallelCount;
            if ((portionLength & 63) != 0) portionLength = 64 + portionLength & ~63;
            return portionLength;
        }
    }

    internal class BenchmarkLogger
    {
        public const string BenchmarkTsvPath = @"..\..\..\V5.Benchmarks.tsv";
        private StreamWriter _writer;

        public BenchmarkLogger()
        {
            _writer = File.AppendText(BenchmarkTsvPath);
            LogSessionStart();
        }

        private void WriteLine(string message = "")
        {
            _writer.WriteLine(message);
            Trace.WriteLine(" - " + message);
        }

        private void LogSessionStart()
        {
            // Write column headings if new file
            if (_writer.BaseStream.Length == 0)
            {
                _writer.WriteLine("Name\tOutput\tX1\tX2\tX4");
            }

            // Get the current git commit
            //ProcessStartInfo psi = new ProcessStartInfo();
            //psi.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            //psi.Arguments = "/K git rev-parse --short HEAD";
            //psi.RedirectStandardOutput = true;
            //psi.UseShellExecute = false;
            //psi.WorkingDirectory = Environment.CurrentDirectory;

            //Process git = Process.Start(psi);
            //string nearestGitCommit = git.StandardOutput.ReadToEnd();
            //git.WaitForExit(1000);

            // Log the session
            WriteLine();
            WriteLine($"{DateTime.UtcNow:u}\t{Environment.MachineName}");//\t{nearestGitCommit}");
        }

        public void LogGroupStart(string description)
        {
            WriteLine();
            WriteLine(description);
        }

        internal void LogResult(BenchmarkResult result)
        {
            WriteLine($"{result.Name}\t{(result.Output ?? "<null>").ToString()}\t{result.ToResultCount()}\t{result.Elapsed.TotalMilliseconds:n0}ms\t{result.Iterations:n0}");
        }

        internal void LogResult(BenchmarkResult x1, BenchmarkResult x2, BenchmarkResult x4)
        {
            WriteLine($"{x1.Name}\t{(x1.Output ?? "<null>").ToString()}\t{x1.ToResultCount()}\t{x2.ToResultCount()}\t{x4.ToResultCount()}");
        }
    }
}
