namespace Melanzana.MachO.Commands
{
    public class UnsupportedLoadCommand : LoadCommand
    {
        public UnsupportedLoadCommand(LoadCommandHeader header, byte[] data)
            : base(header)
        {
            this.Data = data;
        }

        public byte[] Data { get; set; }
    }
}