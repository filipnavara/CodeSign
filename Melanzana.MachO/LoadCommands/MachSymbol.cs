using System.Diagnostics;

namespace Melanzana.MachO
{
    public class MachSymbol
    {
        public string Name { get; set; } = string.Empty;
        public ulong Value { get; set; }
        // TODO: Expose all the fields
    }
}