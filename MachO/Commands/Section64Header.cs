namespace CodeSign.MachO.Commands
{
    public partial class Section64Header
    {
        public string SectionName { get; set; } = string.Empty;
        public string SegmentName { get; set; } = string.Empty;
        public ulong Address { get; set; }
        public ulong Size { get; set; }
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