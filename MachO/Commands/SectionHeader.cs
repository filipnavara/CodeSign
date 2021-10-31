namespace CodeSign.MachO.Commands
{
    public partial class SectionHeader
    {
        public string SectionName { get; set; } = string.Empty;
        public string SegmentName { get; set; } = string.Empty;
        public uint Address { get; set; }
        public uint Size { get; set; }
        public uint FileOffset { get; set; }
        public uint Alignment { get; set; }
        public uint RelocationOffset { get; set; }
        public uint NumberOfReloationEntries { get; set; }
        public uint Flags { get; set; }
        public uint Reserved1 { get; set; }
        public uint Reserved2 { get; set; }
        public uint Reserved3 { get; set; }

        public SectionType Type => (SectionType)(Flags & 0xff);
    }
}