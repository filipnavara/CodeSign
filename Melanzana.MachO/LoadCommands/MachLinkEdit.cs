using System.Diagnostics;

namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        public uint FileOffset => Data.FileOffset;

        public uint FileSize => (uint)Data.Size;

        public MachLinkEditData Data { get; init; }
    }
}