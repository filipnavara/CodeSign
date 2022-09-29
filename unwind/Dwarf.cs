using Melanzana.MachO;
using Iced.Intel;
using System.Buffers.Binary;
using System.Diagnostics;

namespace Melanzana.Unwind
{
    struct CieInfo
    {
        //ulong CieStart;
        //ulong CieLength;
        public ulong CieInstructions;
        public int CieInstructionsLength;
        public byte PointerEncoding;
        public byte LsdaEncoding;
        public byte PersonalityEncoding;
        public ulong Personality;
        public uint CodeAlignFactor;
        public int DataAlignFactor;
        public bool IsSignalFrame;
        public bool FdesHaveAugmentationData;
        public byte ReturnAddressRegister;
/*    bool      addressesSignedWithBKey;
    bool      mteTaggedFrame;        */
    }

    struct FdeInfo
    {
        public CieInfo CieInfo;
        public ulong FdeInstructions;
        public int FdeInstructionsLength;
        public ulong PcStart;
        public ulong PcEnd;
        public ulong Lsda;

        /*public static FdeInfo Read(ReadOnlySpan<byte> buffer, Func<uint, CieInfo> getCieInfo)
        {
            uint ciePointer = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
            var cieInfo = getCieInfo(ciePointer);

            return new FdeInfo {
                CiePointer = 
            };
        }*/
    }


    [Flags]
    enum DW_EH_PE : byte
    {
        absptr	= 0x00,
        omit	= 0xff,

        ptr	= 0x00,
        uleb128	= 0x01,
        udata2	= 0x02,
        udata4	= 0x03,
        udata8	= 0x04,
        sleb128	= 0x09,
        sdata2	= 0x0a,
        sdata4	= 0x0b,
        sdata8	= 0x0c,
        signed	= 0x08,

        pcrel	= 0x10,
        textrel	= 0x20,
        datarel	= 0x30,
        funcrel	= 0x40,
        aligned	= 0x50,

        indirect= 0x80
    }

    enum DW_CFA
    {
        nop                 = 0x0,
        set_loc             = 0x1,
        advance_loc1        = 0x2,
        advance_loc2        = 0x3,
        advance_loc4        = 0x4,
        offset_extended     = 0x5,
        restore_extended    = 0x6,
        undefined           = 0x7,
        same_value          = 0x8,
        register            = 0x9,
        remember_state      = 0xA,
        restore_state       = 0xB,
        def_cfa             = 0xC,
        def_cfa_register    = 0xD,
        def_cfa_offset      = 0xE,
        def_cfa_expression  = 0xF,
        expression         = 0x10,
        offset_extended_sf = 0x11,
        def_cfa_sf         = 0x12,
        def_cfa_offset_sf  = 0x13,
        val_offset         = 0x14,
        val_offset_sf      = 0x15,
        val_expression     = 0x16,
        advance_loc        = 0x40, // high 2 bits are 0x1, lower 6 bits are delta
        offset             = 0x80, // high 2 bits are 0x2, lower 6 bits are register
        restore            = 0xC0, // high 2 bits are 0x3, lower 6 bits are register

        // GNU extensions
        GNU_window_save              = 0x2D,
        GNU_args_size                = 0x2E,
        GNU_negative_offset_extended = 0x2F,

        // AARCH64 extensions
        AARCH64_negate_ra_state      = 0x2D
    }

    enum CFI_OPCODE
    {
        CFI_ADJUST_CFA_OFFSET,    // Offset is adjusted relative to the current one.
        CFI_DEF_CFA_REGISTER,     // New register is used to compute CFA
        CFI_REL_OFFSET,           // Register is saved at offset from the current CFA
        CFI_DEF_CFA               // Take address from register and add offset to it.
    }

    class DwarfEhFrame
    {
        byte[] ehFrame;
        Dictionary<ulong, CieInfo> offsetToCieInfo = new Dictionary<ulong, CieInfo>();
        Dictionary<ulong, FdeInfo> pcToFdeInfo = new Dictionary<ulong, FdeInfo>();

