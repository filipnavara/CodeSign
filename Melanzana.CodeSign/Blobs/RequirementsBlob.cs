using System.Buffers.Binary;
using System.Text;
using Melanzana.CodeSign.Requirements;

namespace Melanzana.CodeSign.Blobs
{
    public partial class RequirementsBlob
    {
        private static byte[] CreateDefaultRequirementContent(string bundleIdentifier, string certificateFriendlyName)
        {
            var utf8BundleIdentifier = Encoding.UTF8.GetBytes(bundleIdentifier);
            var utf8CertificateFriendlyName = Encoding.UTF8.GetBytes(certificateFriendlyName);
            var utf8SubjectCN = Encoding.UTF8.GetBytes("subject.CN");
            var oidRawBytes = new byte[] { 0x2a, 0x86, 0x48, 0x86, 0xf7, 0x63, 0x64, 0x06, 0x02, 0x01, 0x00, 0x00 };            
            var buffer = new byte[utf8BundleIdentifier.Length + utf8CertificateFriendlyName.Length + 1024];

            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)ExpressionOperation.And);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(4, 4), (uint)ExpressionOperation.Ident);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(8, 4), (uint)utf8BundleIdentifier.Length);
            utf8BundleIdentifier.CopyTo(buffer.AsSpan(12, utf8BundleIdentifier.Length));
            int offset = 12 + ((utf8BundleIdentifier.Length + 3) & ~3);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), (uint)ExpressionOperation.And);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 4, 4), (uint)ExpressionOperation.AppleGenericAnchor);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 8, 4), (uint)ExpressionOperation.And);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 12, 4), (uint)ExpressionOperation.CertField);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 16, 4), 0u); // Certificate slot: Leaf certificate
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 20, 4), (uint)utf8SubjectCN.Length);
            utf8SubjectCN.CopyTo(buffer.AsSpan(offset + 24, utf8SubjectCN.Length));
            offset += 24 + ((utf8SubjectCN.Length + 3) & ~3);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), (uint)ExpressionMatchType.Equal);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 4, 4), (uint)utf8CertificateFriendlyName.Length);
            utf8CertificateFriendlyName.CopyTo(buffer.AsSpan(offset + 8, utf8CertificateFriendlyName.Length));
            offset += 8 + ((utf8CertificateFriendlyName.Length + 3) & ~3);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), (uint)ExpressionOperation.CertGeneric);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 4, 4), 1u); // Certificate slot: 1
            // Binary string with OID 1.2.840.113635.100.6.2.1
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 8, 4), 0xa);
            oidRawBytes.CopyTo(buffer.AsSpan(offset + 12, oidRawBytes.Length));
            offset += 12 + ((oidRawBytes.Length + 3) & ~3);
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset, 4), (int)ExpressionMatchType.Exists);
            offset += 4;
            return buffer.AsSpan(0, offset).ToArray();
        }

        private static byte[] WrapDesignatedRequirement(byte[] requirementContent)
        {
            var blobBuffer = new byte[32 + requirementContent.Length];

            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(0, 4), (uint)BlobMagic.Requirements);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(4, 4), (uint)blobBuffer.Length);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(8, 4), 1u); // One requirement

            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(12, 4), (uint)RequirementType.Designated);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(16, 4), 20u); // Offset

            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(20, 4), (uint)BlobMagic.Requirement);
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(24, 4), (uint)(requirementContent.Length + 12));
            BinaryPrimitives.WriteUInt32BigEndian(blobBuffer.AsSpan(28, 4), 1u); // Expression
            requirementContent.CopyTo(blobBuffer.AsSpan(32, requirementContent.Length));

            return blobBuffer;
        }

        public static byte[] CreateDefault(string bundleIdentifier, string certificateFriendlyName)
        {
            if (string.IsNullOrEmpty(bundleIdentifier))
                throw new ArgumentNullException(nameof(bundleIdentifier));
            if (string.IsNullOrEmpty(certificateFriendlyName))
                throw new ArgumentNullException(nameof(certificateFriendlyName));

            var requirementContent = CreateDefaultRequirementContent(bundleIdentifier, certificateFriendlyName);
            return WrapDesignatedRequirement(requirementContent);
        }
    }
}