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
    }

    public class MachLoadDylibCommand : MachDylibCommand
    {
    }

    public class MachLoadWeakDylibCommand : MachDylibCommand
    {
    }

    public class MachReexportDylibCommand : MachDylibCommand
    {
    }
}