        public DwarfEhFrame(Stream ehFrameStream, ulong startAddr)
        {
            ehFrame = new byte[ehFrameStream.Length];
            ehFrameStream.ReadExactly(ehFrame);

            int position = 0;
            while (position < ehFrame.Length)
            {
                var startPosition = position;
                ulong cieLength = BinaryPrimitives.ReadUInt32LittleEndian(ehFrame.AsSpan(position));
                position += 4;
                if (cieLength == 0xffffffff)
                {
                    cieLength = BinaryPrimitives.ReadUInt64LittleEndian(ehFrame.AsSpan(position));
                    position += 8;
                }
                var postLengthPosition = position;

                uint id = BinaryPrimitives.ReadUInt32LittleEndian(ehFrame.AsSpan(position));
                if (id == 0)
                {
                    position += 4;
                    // CIE
                    byte version = ehFrame[position++];
                    Debug.Assert(version == 1 || version == 3);
                    int endAugmentationString = position;
                    while (ehFrame[endAugmentationString] != 0)
                        endAugmentationString++;
                    ReadOnlySpan<byte> augmentationString = ehFrame.AsSpan(position, endAugmentationString - position);
                    position = endAugmentationString + 1;
                    uint codeAlignFactor = (uint)ReadULEB128(ehFrame, ref position);
                    int dataAlignFactor = (int)ReadSLEB128(ehFrame, ref position);
                    var returnAddressRegister = version == 1 ? ehFrame[position++] : (byte)ReadULEB128(ehFrame, ref position);

                    bool fdesHaveAugmentationData = false;
                    byte personalityEncoding = 0;
                    ulong personality = 0;
                    byte lsdaEncoding = 0;
                    byte pointerEncoding = 0;
                    bool isSignalFrame = false; 
                    bool addressesSignedWithBKey = false;
                    bool mteTaggedFrame = false;
                    if (augmentationString.Length > 0 && augmentationString[0] == 'z')
                    {
                        // Augmentation data length
                        ReadULEB128(ehFrame, ref position);
                        foreach (var a in augmentationString)
                        {
                            switch ((char)a)
                            {
                                case 'z': fdesHaveAugmentationData = true; break;
                                case 'P':
                                    personalityEncoding = ehFrame[position++];
                                    personality = ReadAddress(ehFrame, ref position, startAddr, personalityEncoding);
                                    break;
                                case 'L':
                                    lsdaEncoding = ehFrame[position++];
                                    break;
                                case 'R':
                                    pointerEncoding = ehFrame[position++];
                                    break;
                                case 'S': isSignalFrame = true; break;
                                case 'B': addressesSignedWithBKey = true; break;
                                case 'G': mteTaggedFrame = true; break;
                                default:
                                    Debug.Fail("Unknown augmentation");
                                    break;
                            }
                        }
                    }

                    var cieInfo = new CieInfo
                    {
                        CieInstructions = (ulong)position,
                        CieInstructionsLength = (int)cieLength - (position - postLengthPosition),
                        PointerEncoding = pointerEncoding,
                        LsdaEncoding = lsdaEncoding,
                        PersonalityEncoding = personalityEncoding,
                        Personality = personality,
                        CodeAlignFactor = codeAlignFactor,
                        DataAlignFactor = dataAlignFactor,
                        IsSignalFrame = isSignalFrame,
                        FdesHaveAugmentationData = fdesHaveAugmentationData,
                        ReturnAddressRegister = returnAddressRegister,
                        // ...
                    };
                    offsetToCieInfo.Add((ulong)startPosition, cieInfo);
                }
                else
                {
                    // FDE
                    uint ciePointer = BinaryPrimitives.ReadUInt32LittleEndian(ehFrame.AsSpan(position));
                    var cieInfo = offsetToCieInfo[(ulong)position - ciePointer];
                    position += 4;
                    ulong pcStart = ReadAddress(ehFrame, ref position, startAddr, cieInfo.PointerEncoding);
                    ulong pcLength = ReadAddress(ehFrame, ref position, startAddr, (byte)(cieInfo.PointerEncoding & 0x0f));
                    ulong lsda = 0;
                    if (cieInfo.FdesHaveAugmentationData)
                    {
                        // Augmentation data length
                        ulong augmentationLength = ReadULEB128(ehFrame, ref position);
                        var savedPosition = position;
                        if (cieInfo.LsdaEncoding != (byte)DW_EH_PE.omit)
                        {
                            var tempPosition = position;
                            if (ReadAddress(ehFrame, ref tempPosition, startAddr, (byte)(cieInfo.LsdaEncoding & 0x0f)) != 0)
                            {
                                lsda = ReadAddress(ehFrame, ref position, startAddr, (byte)(cieInfo.LsdaEncoding & 0x0f));
                            }
                        }
                        position = savedPosition + (int)augmentationLength;
                    }

                    var fdeInfo = new FdeInfo
                    {
                        CieInfo = cieInfo,
                        FdeInstructions = (ulong)position,
                        FdeInstructionsLength = (int)cieLength - (position - postLengthPosition),
                        PcStart = pcStart,
                        PcEnd = pcStart + pcLength,
                        Lsda = lsda
                    };
                    pcToFdeInfo.Add(pcStart, fdeInfo);
                }

                position = postLengthPosition + (int)cieLength;
            }
        }

