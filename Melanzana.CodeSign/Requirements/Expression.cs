using System.Buffers.Binary;
using System.Text;
using System.Formats.Asn1;

namespace Melanzana.CodeSign.Requirements
{
    public abstract class Expression
    {
        public abstract int Size { get; }

        public abstract void Write(Span<byte> buffer, out int bytesWritten);

        public static Expression False { get; } = new SimpleExpression(ExpressionOperation.False);
        public static Expression True { get; } = new SimpleExpression(ExpressionOperation.True);
        public static Expression Ident(string identifier) => new StringExpression(ExpressionOperation.Ident, identifier);
        public static Expression AppleAnchor { get; } = new SimpleExpression(ExpressionOperation.AppleAnchor);
        public static Expression AnchorHash(byte[] anchorHash) => new DataExpression(ExpressionOperation.AnchorHash, anchorHash);
        // InfoKeyValue
        public static Expression And(Expression left, Expression right) => new BinaryOperatorExpression(ExpressionOperation.And, left, right);
        public static Expression Or(Expression left, Expression right) => new BinaryOperatorExpression(ExpressionOperation.Or, left, right);
        public static Expression CDHash(byte[] codeDirectoryHash) => new DataExpression(ExpressionOperation.CDHash, codeDirectoryHash);
        public static Expression Not(Expression inner) => new UnaryOperatorExpression(ExpressionOperation.Not, inner);
        // InfoKeyField,
        public static Expression CertField(int certificateIndex, string certificateField, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertField, certificateIndex, certificateField, matchType, matchValue);
        // TrustedCert,
        public static Expression TrustedCerts { get; } = new SimpleExpression(ExpressionOperation.TrustedCerts);
        public static Expression CertGeneric(int certificateIndex, string certificateFieldOid, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertGeneric, certificateIndex, GetOidBytes(certificateFieldOid), matchType, matchValue);
        public static Expression AppleGenericAnchor { get; } = new SimpleExpression(ExpressionOperation.AppleGenericAnchor);
        // EntitlementField,
        public static Expression CertPolicy(int certificateIndex, string certificateFieldOid, ExpressionMatchType matchType, string? matchValue = null)
            => new CertExpression(ExpressionOperation.CertPolicy, certificateIndex, GetOidBytes(certificateFieldOid), matchType, matchValue);
        // NamedAnchor,
        // NamedCode,
        // Platform,
        public static Expression Notarized { get; } = new SimpleExpression(ExpressionOperation.Notarized);
        // CertFieldDate,
        public static Expression LegacyDevID { get; } = new SimpleExpression(ExpressionOperation.LegacyDevID);

        private int Align(int size) => (size + 3) & ~3;

        private static byte[] GetOidBytes(string oid)
        {
            var asnWriter = new AsnWriter(AsnEncodingRules.DER);
            asnWriter.WriteObjectIdentifier(oid);
            return asnWriter.Encode().AsSpan(2).ToArray();
        }

        class SimpleExpression : Expression
        {
            ExpressionOperation op;

            public SimpleExpression(ExpressionOperation op)
            {
                this.op = op;
            }

            public override int Size => 4;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                bytesWritten = 4;
            }
        }

        class BinaryOperatorExpression : Expression
        {
            ExpressionOperation op;
            Expression left;
            Expression right;

            public BinaryOperatorExpression(ExpressionOperation op, Expression left, Expression right)
            {
                this.op = op;
                this.left = left;
                this.right = right;
            }

            public override int Size => 4 + left.Size + right.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                left.Write(buffer.Slice(4), out var bytesWrittenLeft);
                right.Write(buffer.Slice(4 + bytesWrittenLeft), out var bytesWrittenRight);
                bytesWritten = 4 + bytesWrittenLeft + bytesWrittenRight;
            }
        }

        class UnaryOperatorExpression : Expression
        {
            ExpressionOperation op;
            Expression inner;

            public UnaryOperatorExpression(ExpressionOperation op, Expression inner)
            {
                this.op = op;
                this.inner = inner;
            }

            public override int Size => 4 + inner.Size;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                inner.Write(buffer.Slice(4), out var bytesWrittenInner);
                bytesWritten = 4 + bytesWrittenInner;
            }
        }

        class StringExpression : Expression
        {
            ExpressionOperation op;
            string opString;

            public StringExpression(ExpressionOperation op, string opString)
            {
                this.op = op;
                this.opString = opString;
            }

            public override int Size => 8 + Align(Encoding.UTF8.GetByteCount(opString));

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                byte[] opStringBytes = Encoding.UTF8.GetBytes(opString);
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4), opStringBytes.Length);
                opStringBytes.CopyTo(buffer.Slice(8, opStringBytes.Length));
                buffer.Slice(8 + opStringBytes.Length, Align(opStringBytes.Length) - opStringBytes.Length).Fill((byte)0);
                bytesWritten = 8 + Align(opStringBytes.Length);
            }
        }

        class DataExpression : Expression
        {
            ExpressionOperation op;
            byte[] opData;

            public DataExpression(ExpressionOperation op, byte[] opData)
            {
                this.op = op;
                this.opData = opData;
            }

            public override int Size => 4 + opData.Length;

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                opData.CopyTo(buffer.Slice(4, opData.Length));
                bytesWritten = 4 + opData.Length;
            }
        }

        class CertExpression : Expression
        {
            ExpressionOperation op;
            int certificateIndex;
            object certificateField;
            byte[] certificateFieldBytes;
            ExpressionMatchType matchType;
            object? matchValue;
            byte[] matchValueBytes;

            public CertExpression(
                ExpressionOperation op,
                int certificateIndex,
                object certificateField, // string or byte[]
                ExpressionMatchType matchType,
                object? matchValue)
            {
                this.op = op;
                this.certificateIndex = certificateIndex;
                this.certificateField = certificateField;
                this.matchType = matchType;
                this.matchValue = matchValue;

                certificateFieldBytes = certificateField is string certificateFieldString ? 
                    Encoding.UTF8.GetBytes(certificateFieldString) :
                    (byte[])certificateField;
                matchValueBytes = matchValue is string matchValueString ?
                    Encoding.UTF8.GetBytes(matchValueString) :
                    (byte[])matchValue;
            }

            public override int Size =>
                16 +
                Align(certificateFieldBytes.Length) +
                (matchValue != null ? 4 + Align(matchValueBytes.Length) : 0);

            public override void Write(Span<byte> buffer, out int bytesWritten)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)op);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(4), certificateIndex);
                BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(8), certificateFieldBytes.Length);
                certificateFieldBytes.CopyTo(buffer.Slice(12, certificateFieldBytes.Length));
                buffer.Slice(12 + certificateFieldBytes.Length, Align(certificateFieldBytes.Length) - certificateFieldBytes.Length).Fill((byte)0);
                bytesWritten = 12 + Align(certificateFieldBytes.Length);
                BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(bytesWritten), (uint)matchType);
                bytesWritten += 4;
                if (matchValue != null)
                {
                    BinaryPrimitives.WriteInt32BigEndian(buffer.Slice(bytesWritten), matchValueBytes.Length);
                    matchValueBytes.CopyTo(buffer.Slice(4 + bytesWritten, matchValueBytes.Length));
                    buffer.Slice(4 + matchValueBytes.Length, Align(matchValueBytes.Length) - matchValueBytes.Length).Fill((byte)0);
                    bytesWritten += 4 + Align(matchValueBytes.Length);
                }
            }
        }
    }
}
