namespace CodeSign.MachO.Commands
{
    public class LinkEdit : LoadCommand
    {
        private readonly LinkEditHeader linkEditHeader;

        public LinkEdit(LoadCommandHeader header, LinkEditHeader linkEditHeader)
            : base(header)
        {
            this.linkEditHeader = linkEditHeader;
        }

        public LinkEditHeader LinkEditHeader => linkEditHeader;
    }
}