namespace CodeSign.MachO.Commands
{
    public class Segment : LoadCommand, ISegment
    {
        private readonly SegmentHeader segmentHeader;
        private readonly SectionHeader[] sectionHeaders;

        public Segment(LoadCommandHeader header, SegmentHeader segmentHeader, SectionHeader[] sectionHeaders)
            : base(header)
        {
            this.segmentHeader = segmentHeader;
            this.sectionHeaders = sectionHeaders;
        }

        public SegmentHeader SegmentHeader => segmentHeader;

        public IReadOnlyList<SectionHeader> Sections => sectionHeaders;

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