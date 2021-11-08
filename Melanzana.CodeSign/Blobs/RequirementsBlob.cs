using System.Buffers.Binary;
using System.Text;
using Melanzana.CodeSign.Requirements;

namespace Melanzana.CodeSign.Blobs
{
    public partial class RequirementsBlob
    {
        private static byte[] CreateDefaultRequirementContent(string bundleIdentifier, string certificateFriendlyName)
        {
            var expression = Expression.And(
                Expression.Ident(bundleIdentifier),
                Expression.And(
                    Expression.AppleGenericAnchor,
                    Expression.And(
                        Expression.CertField(0, "subject.CN", ExpressionMatchType.Equal, certificateFriendlyName),
                        Expression.CertGeneric(1, "1.2.840.113635.100.6.2.1", ExpressionMatchType.Exists)
                    )
                )
            );

            var expressionBytes = new byte[expression.Size];
            expression.Write(expressionBytes, out var _);

            return expressionBytes;
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