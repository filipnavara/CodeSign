namespace Melanzana.MachO
{
    [GenerateReaderWriter]
    public partial class LoadCommandHeader
    {
        public LoadCommandType CommandType { get; set; }
        public uint CommandSize { get; set; }
    }
}