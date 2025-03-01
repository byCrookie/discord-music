using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Humanizer;
using Humanizer.Bytes;

namespace DiscordMusic.Core.Utils;

public static class ProcessTree
{
    public static string Tree()
    {
        var processes = Process.GetProcesses()
            .Select(p => new ProcessInfo(p))
            .ToDictionary(p => p.Id);

        foreach (var p in processes.Values)
        {
            if (processes.TryGetValue(p.ParentId, out var value))
            {
                value.Children.Add(p);
            }
        }

        var output = new StringBuilder();
        foreach (var p in processes.Values.Where(p => p.ParentId == 0 || !processes.ContainsKey(p.ParentId)))
        {
            output.AppendLine(PrintTree(p, 0));
        }

        return output.ToString().Trim();
    }

    private static string PrintTree(ProcessInfo p, int indent)
    {
        var output = new StringBuilder();
        output.AppendLine($"{new string(' ', indent * 2)}- {p.Name} (PID: {p.Id}, Mem: {p.MemoryUsage})");
        foreach (var child in p.Children.OrderBy(c => c.Id))
        {
            output.AppendLine(PrintTree(child, indent + 1));
        }

        return output.ToString();
    }

    private class ProcessInfo(Process process)
    {
        public int Id { get; } = process.Id;
        public int ParentId { get; } = GetParentProcessId(process);
        public string Name { get; } = process.ProcessName;
        public ByteSize MemoryUsage { get; } = GetMemoryUsage(process);
        public List<ProcessInfo> Children { get; } = [];

        private static ByteSize GetMemoryUsage(Process process)
        {
            try
            {
                return process.WorkingSet64.Bytes();
            }
            catch
            {
                return 0.Bytes();
            }
        }

        private static int GetParentProcessId(Process process)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return 0;
            }

            try
            {
                var statFile = $"/proc/{process.Id}/stat";
                if (File.Exists(statFile))
                {
                    var stat = File.ReadAllText(statFile).Split(' ');
                    return int.Parse(stat[3]);
                }
            }
            catch
            {
                // ignored
            }

            return 0;
        }
    }
}
