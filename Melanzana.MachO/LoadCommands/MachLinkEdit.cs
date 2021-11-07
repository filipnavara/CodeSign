using System.Diagnostics;

namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        public uint FileOffset { get; set; }

        public uint FileSize { get; set; }

        internal void Validate(MachSegment linkEditSegment)
        {
            Debug.Assert(FileOffset >= linkEditSegment.FileOffset);
            Debug.Assert(FileOffset + FileSize <= linkEditSegment.FileOffset + linkEditSegment.FileSize);
        }
    }
}