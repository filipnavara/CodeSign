namespace Melanzana.MachO
{
    [GenerateReaderWriter]
    public partial class MachHeader64 : IMachHeader
    {
        public MachCpuType CpuType { get; set; }
        public uint CpuSubType { get; set; }
        public MachFileType FileType { get; set; }
        public uint NumberOfCommands { get; set; }
        public uint SizeOfCommands { get; set; }
        public MachHeaderFlags Flags { get; set; }
        public uint Reserved { get; set; }
    }
}