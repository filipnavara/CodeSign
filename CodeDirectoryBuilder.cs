using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CodeSign.MachO;
using CodeSign.Streams;

class CodeDirectoryBuilder
{
    private readonly MachO executable;
    private readonly string identifier;
    private readonly string teamId;
    byte[][] specialSlots = Array.Empty<byte[]>();

    public CodeDirectoryBuilder(MachO executable, string identifier, string teamId)
    {
        this.executable = executable;
        this.identifier = identifier;
        this.teamId = teamId;
    }

    public void SetSpecialSlotData(SpecialSlot slot, byte[] data)
    {
        if (specialSlots.Length < (int)slot)
        {
            Array.Resize(ref specialSlots, (int)slot + 1);
        }
        specialSlots[(int)slot] = data;
    }


    private static int GetFixedHeaderSize(CodeDirectoryVersion version)
    {
        return version switch 
        {
            CodeDirectoryVersion.SupportsPreEncrypt => 84,
            CodeDirectoryVersion.SupportsExecSegment => 76,
            CodeDirectoryVersion.SupportsCodeLimit64 => 56,
            CodeDirectoryVersion.SupportsTeamId => 44,
            CodeDirectoryVersion.SupportsScatter => 40,
            CodeDirectoryVersion.Baseline => 36,
            _ => throw new NotSupportedException()
        };
    }

    private static IncrementalHash GetIncrementalHash(CodeSigningHashType hashType)
    {
        return hashType switch
        {
            CodeSigningHashType.SHA1 => IncrementalHash.CreateHash(HashAlgorithmName.SHA1),
            CodeSigningHashType.SHA256 => IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
            CodeSigningHashType.SHA256Truncated => IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
            CodeSigningHashType.SHA384 => IncrementalHash.CreateHash(HashAlgorithmName.SHA384),
            CodeSigningHashType.SHA512 => IncrementalHash.CreateHash(HashAlgorithmName.SHA512),
            _ => throw new NotSupportedException()
        };
    }

    private static byte GetHashSize(CodeSigningHashType hashType)
    {
        return hashType switch
        {
            CodeSigningHashType.SHA1 => 20,
            CodeSigningHashType.SHA256 => 32,
            CodeSigningHashType.SHA256Truncated => 20,
            CodeSigningHashType.SHA384 => 48,
            CodeSigningHashType.SHA512 => 64,
            _ => throw new NotSupportedException()
        };
    }

