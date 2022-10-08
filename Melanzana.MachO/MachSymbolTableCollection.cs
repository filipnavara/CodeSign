using System.Buffers.Binary;
using System.Collections;
using System.Diagnostics;
using System.Text;
using Melanzana.MachO.BinaryFormat;
using Melanzana.Streams;

namespace Melanzana.MachO
{
    internal class MachSymbolTableCollection : ICollection<MachSymbol>
    {
        private readonly MachObjectFile objectFile;
        private readonly MachLinkEditData symbolTableData;
        private readonly MachLinkEditData stringTableData;

        private readonly List<MachSymbol> localSymbols = new();
        private readonly List<MachSymbol> externalSymbols = new();
        private readonly List<MachSymbol> undefinedSymbols = new();

        bool isDirty;

        public MachSymbolTableCollection(
            MachObjectFile objectFile,
            MachLinkEditData symbolTableData,
            MachLinkEditData stringTableData,
            Dictionary<byte, MachSection> sectionMap)
        {
            this.objectFile = objectFile;
            this.symbolTableData = symbolTableData;
            this.stringTableData = stringTableData;

            // Read existing symbols
            if (symbolTableData.Size > 0)
            {
                byte[] stringTable = new byte[stringTableData.Size];
                using var stringTableStream = stringTableData.GetReadStream();
                stringTableStream.ReadFully(stringTable);

                int symbolSize = SymbolHeader.BinarySize + (objectFile.Is64Bit ? 8 : 4);
                byte[] symbolBuffer = new byte[symbolSize];
                using var symbolTableStream = symbolTableData.GetReadStream();
                while (symbolTableStream.Position < symbolTableStream.Length)
                {
                    symbolTableStream.ReadFully(symbolBuffer);
                    var symbolHeader = SymbolHeader.Read(symbolBuffer, objectFile.IsLittleEndian, out var _);
                    ulong symbolValue;
                    if (objectFile.IsLittleEndian)
                    {
                        symbolValue = objectFile.Is64Bit ?
                            BinaryPrimitives.ReadUInt64LittleEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize)) :
                            BinaryPrimitives.ReadUInt32LittleEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize));
                    }
                    else
                    {
                        symbolValue = objectFile.Is64Bit ?
                            BinaryPrimitives.ReadUInt64BigEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize)) :
                            BinaryPrimitives.ReadUInt32BigEndian(symbolBuffer.AsSpan(SymbolHeader.BinarySize));
                    }

                    string name = string.Empty;
                    if (symbolHeader.NameIndex != 0)
                    {
                        int nameLength = stringTable.AsSpan((int)symbolHeader.NameIndex).IndexOf((byte)0);
                        Debug.Assert(nameLength >= 0);
                        name = Encoding.UTF8.GetString(stringTable.AsSpan((int)symbolHeader.NameIndex, nameLength));
                    }

                    var symbol = new MachSymbol
                    {
                        Name = name,
                        Descriptor = (MachSymbolDescriptor)symbolHeader.Descriptor,
                        Section = symbolHeader.Section == 0 ? null : sectionMap[symbolHeader.Section],
                        Type = (MachSymbolType)symbolHeader.Type,
                        Value = symbolValue,
                    };

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
            }
        }

        public int Count => localSymbols.Count + externalSymbols.Count + undefinedSymbols.Count;

        public bool IsReadOnly => false;

        public void Add(MachSymbol symbol)
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

            isDirty = true;
        }

        public void Clear()
        {
            externalSymbols.Clear();
            undefinedSymbols.Clear();
            localSymbols.Clear();
            isDirty = true;
        }

        public bool Contains(MachSymbol symbol)
        {
            return
                localSymbols.Contains(symbol) ||
                externalSymbols.Contains(symbol) ||
                undefinedSymbols.Contains(symbol);
        }

        public void CopyTo(MachSymbol[] array, int arrayIndex)
        {
            ArgumentNullException.ThrowIfNull(array);

            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            }

            int count = Count;
            if (array.Length - arrayIndex < count)
            {
                throw new ArgumentException("Array is too small", nameof(array));
            }

            localSymbols.CopyTo(array, arrayIndex);
            externalSymbols.CopyTo(array, arrayIndex + localSymbols.Count);
            undefinedSymbols.CopyTo(array, arrayIndex + localSymbols.Count + externalSymbols.Count);
        }

        public IEnumerator<MachSymbol> GetEnumerator()
        {
            return localSymbols.Concat(externalSymbols).Concat(undefinedSymbols).GetEnumerator();
        }

        public bool Remove(MachSymbol item)
        {
            if (localSymbols.Remove(item) ||
                externalSymbols.Remove(item) ||
                undefinedSymbols.Remove(item))
            {
                isDirty = true;
                return true;
            }

            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public MachDynamicLinkEditSymbolTable CreateDynamicLinkEditSymbolTable()
        {
            // NOTE: Match the order of WriteSymbols in FlushIfDirty
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

        public void FlushIfDirty()
        {
            if (isDirty)
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

                using var stringTableWriter = stringTableData.GetWriteStream();
                using var symbolTableWriter = symbolTableData.GetWriteStream();

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

                isDirty = false;
            }
        }
    }
}