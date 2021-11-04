using Melanzana.MachO.BinaryFormat;

namespace Melanzana.MachO
{
    public abstract class MachLinkEdit : MachLoadCommand
    {
        public uint FileOffset { get; set; }

        public uint FileSize { get; set; }

        public override int GetCommandSize(MachObjectFile objectFile) => LoadCommandHeader.BinarySize + LinkEditHeader.BinarySize;
    }

    public class MachCodeSignature : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.CodeSignature;
    }

    public class MachDylibCodeSigningDirs : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.DylibCodeSigningDRs;
    }

    public class MachSegmentSplitInfo : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.SegmentSplitInfo;
    }

    public class MachFunctionStarts : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.FunctionStarts;
    }

    public class MachDataInCode : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.DataInCode;
    }

    public class MachLinkerOptimizationHint : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.LinkerOptimizationHint;
    }

    public class MachDyldExportsTrie : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.DyldExportsTrie;
    }

    public class MachDyldChainedFixups : MachLinkEdit
    {
        public override MachLoadCommandType GetCommandType(MachObjectFile objectFile) => MachLoadCommandType.DyldChainedFixups;
    }
}