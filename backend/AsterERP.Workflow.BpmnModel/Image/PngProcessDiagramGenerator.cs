using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace AsterERP.Workflow.BpmnModel.Image;

public sealed class PngProcessDiagramGenerator : IPngProcessDiagramGenerator
{
    private const int Padding = 20;
    private static readonly RgbaColor White = new(255, 255, 255, 255);
    private static readonly RgbaColor Stroke = new(66, 66, 66, 255);
    private static readonly RgbaColor Fill = new(224, 242, 254, 255);
    private static readonly RgbaColor EventFill = new(236, 253, 245, 255);

    public byte[] GeneratePng(BpmnModel bpmnModel, string? processId = null)
    {
        ArgumentNullException.ThrowIfNull(bpmnModel);

        var process = ResolveProcess(bpmnModel, processId);
        var layout = DiagramLayout.CalculateLayout(process);
        var bounds = CalculateBounds(layout);

        var width = Math.Max(128, (int)Math.Ceiling(bounds.MaxX - bounds.MinX + (Padding * 2)));
        var height = Math.Max(96, (int)Math.Ceiling(bounds.MaxY - bounds.MinY + (Padding * 2)));
        var pixels = new byte[width * height * 4];

        FillImage(pixels, width, height, White);
        DrawSequenceFlows(process, layout, bounds, pixels, width, height);
        DrawFlowElements(process, layout, bounds, pixels, width, height);

        return EncodePng(width, height, pixels);
    }

    private static Process ResolveProcess(BpmnModel bpmnModel, string? processId)
    {
        Process? process;
        if (string.IsNullOrWhiteSpace(processId))
        {
            process = bpmnModel.Processes.FirstOrDefault();
        }
        else
        {
            process = bpmnModel.GetProcessById(processId);
        }

        if (process == null)
        {
            throw new InvalidOperationException("No process found to generate PNG diagram.");
        }

        return process;
    }

    private static void DrawFlowElements(
        Process process,
        IReadOnlyDictionary<string, GraphicInfo> layout,
        Bounds bounds,
        byte[] pixels,
        int width,
        int height)
    {
        foreach (var element in process.FlowElements.Where(flowElement => flowElement is not SequenceFlow))
        {
            if (string.IsNullOrWhiteSpace(element.Id) || !layout.TryGetValue(element.Id, out var gi))
            {
                continue;
            }

            var x = (int)Math.Round(gi.X - bounds.MinX + Padding);
            var y = (int)Math.Round(gi.Y - bounds.MinY + Padding);
            var w = Math.Max(6, (int)Math.Round(gi.Width));
            var h = Math.Max(6, (int)Math.Round(gi.Height));

            if (element is Event)
            {
                var radius = Math.Min(w, h) / 2;
                var centerX = x + (w / 2);
                var centerY = y + (h / 2);
                FillCircle(pixels, width, height, centerX, centerY, radius, EventFill);
                DrawCircle(pixels, width, height, centerX, centerY, radius, Stroke);
                continue;
            }

            if (element is Gateway)
            {
                FillDiamond(pixels, width, height, x, y, w, h, Fill);
                DrawDiamond(pixels, width, height, x, y, w, h, Stroke);
                continue;
            }

            FillRect(pixels, width, height, x, y, w, h, Fill);
            DrawRect(pixels, width, height, x, y, w, h, Stroke);
        }
    }

    private static void DrawSequenceFlows(
        Process process,
        IReadOnlyDictionary<string, GraphicInfo> layout,
        Bounds bounds,
        byte[] pixels,
        int width,
        int height)
    {
        foreach (var sequenceFlow in process.FlowElements.OfType<SequenceFlow>())
        {
            if (string.IsNullOrWhiteSpace(sequenceFlow.SourceRef) ||
                string.IsNullOrWhiteSpace(sequenceFlow.TargetRef) ||
                !layout.TryGetValue(sequenceFlow.SourceRef, out var source) ||
                !layout.TryGetValue(sequenceFlow.TargetRef, out var target))
            {
                continue;
            }

            var startX = (int)Math.Round(source.X - bounds.MinX + Padding + (source.Width / 2.0));
            var startY = (int)Math.Round(source.Y - bounds.MinY + Padding + (source.Height / 2.0));
            var endX = (int)Math.Round(target.X - bounds.MinX + Padding + (target.Width / 2.0));
            var endY = (int)Math.Round(target.Y - bounds.MinY + Padding + (target.Height / 2.0));
            DrawLine(pixels, width, height, startX, startY, endX, endY, Stroke);
        }
    }

