namespace Melanzana.MachO
{
    [GenerateReaderWriter]
    public partial class FatHeader
    {
        public uint NumberOfFatArchitectures { get; set; }
    }
}