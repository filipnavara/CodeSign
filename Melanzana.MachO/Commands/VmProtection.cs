namespace Melanzana.MachO.Commands
{
    [Flags]
    public enum VmProtection : uint
    {
        None = 0x0,
        Read = 0x1,
        Write = 0x2,
        Execute = 0x4,
    }
}