    private static void FillImage(byte[] pixels, int width, int height, RgbaColor color)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                SetPixel(pixels, width, height, x, y, color);
            }
        }
    }

    private static void FillRect(byte[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, RgbaColor color)
    {
        for (var yy = y; yy < y + rectHeight; yy++)
        {
            for (var xx = x; xx < x + rectWidth; xx++)
            {
                SetPixel(pixels, width, height, xx, yy, color);
            }
        }
    }

    private static void DrawRect(byte[] pixels, int width, int height, int x, int y, int rectWidth, int rectHeight, RgbaColor color)
    {
        DrawLine(pixels, width, height, x, y, x + rectWidth, y, color);
        DrawLine(pixels, width, height, x, y, x, y + rectHeight, color);
        DrawLine(pixels, width, height, x + rectWidth, y, x + rectWidth, y + rectHeight, color);
        DrawLine(pixels, width, height, x, y + rectHeight, x + rectWidth, y + rectHeight, color);
    }

    private static void FillCircle(byte[] pixels, int width, int height, int centerX, int centerY, int radius, RgbaColor color)
    {
        var radiusSquared = radius * radius;
        for (var y = -radius; y <= radius; y++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                if ((x * x) + (y * y) <= radiusSquared)
                {
                    SetPixel(pixels, width, height, centerX + x, centerY + y, color);
                }
            }
        }
    }

    private static void DrawCircle(byte[] pixels, int width, int height, int centerX, int centerY, int radius, RgbaColor color)
    {
        var x = radius;
        var y = 0;
        var decision = 1 - x;

        while (y <= x)
        {
            SetPixel(pixels, width, height, centerX + x, centerY + y, color);
            SetPixel(pixels, width, height, centerX + y, centerY + x, color);
            SetPixel(pixels, width, height, centerX - y, centerY + x, color);
            SetPixel(pixels, width, height, centerX - x, centerY + y, color);
            SetPixel(pixels, width, height, centerX - x, centerY - y, color);
            SetPixel(pixels, width, height, centerX - y, centerY - x, color);
            SetPixel(pixels, width, height, centerX + y, centerY - x, color);
            SetPixel(pixels, width, height, centerX + x, centerY - y, color);

            y++;
            if (decision <= 0)
            {
                decision += (2 * y) + 1;
            }
            else
            {
                x--;
                decision += (2 * (y - x)) + 1;
            }
        }
    }

    private static void FillDiamond(byte[] pixels, int width, int height, int x, int y, int diamondWidth, int diamondHeight, RgbaColor color)
    {
        var centerX = x + (diamondWidth / 2);
        var centerY = y + (diamondHeight / 2);
        var halfWidth = Math.Max(1, diamondWidth / 2);
        var halfHeight = Math.Max(1, diamondHeight / 2);

        for (var yy = y; yy <= y + diamondHeight; yy++)
        {
            for (var xx = x; xx <= x + diamondWidth; xx++)
            {
                var normalized = Math.Abs(xx - centerX) / (double)halfWidth + Math.Abs(yy - centerY) / (double)halfHeight;
                if (normalized <= 1.0)
                {
                    SetPixel(pixels, width, height, xx, yy, color);
                }
            }
        }
    }

    private static void DrawDiamond(byte[] pixels, int width, int height, int x, int y, int diamondWidth, int diamondHeight, RgbaColor color)
    {
        var centerX = x + (diamondWidth / 2);
        var centerY = y + (diamondHeight / 2);
        DrawLine(pixels, width, height, centerX, y, x + diamondWidth, centerY, color);
        DrawLine(pixels, width, height, x + diamondWidth, centerY, centerX, y + diamondHeight, color);
        DrawLine(pixels, width, height, centerX, y + diamondHeight, x, centerY, color);
        DrawLine(pixels, width, height, x, centerY, centerX, y, color);
    }

    private static void DrawLine(byte[] pixels, int width, int height, int x0, int y0, int x1, int y1, RgbaColor color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            SetPixel(pixels, width, height, x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var e2 = 2 * error;
            if (e2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixel(byte[] pixels, int width, int height, int x, int y, RgbaColor color)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = (y * width + x) * 4;
        pixels[index] = color.R;
        pixels[index + 1] = color.G;
        pixels[index + 2] = color.B;
        pixels[index + 3] = color.A;
    }

    private static Bounds CalculateBounds(IReadOnlyDictionary<string, GraphicInfo> layout)
    {
        if (layout.Count == 0)
        {
            return new Bounds(0, 0, 128, 96);
        }

        var minX = layout.Values.Min(info => info.X);
        var minY = layout.Values.Min(info => info.Y);
        var maxX = layout.Values.Max(info => info.X + info.Width);
        var maxY = layout.Values.Max(info => info.Y + info.Height);
        return new Bounds(minX, minY, maxX, maxY);
    }

    private static byte[] EncodePng(int width, int height, byte[] rgbaPixels)
    {
        var scanlineLength = (width * 4) + 1;
        var raw = new byte[scanlineLength * height];

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * scanlineLength;
            raw[rowOffset] = 0; // filter type: None
            Buffer.BlockCopy(
                rgbaPixels,
                y * width * 4,
                raw,
                rowOffset + 1,
                width * 4);
        }

        byte[] compressed;
        using (var compressedStream = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compressedStream, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write(raw, 0, raw.Length);
            }

            compressed = compressedStream.ToArray();
        }

        using var output = new MemoryStream();
        output.Write(PngConstants.Signature, 0, PngConstants.Signature.Length);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // color type RGBA
        ihdr[10] = 0; // compression method
        ihdr[11] = 0; // filter method
        ihdr[12] = 0; // interlace method

        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IDAT", compressed);
        WriteChunk(output, "IEND", Array.Empty<byte>());
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string chunkType, byte[] data)
    {
        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lengthBytes, data.Length);
        output.Write(lengthBytes);

        var typeBytes = Encoding.ASCII.GetBytes(chunkType);
        output.Write(typeBytes, 0, typeBytes.Length);
        if (data.Length > 0)
        {
            output.Write(data, 0, data.Length);
        }

        var crc = ComputeCrc(typeBytes, data);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint ComputeCrc(byte[] chunkType, byte[] chunkData)
    {
        uint crc = 0xFFFFFFFF;
        crc = UpdateCrc(crc, chunkType);
        crc = UpdateCrc(crc, chunkData);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateCrc(uint crc, byte[] data)
    {
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ 0xEDB88320
                    : crc >> 1;
            }
        }

        return crc;
    }

    private readonly record struct Bounds(double MinX, double MinY, double MaxX, double MaxY);

    private readonly record struct RgbaColor(byte R, byte G, byte B, byte A);
}

internal static class PngConstants
{
    public static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
}
