using System.Diagnostics;

namespace Melanzana.MachO
{
    public class MachSymbol
    {
        public string Name { get; set; }
        public ulong Value { get; set; }
        // TODO: Expose all the fields
    }
}