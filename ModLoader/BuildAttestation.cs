using System.Linq;
using System.Reflection;

namespace ModManager
{
    internal static class BuildAttestation
    {
        public static readonly string Value =
            typeof(BuildAttestation).Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "BuildAttestation")?.Value ?? "";
    }
}