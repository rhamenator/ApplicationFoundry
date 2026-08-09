using System.Buffers.Binary;
using System.Text;

namespace ApplicationFoundry.Features.Fit;

internal static class TinyOnnxModel
{
    public static byte[] Build()
    {
        var graph = Message(writer =>
        {
            WriteMessage(writer, 1, Node("fit_linear", "Gemm", ["features", "weights", "bias"], ["logits"]));
            WriteMessage(writer, 1, Node("fit_probability", "Sigmoid", ["logits"], ["score"]));
            WriteString(writer, 2, "application_fit");
            WriteMessage(writer, 5, Tensor("weights", [4, 1], [1.35f, 1.1f, 0.75f, 0.8f]));
            WriteMessage(writer, 5, Tensor("bias", [1], [-1.65f]));
            WriteMessage(writer, 11, ValueInfo("features", [1, 4]));
            WriteMessage(writer, 12, ValueInfo("score", [1, 1]));
        });
        return Message(writer =>
        {
            WriteVarintField(writer, 1, 8);
            WriteString(writer, 2, "ApplicationFoundry");
            WriteMessage(writer, 7, graph);
            WriteMessage(writer, 8, Message(opset => WriteVarintField(opset, 2, 13)));
        });
    }

    private static byte[] Node(string name, string operation, string[] inputs, string[] outputs) =>
        Message(writer =>
        {
            foreach (var input in inputs) WriteString(writer, 1, input);
            foreach (var output in outputs) WriteString(writer, 2, output);
            WriteString(writer, 3, name);
            WriteString(writer, 4, operation);
        });

    private static byte[] Tensor(string name, long[] dimensions, float[] values) => Message(writer =>
    {
        foreach (var dimension in dimensions) WriteVarintField(writer, 1, (ulong)dimension);
        WriteVarintField(writer, 2, 1);
        Span<byte> bytes = stackalloc byte[4];
        foreach (var value in values)
        {
            WriteTag(writer, 4, 5);
            BinaryPrimitives.WriteSingleLittleEndian(bytes, value);
            writer.Write(bytes);
        }
        WriteString(writer, 8, name);
    });

    private static byte[] ValueInfo(string name, long[] dimensions) => Message(writer =>
    {
        WriteString(writer, 1, name);
        WriteMessage(writer, 2, Message(type => WriteMessage(type, 1, Message(tensorType =>
        {
            WriteVarintField(tensorType, 1, 1);
            WriteMessage(tensorType, 2, Message(shape =>
            {
                foreach (var dimension in dimensions)
                {
                    WriteMessage(shape, 1, Message(dim => WriteVarintField(dim, 1, (ulong)dimension)));
                }
            }));
        }))));
    });

    private static byte[] Message(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        write(writer);
        return stream.ToArray();
    }

    private static void WriteMessage(BinaryWriter writer, int field, byte[] value)
    {
        WriteTag(writer, field, 2);
        WriteVarint(writer, (ulong)value.Length);
        writer.Write(value);
    }

    private static void WriteString(BinaryWriter writer, int field, string value) =>
        WriteMessage(writer, field, Encoding.UTF8.GetBytes(value));

    private static void WriteVarintField(BinaryWriter writer, int field, ulong value)
    {
        WriteTag(writer, field, 0);
        WriteVarint(writer, value);
    }

    private static void WriteTag(BinaryWriter writer, int field, int wireType) =>
        WriteVarint(writer, (ulong)((field << 3) | wireType));

    private static void WriteVarint(BinaryWriter writer, ulong value)
    {
        while (value >= 0x80)
        {
            writer.Write((byte)(value | 0x80));
            value >>= 7;
        }
        writer.Write((byte)value);
    }
}