    public byte[] Build()
    {
        // NOTE: We only support building version 0x20400 at the moment
        CodeDirectoryVersion version = CodeDirectoryVersion.SupportsExecSegment;

        using var machOStream = executable.GetStream();

        ulong execLength = (ulong)machOStream.Length; // TODO: Size up to code signature

        uint pageSize = 4096;
        uint codeSlotCount = (uint)((execLength + pageSize - 1) / pageSize);

        byte[] utf8Identifier = Encoding.UTF8.GetBytes(identifier);
        byte[] utf8TeamId = Encoding.UTF8.GetBytes(teamId);

        CodeSigningHashType hashType = CodeSigningHashType.SHA256;
        byte hashSize = GetHashSize(hashType);

        long codeDirectorySize = GetFixedHeaderSize(version);
        codeDirectorySize += identifier.Length + 1;
        if (!String.IsNullOrEmpty(teamId))
            codeDirectorySize += teamId.Length + 1;
        codeDirectorySize += (specialSlots.Length + codeSlotCount) * hashSize;
        // TODO: Pre-encrypt slots (codeSlotCount * hashSize)



        byte[] overSizeBuffer = new byte[codeDirectorySize];

        //var textSegment = executable.GetCommandsOfType<Segment>().First(s => s.Name == "__TEXT");
        //Debug.Assert(textSegment != null);

        //textSegment.

        //BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer, 0xfade0c02);
        BinaryPrimitives.WriteInt32BigEndian(overSizeBuffer, (int)CodeDirectoryVersion.SupportsExecSegment);
        BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(4), 0); // Flags
        //BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(8), 0); // Hashes offset
        //BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(12), 0); // Identifier offset
        BinaryPrimitives.WriteInt32BigEndian(overSizeBuffer.AsSpan(16), specialSlots.Length);
        BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(20), codeSlotCount);
        BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(24), (execLength > uint.MaxValue) ? uint.MaxValue : (uint)execLength);
        overSizeBuffer[28] = hashSize;
        overSizeBuffer[29] = (byte)hashType;
        overSizeBuffer[30] = 0; // Platform
        overSizeBuffer[31] = 12; // Page size (log2)
        // Reserved (4 bytes)
        if (version >= CodeDirectoryVersion.SupportsScatter)
        {
            // TODO
            //BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(36), 0);
            if (version >= CodeDirectoryVersion.SupportsTeamId)
            {
                // TODO: Team ID offset
                if (version >= CodeDirectoryVersion.SupportsCodeLimit64)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(44), 0);
                    if (execLength > uint.MaxValue)
                        BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(48), execLength);
                    if (version >= CodeDirectoryVersion.SupportsExecSegment)
                    {
                        //BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(56), textSegment.FileOffset);
                        //BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(64), textSegment.Size);
                        //BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(72), execSegmentFlags);
                        if (version >= CodeDirectoryVersion.SupportsPreEncrypt)
                        {
                            // TODO: Runtime version + Pre-encrypt offset
                        }
                    }
                }
            }
        }

        // TODO: Read this from the stream
        //var textSegmentData = textSegment.GetData().AsSpan();
        Span<byte> buffer = stackalloc byte[(int)pageSize];
        long remaining = (long)execLength;
        var hasher = GetIncrementalHash(hashType);
        while (remaining > 0)
        {
            int codePageSize = (int)Math.Min(remaining, 4096);
            machOStream.ReadFully(buffer.Slice(0, codePageSize));
            hasher.AppendData(buffer.Slice(0, codePageSize));
            //this.Hashes[index] = this.HashAlgorithm.ComputeHash(rawMachO, (int)index * 4096, pageSize);
            hasher.GetHashAndReset();
            //textSegmentData = textSegmentData.Slice(codePageSize);
            remaining -= codePageSize;
        }
        //executable.

        return Array.Empty<byte>();

        /*

                    using (EndianessAwareWriter writer = new EndianessAwareWriter((Stream)new MemoryStream(), Endianess.BigEndian))
            {
                CodeDirectoryVersion directoryVersion = CodeDirectoryVersion.XNU_iOS11;
                writer.WriteInt32(132096);
                writer.WriteUInt32(0U);
                long position1 = writer.BaseStream.Position;
                writer.WriteInt32(0);
                long position2 = writer.BaseStream.Position;
                writer.WriteInt32(0);
                writer.WriteUInt32((uint)numberOfSpecialSlots);
                int num = (int)Math.Ceiling((double)machO.Size / 4096.0);
                writer.WriteUInt32((uint)num);
                writer.WriteUInt32((uint)machO.Size);
                writer.WriteByte((byte)hashSize);
                writer.WriteByte(hashSize == 20 ? (byte)1 : (byte)2);
                writer.WriteByte((byte)0);
                writer.WriteByte((byte)12);
                writer.WriteUInt32(0U);
                if (directoryVersion >= CodeDirectoryVersion.XNU_NA)
                    writer.WriteUInt32(0U);
                long position3 = writer.BaseStream.Position;
                if (directoryVersion >= CodeDirectoryVersion.XNU_2422)
                    writer.WriteUInt32(0U);
                if (directoryVersion >= CodeDirectoryVersion.XNU_3247)
                {
                    writer.WriteUInt32(0U);
                    writer.WriteInt64(0L);
                }
                if (directoryVersion >= CodeDirectoryVersion.XNU_iOS11)
                {
                    Segment segment = machO.GetCommandsOfType<Segment>().FirstOrDefault<Segment>((Func<Segment, bool>)(s => s.Name == "__TEXT"));
                    writer.WriteInt64(0L);
                    writer.WriteInt64(segment.Size);
                    if (numberOfSpecialSlots >= 5) // Application (5+ slots)
                        writer.WriteInt64(17L);
                    else // Framework (3 slots)
                        writer.WriteInt64(0);
                }
                int identOffsetValue = (int)writer.BaseStream.Position + 8;
                writer.JumpToWriteAndGetBack(position2, (Action)(() => writer.WriteInt32(identOffsetValue)));
                writer.WriteString(identifier);
                if (directoryVersion >= CodeDirectoryVersion.XNU_2422)
                {
                    int teamIdOffsetValue = (int)writer.BaseStream.Position + 8;
                    writer.JumpToWriteAndGetBack(position3, (Action)(() => writer.WriteInt32(teamIdOffsetValue)));
                    writer.WriteString(teamID);
                }
                for (int index = 0; index < numberOfSpecialSlots; ++index)
                    writer.WriteBytes(new byte[hashSize], true);
                int hashesOffsetValue = (int)(writer.BaseStream.Position + 8L);
                writer.JumpToWriteAndGetBack(position1, (Action)(() => writer.WriteInt32(hashesOffsetValue)));
                File.ReadAllBytes(machO.FileName);
                byte[] array = (writer.BaseStream as MemoryStream).ToArray();
                CodeDirectory codeDirectory = new CodeDirectory(0, array.Length, array);
                codeDirectory.UpdateCodeSlotHashes(machO);
                return codeDirectory;
            }*/
 
    }
}
