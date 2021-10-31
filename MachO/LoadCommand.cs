namespace CodeSign.MachO
{
    public class LoadCommand
    {
        public LoadCommand(LoadCommandHeader header)
        {
            this.Header = header;
        }

        public LoadCommandHeader Header { get; set; }
    }
}