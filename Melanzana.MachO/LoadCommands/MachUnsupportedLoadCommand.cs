using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public class MachUnsupportedLoadCommand : MachLoadCommand
    {
        public MachUnsupportedLoadCommand(MachLoadCommandType type, byte[] data)
        {
            this.Type = type;
            this.Data = data;
        }

        public MachLoadCommandType Type { get; set; }

        public byte[] Data { get; set; }
    }
}