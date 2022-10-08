namespace Melanzana.MachO
{
    public class MachSymbolTable : MachLoadCommand
    {
        private readonly MachObjectFile objectFile;
        private readonly MachLinkEditData symbolTableData;
        private readonly MachLinkEditData stringTableData;
        private MachSymbolTableCollection? symbolTableCollection;

        public MachSymbolTable(MachObjectFile objectFile)
        {
            ArgumentNullException.ThrowIfNull(objectFile);

            this.objectFile = objectFile;
            this.symbolTableData = new MachLinkEditData();
            this.stringTableData = new MachLinkEditData();
        }

        internal MachSymbolTable(
            MachObjectFile objectFile,
            MachLinkEditData symbolTableData,
            MachLinkEditData stringTableData)
        {
            ArgumentNullException.ThrowIfNull(objectFile);
            ArgumentNullException.ThrowIfNull(symbolTableData);
            ArgumentNullException.ThrowIfNull(stringTableData);

            this.objectFile = objectFile;
            this.symbolTableData = symbolTableData;
            this.stringTableData = stringTableData;
        }

        public MachLinkEditData SymbolTableData
        {
            get
            {
                symbolTableCollection?.FlushIfDirty();
                return symbolTableData;
            }
        }

        public MachLinkEditData StringTableData
        {
            get
            {
                symbolTableCollection?.FlushIfDirty();
                return stringTableData;
            }
        }

        public ICollection<MachSymbol> Symbols
        {
            get
            {
                symbolTableCollection ??= new MachSymbolTableCollection(objectFile, symbolTableData, stringTableData);
                return symbolTableCollection;
            }
        }

        public MachDynamicLinkEditSymbolTable CreateDynamicLinkEditSymbolTable()
        {
            symbolTableCollection ??= new MachSymbolTableCollection(objectFile, symbolTableData, stringTableData);
            return symbolTableCollection.CreateDynamicLinkEditSymbolTable();
        }

        internal override IEnumerable<MachLinkEditData> LinkEditData
        {
            get
            {
                yield return SymbolTableData;
                yield return StringTableData;
            }
        }
    }
}