namespace Melanzana.MachO.Commands
{
    public interface ISegment
    {
        string Name { get; }
        ulong Address { get; }
        ulong Size { get; }
        ulong FileOffset { get; }
        ulong FileSize { get; }
        VmProtection MaximalProtection { get; }
        VmProtection InitialProtection { get; }
        uint Flags { get; }
    }
}