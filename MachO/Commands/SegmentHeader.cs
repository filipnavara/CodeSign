namespace CodeSign.MachO.Commands
{
    public partial class SegmentHeader
    {
        public string Name { get; set; } = string.Empty;
        public uint Address { get; set; }
        public uint Size { get; set; }
        public uint FileOffset { get; set; }
        public uint FileSize { get; set; }
        public VmProtection MaximalProtection { get; set; }
        public VmProtection InitialProtection { get; set; }
        public uint NumberOfSections { get; set; }
        public uint Flags { get; set; }
    }
}