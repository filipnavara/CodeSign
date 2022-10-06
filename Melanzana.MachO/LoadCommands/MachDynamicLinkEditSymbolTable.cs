using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachDynamicLinkEditSymbolTable : MachLoadCommand
    {
        internal DynamicSymbolTableCommandHeader Header;

        public MachDynamicLinkEditSymbolTable(DynamicSymbolTableCommandHeader header)
        {
            this.Header = header;
        }
    }
}