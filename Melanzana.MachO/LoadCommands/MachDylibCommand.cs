using System.Text;
using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public abstract class MachDylibCommand : MachLoadCommand
    {
        public string Name { get; set; }

        public uint Timestamp { get; set; }

        public uint CurrentVersion { get; set; }

        public uint CompatibilityVersion { get; set; }

        private int AlignedSize(int size, bool is64bit)
            => is64bit ? (size + 7) & ~7 : (size + 3) & ~3;

        public override int GetCommandSize(MachObjectFile objectFile)
            => AlignedSize(LoadCommandHeader.BinarySize + DylibCommandHeader.BinarySize + Encoding.UTF8.GetByteCount(Name) + 1, objectFile.Is64Bit);
    }

    public class MachLoadDylibCommand : MachDylibCommand
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.LoadDylib;
    }

    public class MachLoadWeakDylibCommand : MachDylibCommand
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.LoadWeakDylib;
    }

    public class MachReexportDylibCommand : MachDylibCommand
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.ReexportDylib;
    }
}