        public void PrintCfi(ulong pcStart)
        {
            if (pcToFdeInfo.TryGetValue(pcStart, out var fdeInfo))
            {
                Console.WriteLine("CFI:");
                Console.WriteLine(Convert.ToHexString(ehFrame.AsSpan((int)fdeInfo.CieInfo.CieInstructions, (int)fdeInfo.CieInfo.CieInstructionsLength)));
                Console.WriteLine(Convert.ToHexString(ehFrame.AsSpan((int)fdeInfo.FdeInstructions, (int)fdeInfo.FdeInstructionsLength)));
            }
        }

        private IEnumerable<(ulong codeOffset, ulong cfiRegister, ulong cfiRegisterOffset)> EnumerateCfi(
            ulong pcStart,
            ReadOnlySpan<byte> instructions,
            CieInfo cieInfo,
            ref ulong cfiRegister,
            ref ulong cfiRegisterOffset)
        {
            ulong codeOffset = 0;
            List<(ulong codeOffset, ulong cfiRegister, ulong cfiRegisterOffset)> result = new();
            ulong lastCodeOffset = 0;
            ulong lastCfiRegister = cfiRegister;
            ulong lastCfiRegisterOffset = cfiRegisterOffset;

            while (!instructions.IsEmpty)
            {
                byte opcode = instructions[0];
                int position = 1;
                switch ((DW_CFA)opcode)
                {
                    case DW_CFA.nop:
                        instructions = instructions.Slice(1);
                        break;
                    case DW_CFA.advance_loc1:
                        codeOffset += instructions[1] * cieInfo.CodeAlignFactor;
                        instructions = instructions.Slice(2);
                        break;
                    case DW_CFA.advance_loc2:
                        codeOffset += BinaryPrimitives.ReadUInt16LittleEndian(instructions.Slice(1)) * cieInfo.CodeAlignFactor;
                        instructions = instructions.Slice(3);
                        break;
                    case DW_CFA.advance_loc4:
                        codeOffset += BinaryPrimitives.ReadUInt32LittleEndian(instructions.Slice(1)) * cieInfo.CodeAlignFactor;
                        instructions = instructions.Slice(5);
                        break;
                    case DW_CFA.register:
                        ReadULEB128(instructions, ref position);
                        ReadULEB128(instructions, ref position);
                        instructions = instructions.Slice(position);
                        break;
                    case DW_CFA.offset_extended:
                    case DW_CFA.restore_extended:
                    case DW_CFA.undefined:
                    case DW_CFA.same_value:
                    case DW_CFA.remember_state:
                    case DW_CFA.restore_state:
                    case DW_CFA.def_cfa_expression:
                    case DW_CFA.offset_extended_sf:
                        Debug.Fail("TODO");
                        break;
                    case DW_CFA.def_cfa:
                        cfiRegister = ReadULEB128(instructions, ref position);
                        cfiRegisterOffset = ReadULEB128(instructions, ref position);
                        instructions = instructions.Slice(position);
                        break;
                    case DW_CFA.def_cfa_register:
                        cfiRegister = ReadULEB128(instructions, ref position);
                        instructions = instructions.Slice(position);
                        break;
                    case DW_CFA.def_cfa_offset:
                        cfiRegisterOffset = ReadULEB128(instructions, ref position);
                        instructions = instructions.Slice(position);
                        break;
                    default:
                        var operand = opcode & 0x3f;
                        switch ((DW_CFA)(opcode & 0xc0))
                        {
                            case DW_CFA.offset:
                                ReadULEB128(instructions, ref position);
                                instructions = instructions.Slice(position);
                                break;
                            case DW_CFA.advance_loc:
                                codeOffset += (ulong)(operand * cieInfo.CodeAlignFactor);
                                instructions = instructions.Slice(1);
                                break;
                            default:
                                Debug.Fail("TODO");
                                break;
                        }
                        break;
                }

                if (lastCodeOffset != codeOffset)
                {
                    if (lastCfiRegister != cfiRegister ||
                        lastCfiRegisterOffset != cfiRegisterOffset)
                    {
                        result.Add((lastCodeOffset, cfiRegister, cfiRegisterOffset));
                        lastCfiRegister = cfiRegister;
                        lastCfiRegisterOffset = cfiRegisterOffset;
                    }

                    lastCodeOffset = codeOffset;
                }
            }

            if (lastCfiRegister != cfiRegister ||
                lastCfiRegisterOffset != cfiRegisterOffset)
            {
                result.Add((codeOffset, cfiRegister, cfiRegisterOffset));
            }

            return result;
        }

