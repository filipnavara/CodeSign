namespace Melanzana.MachO
{
    public class LoadCommand
    {
        protected LoadCommand(LoadCommandHeader header)
        {
            this.Header = header;
        }

        public LoadCommandHeader Header { get; set; }
    }
}