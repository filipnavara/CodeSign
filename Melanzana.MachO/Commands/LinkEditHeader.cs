namespace Melanzana.MachO.Commands
{
    [GenerateReaderWriter]
    public partial class LinkEditHeader
    {
        public uint FileOffset { get; set; }
        public uint FileSize { get; set; }
    }
}