        public IEnumerable<(ulong codeOffset, ulong cfiRegister, ulong cfiRegisterOffset)> EnumerateCfi(ulong pcStart)
        {
            if (pcToFdeInfo.TryGetValue(pcStart, out var fdeInfo))
            {
                ulong cfiRegister = 0;
                ulong cfiRegisterOffset = 0;
                foreach (var e in EnumerateCfi(pcStart, ehFrame.AsSpan((int)fdeInfo.CieInfo.CieInstructions, (int)fdeInfo.CieInfo.CieInstructionsLength), fdeInfo.CieInfo, ref cfiRegister, ref cfiRegisterOffset))
                    yield return e;
                foreach (var e in EnumerateCfi(pcStart, ehFrame.AsSpan((int)fdeInfo.FdeInstructions, (int)fdeInfo.FdeInstructionsLength), fdeInfo.CieInfo, ref cfiRegister, ref cfiRegisterOffset))
                    yield return e;
            }
        }

        private static ulong ReadULEB128(ReadOnlySpan<byte> data, ref int position)
        {
            ulong result = 0;
            int shift = 0;
            do
            {
                result |= (ulong)(data[position] & 0x7f) << shift;
                shift += 7;
            }
            while ((data[position++] & 0x80) != 0);
            return result;
        }


        private static long ReadSLEB128(ReadOnlySpan<byte> data, ref int position)
        {
            long result = 0;
            int shift = 0;
            do
            {
                result |= (long)(data[position] & 0x7f) << shift;
                shift += 7;
            }
            while ((data[position++] & 0x80) != 0);
            if (shift < 8 && (data[position - 1] & 0x40) != 0)
                  result |= ~0L << shift;
            return result;
        }

        private static ulong ReadAddress(ReadOnlySpan<byte> data, ref int position, ulong startAddr, byte encoding)
        {
            ulong result = 0;

            switch ((DW_EH_PE)(encoding & 0x70))
            {
                case DW_EH_PE.absptr: result = 0; break;
                case DW_EH_PE.pcrel: result = startAddr + (ulong)position; break;
                default:
                    Debug.Fail("Unsupport address encoding");
                    break;
            }

            switch ((DW_EH_PE)(encoding & 0x0F))
            {
                case DW_EH_PE.uleb128:
                    result += ReadULEB128(data, ref position);
                    break;
                case DW_EH_PE.udata2:
                    result += BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(position));
                    position += 2;
                    break;
                case DW_EH_PE.udata4:
                    result += BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(position));
                    position += 4;
                    break;
                case DW_EH_PE.ptr:
                case DW_EH_PE.udata8:
                    result += BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(position));
                    position += 8;
                    break;
                case DW_EH_PE.sleb128:
                    result = (ulong)((long)result + ReadSLEB128(data, ref position));
                    break;
                case DW_EH_PE.sdata2:
                    result = (ulong)((long)result + BinaryPrimitives.ReadInt16LittleEndian(data.Slice(position)));
                    position += 2;
                    break;
                case DW_EH_PE.sdata4:
                    result = (ulong)((long)result + BinaryPrimitives.ReadInt32LittleEndian(data.Slice(position)));
                    position += 4;
                    break;
                case DW_EH_PE.sdata8:
                    result = (ulong)((long)result + BinaryPrimitives.ReadInt64LittleEndian(data.Slice(position)));
                    position += 8;
                    break;
            }

            if ((encoding & (byte)DW_EH_PE.indirect) != 0)
            {
                Debug.Fail("Indirect address");
            }

            return result;
        }
    }
}