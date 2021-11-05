using System.Text;
using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public class MachEntrypointCommand : MachLoadCommand
    {
        public ulong FileOffset { get; set; }

        public ulong StackSize { get; set; }

        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.Main;

        public override int GetCommandSize(MachObjectFile objectFile)
            => LoadCommandHeader.BinarySize + MainCommandHeader.BinarySize;
    }
}
