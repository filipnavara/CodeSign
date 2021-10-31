using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CodeSign.MachO;
using CodeSign.MachO.Commands;
using CodeSign.Streams;

namespace CodeSign.Signing
{
    class CodeDirectoryBuilder
    {
        private readonly MachO.MachO executable;
        private readonly string identifier;
        private readonly string teamId;
        private readonly uint pageSize = 4096;
        private byte[][] specialSlots = new byte[7][];
        private int specialSlotCount;

        public CodeDirectoryBuilder(MachO.MachO executable, string identifier, string teamId)
        {
            this.executable = executable;
            this.identifier = identifier;
            this.teamId = teamId;

            if (executable.Header.FileType == MachFileType.Execute)
                ExecutableSegmentFlags |= ExecutableSegmentFlags.MainBinary;
        }

        public void SetSpecialSlotData(CodeDirectorySpecialSlot slot, byte[] data)
        {
            Debug.Assert((int)slot >= 1 && (int)slot <= specialSlots.Length);
            specialSlots[(int)(slot - 1)] = data;
            specialSlotCount = Math.Max(specialSlotCount, (int)slot);
        }

        public CodeSigningHashType HashType { get; set; } = CodeSigningHashType.SHA256;

        public ExecutableSegmentFlags ExecutableSegmentFlags { get; set; }

        private static int GetFixedHeaderSize(CodeDirectoryVersion version)
        {
            return version switch 
            {
                CodeDirectoryVersion.SupportsPreEncrypt => 96,
                CodeDirectoryVersion.SupportsExecSegment => 88,
                CodeDirectoryVersion.SupportsCodeLimit64 => 64,
                CodeDirectoryVersion.SupportsTeamId => 52,
                CodeDirectoryVersion.SupportsScatter => 48,
                CodeDirectoryVersion.Baseline => 42,
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

        public long Size(CodeDirectoryVersion version)
        {
            ulong execLength = executable.GetSigningLimit();
            uint codeSlotCount = (uint)((execLength + pageSize - 1) / pageSize);
            byte hashSize = GetHashSize(HashType);

            byte[] utf8Identifier = Encoding.UTF8.GetBytes(identifier);
            byte[] utf8TeamId = Encoding.UTF8.GetBytes(teamId);

            long codeDirectorySize = GetFixedHeaderSize(version);
            codeDirectorySize += utf8Identifier.Length + 1;
            if (!String.IsNullOrEmpty(teamId))
                codeDirectorySize += utf8TeamId.Length + 1;
            codeDirectorySize += (specialSlotCount + codeSlotCount) * hashSize;
            if (version >= CodeDirectoryVersion.SupportsPreEncrypt)
                codeDirectorySize += codeSlotCount * hashSize;

            return codeDirectorySize;
        }

        public byte[] Build()
        {
            // NOTE: We only support building version 0x20400 at the moment
            CodeDirectoryVersion version = CodeDirectoryVersion.SupportsExecSegment;

            using var machOStream = executable.GetStream();

            ulong execLength = executable.GetSigningLimit();
            uint codeSlotCount = (uint)((execLength + pageSize - 1) / pageSize);

            byte[] utf8Identifier = Encoding.UTF8.GetBytes(identifier);
            byte[] utf8TeamId = Encoding.UTF8.GetBytes(teamId);

            byte hashSize = GetHashSize(HashType);

            byte[] overSizeBuffer = new byte[Size(version)];

            var textSegment = executable.LoadCommands.OfType<ISegment>().First(s => s.Name == "__TEXT");
            Debug.Assert(textSegment != null);

            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(0, 4), (uint)CodeSigningBlobMagic.CodeDirectory);
            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(4, 4), (uint)overSizeBuffer.Length);
            BinaryPrimitives.WriteInt32BigEndian(overSizeBuffer.AsSpan(8, 4), (int)CodeDirectoryVersion.SupportsExecSegment);
            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(12, 4), 0); // Flags
            // Filled later: Hashes offset
            // Filled later: Identifier offset
            BinaryPrimitives.WriteInt32BigEndian(overSizeBuffer.AsSpan(24), specialSlotCount);
            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(28), codeSlotCount);
            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(32), (execLength > uint.MaxValue) ? uint.MaxValue : (uint)execLength);
            overSizeBuffer[36] = hashSize;
            overSizeBuffer[37] = (byte)HashType;
            overSizeBuffer[38] = 0; // TODO: Platform
            overSizeBuffer[39] = (byte)Math.Log2(pageSize);
            // Reserved (4 bytes)
            if (version >= CodeDirectoryVersion.SupportsCodeLimit64)
            {
                BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(52), 0);
                if (execLength > uint.MaxValue)
                    BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(60), execLength);
            }
            if (version >= CodeDirectoryVersion.SupportsExecSegment)
            {
                BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(64), textSegment.FileOffset);
                BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(72), textSegment.FileSize);
                BinaryPrimitives.WriteUInt64BigEndian(overSizeBuffer.AsSpan(80), (ulong)ExecutableSegmentFlags);
            }

            // Fill in flexible fields
            int flexibleOffset = GetFixedHeaderSize(version);

            if (version >= CodeDirectoryVersion.SupportsScatter)
            {
                // TODO
                // BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(36), flexibleOffset);
            }

            // Identifier
            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(20, 4), (uint)flexibleOffset);
            utf8Identifier.AsSpan().CopyTo(overSizeBuffer.AsSpan(flexibleOffset, utf8Identifier.Length));
            flexibleOffset += utf8Identifier.Length + 1;

            // Team ID
            if (version >= CodeDirectoryVersion.SupportsTeamId && !string.IsNullOrEmpty(teamId))
            {
                BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(48, 4), (uint)flexibleOffset);
                utf8TeamId.AsSpan().CopyTo(overSizeBuffer.AsSpan(flexibleOffset, utf8TeamId.Length));
                flexibleOffset += utf8TeamId.Length + 1;
            }

            // Pre-encrypt hashes
            if (version >= CodeDirectoryVersion.SupportsPreEncrypt)
            {
                // TODO
                // BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(88), (uint)flexibleOffset);
            }

            var hasher = GetIncrementalHash(HashType);

            // Special slot hashes
            for (int i = specialSlotCount - 1; i >= 0; i--)
            {
                if (specialSlots[i] != null)
                {
                    hasher.AppendData(specialSlots[i]);
                    hasher.GetHashAndReset().CopyTo(overSizeBuffer.AsSpan(flexibleOffset, hashSize));
                }
                flexibleOffset += hashSize;
            }

            BinaryPrimitives.WriteUInt32BigEndian(overSizeBuffer.AsSpan(16), (uint)flexibleOffset);

            // Code hashes
            Span<byte> buffer = stackalloc byte[(int)pageSize];
            long remaining = (long)execLength;
            while (remaining > 0)
            {
                int codePageSize = (int)Math.Min(remaining, 4096);
                machOStream.ReadFully(buffer.Slice(0, codePageSize));
                hasher.AppendData(buffer.Slice(0, codePageSize));
                hasher.GetHashAndReset().CopyTo(overSizeBuffer.AsSpan(flexibleOffset, hashSize));
                remaining -= codePageSize;
                flexibleOffset += hashSize;
            }

            Debug.Assert(flexibleOffset == overSizeBuffer.Length);

            return overSizeBuffer;
        }
    }
}
