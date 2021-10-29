namespace Melanzana.MachO.Commands
{
    [GenerateReaderWriter]
    public partial class Segment64Header
    {
        public MachFixedName Name { get; set; } = MachFixedName.Empty;
        public ulong Address { get; set; }
        public ulong Size { get; set; }
        public ulong FileOffset { get; set; }
        public ulong FileSize { get; set; }
        public VmProtection MaximalProtection { get; set; }
        public VmProtection InitialProtection { get; set; }
        public uint NumberOfSections { get; set; }
        public uint Flags { get; set; }
    }
}