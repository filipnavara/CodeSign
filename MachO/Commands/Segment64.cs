namespace CodeSign.MachO.Commands
{
    public class Segment64 : LoadCommand, ISegment
    {
        private readonly Segment64Header segmentHeader;
        private readonly Section64Header[] section64Headers;

        public Segment64(LoadCommandHeader header, Segment64Header segmentHeader, Section64Header[] section64Headers)
            : base(header)
        {
            this.segmentHeader = segmentHeader;
            this.section64Headers = section64Headers;
        }

        public Segment64Header SegmentHeader => segmentHeader;

        public IReadOnlyList<Section64Header> Sections => section64Headers;

        public string Name => SegmentHeader.Name;

        public ulong Address => SegmentHeader.Address;

        public ulong Size => SegmentHeader.Size;

        public ulong FileOffset => SegmentHeader.FileOffset;

        public ulong FileSize => SegmentHeader.FileSize;

        public VmProtection MaximalProtection => SegmentHeader.MaximalProtection;

        public VmProtection InitialProtection => SegmentHeader.InitialProtection;

        public uint Flags => SegmentHeader.Flags;
    }
}