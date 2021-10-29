namespace Melanzana.MachO.Commands
{
    public class LinkEdit : LoadCommand
    {
        private readonly LinkEditHeader linkEditHeader;

        public LinkEdit(LoadCommandHeader header, LinkEditHeader linkEditHeader)
            : base(header)
        {
            this.linkEditHeader = linkEditHeader;
        }

        public LinkEdit(LoadCommandType commandType, uint fileOffset, uint fileSize)
            : base(new LoadCommandHeader { CommandType = commandType, CommandSize = LoadCommandHeader.BinarySize + LinkEditHeader.BinarySize })
        {
            this.linkEditHeader = new LinkEditHeader
            {
                FileOffset = fileOffset,
                FileSize = fileSize,
            };
        }

        public LinkEditHeader LinkEditHeader => linkEditHeader;
    }
}