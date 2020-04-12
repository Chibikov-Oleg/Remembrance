using System;
using Scar.Common;

namespace Remembrance.Contracts.ProcessMonitoring.Data
{
    public sealed class ProcessInfo
    {
        public ProcessInfo(string name, string? filePath = null)
        {
            _ = name ?? throw new ArgumentNullException(nameof(name));

            Name = name.Capitalize();
            FilePath = filePath;
        }

        public string? FilePath { get; }

        public string Name { get; }

        public override bool Equals(object obj)
        {
            return obj is ProcessInfo processInfo && Equals(processInfo);
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
        }

        public override string ToString()
        {
            return Name;
        }

        private bool Equals(ProcessInfo other)
        {
            return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
