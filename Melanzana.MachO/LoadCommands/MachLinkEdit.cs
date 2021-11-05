using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        public uint FileOffset { get; set; }

        public uint FileSize { get; set; }
    }

    public class MachCodeSignature : MachLinkEdit
    {
    }

    public class MachDylibCodeSigningDirs : MachLinkEdit
    {
    }

    public class MachSegmentSplitInfo : MachLinkEdit
    {
    }

    public class MachFunctionStarts : MachLinkEdit
    {
    }

    public class MachDataInCode : MachLinkEdit
    {
    }

    public class MachLinkerOptimizationHint : MachLinkEdit
    {
    }

    public class MachDyldExportsTrie : MachLinkEdit
    {
    }

    public class MachDyldChainedFixups : MachLinkEdit
    {
    }
}