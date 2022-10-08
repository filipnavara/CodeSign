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

        public MachDynamicLinkEditSymbolTable()
        {
            this.Header = new DynamicSymbolTableCommandHeader();
        }

        public MachDynamicLinkEditSymbolTable(DynamicSymbolTableCommandHeader header)
        {
            this.Header = header;
        }

        public uint LocalSymbolsIndex
        {
            get => Header.LocalSymbolsIndex;
            set => Header.LocalSymbolsIndex = value;
        }

        public uint LocalSymbolsCount
        {
            get => Header.LocalSymbolsCount;
            set => Header.LocalSymbolsCount = value;
        }

        public uint ExternalSymbolsIndex
        {
            get => Header.ExternalSymbolsIndex;
            set => Header.ExternalSymbolsIndex = value;
        }

        public uint ExternalSymbolsCount
        {
            get => Header.ExternalSymbolsCount;
            set => Header.ExternalSymbolsCount = value;
        }

        public uint UndefinedSymbolsIndex
        {
            get => Header.UndefinedSymbolsIndex;
            set => Header.UndefinedSymbolsIndex = value;
        }

        public uint UndefinedSymbolsCount
        {
            get => Header.UndefinedSymbolsCount;
            set => Header.UndefinedSymbolsCount = value;
        }

        public uint TableOfContentsOffset
        {
            get => Header.TableOfContentsOffset;
            set => Header.TableOfContentsOffset = value;
        }

        public uint TableOfContentsCount
        {
            get => Header.TableOfContentsCount;
            set => Header.TableOfContentsCount = value;
        }

        public uint ModuleTableOffset
        {
            get => Header.ModuleTableOffset;
            set => Header.ModuleTableOffset = value;
        }

        public uint ModuleTableCount
        {
            get => Header.ModuleTableCount;
            set => Header.ModuleTableCount = value;
        }

        public uint ExternalReferenceTableOffset
        {
            get => Header.ExternalReferenceTableOffset;
            set => Header.ExternalReferenceTableOffset = value;
        }

        public uint ExternalReferenceTableCount
        {
            get => Header.ExternalReferenceTableCount;
            set => Header.ExternalReferenceTableCount = value;
        }

        public uint IndirectSymbolTableOffset
        {
            get => Header.IndirectSymbolTableOffset;
            set => Header.IndirectSymbolTableOffset = value;
        }

        public uint IndirectSymbolTableCount
        {
            get => Header.IndirectSymbolTableCount;
            set => Header.IndirectSymbolTableCount = value;
        }

        public uint ExternalRelocationTableOffset
        {
            get => Header.ExternalRelocationTableOffset;
            set => Header.ExternalRelocationTableOffset = value;
        }

        public uint ExternalRelocationTableCount
        {
            get => Header.ExternalRelocationTableCount;
            set => Header.ExternalRelocationTableCount = value;
        }

        public uint LocalRelocationTableOffset
        {
            get => Header.LocalRelocationTableOffset;
            set => Header.LocalRelocationTableOffset = value;
        }

        public uint LocalRelocationTableCount
        {
            get => Header.LocalRelocationTableCount;
            set => Header.LocalRelocationTableCount = value;
        }
    }
}