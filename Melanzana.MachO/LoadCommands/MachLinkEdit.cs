using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        public uint FileOffset { get; set; }

        public uint FileSize { get; set; }
    }
}