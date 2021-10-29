namespace Melanzana.MachO.Commands
{
    [GenerateReaderWriter]
    public partial class SegmentHeader
    {
        public MachFixedName Name { get; set; } = MachFixedName.Empty;
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