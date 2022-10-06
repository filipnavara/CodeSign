using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    public class MachSymbolTableWriter : IDisposable
    {
        MachObjectFile objectFile;
        MachSymbolTable symbolTable;
        MachSection symbolTableSection;
        MachSection stringTableSection;
        List<MachSymbol> localSymbols = new();
        List<MachSymbol> externalSymbols = new();
        List<MachSymbol> undefinedSymbols = new();
        bool disposed;

        internal MachSymbolTableWriter(
            MachObjectFile objectFile,
            MachSymbolTable symbolTable,
            MachSection symbolTableSection,
            MachSection stringTableSection)
        {
            this.objectFile = objectFile;
            this.symbolTable = symbolTable;
            this.symbolTableSection = symbolTableSection;
            this.stringTableSection = stringTableSection;
        }

        public void AddSymbol(MachSymbol symbol)
        {
            if (symbol.IsExternal)
            {
                externalSymbols.Add(symbol);
            }
            else if (symbol.IsUndefined)
            {
                undefinedSymbols.Add(symbol);
            }
            else
            {
                localSymbols.Add(symbol);
            }
        }

        public MachDynamicLinkEditSymbolTable CreateDynamicLinkEditSymbolTable()
        {
            // NOTE: Match the order of WriteSymbols in Dispose
            return new MachDynamicLinkEditSymbolTable(new DynamicSymbolTableCommandHeader
            {
                LocalSymbolsIndex = 0,
                LocalSymbolsCount = (uint)localSymbols.Count,
                ExternalSymbolsIndex = (uint)localSymbols.Count,
                ExternalSymbolsCount = (uint)externalSymbols.Count,
                UndefinedSymbolsIndex = (uint)(localSymbols.Count + externalSymbols.Count),
                UndefinedSymbolsCount = (uint)undefinedSymbols.Count,
            });
        }

        public void Dispose()
        {
            if (!disposed)
            {
                externalSymbols.Sort((symA, symB) => string.CompareOrdinal(symA.Name, symB.Name));
                undefinedSymbols.Sort((symA, symB) => string.CompareOrdinal(symA.Name, symB.Name));

                var sectionMap = new Dictionary<MachSection, byte>();
                byte sectionIndex = 1;
                foreach (var section in objectFile.Segments.SelectMany(segment => segment.Sections))
                {
                    sectionMap.Add(section, sectionIndex++);
                    Debug.Assert(sectionIndex != 0);
                }

                using var stringTableWriter = stringTableSection.GetWriteStream();
                using var symbolTableWriter = symbolTableSection.GetWriteStream();

                // Start the table with a NUL byte.
                stringTableWriter.WriteByte(0);

                WriteSymbols(localSymbols);
                WriteSymbols(externalSymbols);
                WriteSymbols(undefinedSymbols);

                void WriteSymbols(IList<MachSymbol> symbols)
                {
                    SymbolHeader symbolHeader = new SymbolHeader();
                    Span<byte> symbolHeaderBuffer = stackalloc byte[SymbolHeader.BinarySize];
                    Span<byte> symbolValueBuffer = new byte[objectFile.Is64Bit ? 8 : 4];

                    foreach (var symbol in symbols)
                    {
                        var nameBytes = Encoding.UTF8.GetBytes(symbol.Name);
                        var nameOffset = stringTableWriter.Position;

                        stringTableWriter.Write(nameBytes);
                        stringTableWriter.WriteByte(0);

                        symbolHeader.NameIndex = (uint)nameOffset;
                        symbolHeader.Section = symbol.Section == null ? (byte)0 : sectionMap[symbol.Section];
                        symbolHeader.Descriptor = (ushort)symbol.Descriptor;
                        symbolHeader.Type = (byte)symbol.Type;

                        symbolHeader.Write(symbolHeaderBuffer, objectFile.IsLittleEndian, out _);
                        symbolTableWriter.Write(symbolHeaderBuffer);

                        if (objectFile.Is64Bit)
                        {
                            if (objectFile.IsLittleEndian)
                            {
                                BinaryPrimitives.WriteUInt64LittleEndian(symbolValueBuffer, symbol.Value);
                            }
                            else
                            {
                                BinaryPrimitives.WriteUInt64BigEndian(symbolValueBuffer, symbol.Value);
                            }
                        }
                        else if (objectFile.IsLittleEndian)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(symbolValueBuffer, (uint)symbol.Value);
                        }
                        else
                        {
                            BinaryPrimitives.WriteUInt32BigEndian(symbolValueBuffer, (uint)symbol.Value);
                        }

                        symbolTableWriter.Write(symbolValueBuffer);
                    }
                }

                // Pad the string table
                int alignment = objectFile.Is64Bit ? 8 : 4;
                while ((stringTableWriter.Position & (alignment - 1)) != 0)
                    stringTableWriter.WriteByte(0);

                symbolTable.NumberOfSymbols =
                    (uint)(localSymbols.Count +
                    externalSymbols.Count +
                    undefinedSymbols.Count);
                symbolTable.StringTableSize = (uint)stringTableWriter.Position;

                disposed = true;
            }
        }
    }
}