using System;
using System.IO;
using System.Collections.Generic;
using SkiaSharp;

public sealed class CustomGifEncoder
{
    private Stream _stream;
    private byte[]? _globalColorTable;
    private readonly List<GifFrame> _frames;
    private ushort _width;
    private ushort _height;
    private readonly byte _backgroundColorIndex;
    
    public CustomGifEncoder()
    {
        _frames = new List<GifFrame>();
        _globalColorTable = null;
        _backgroundColorIndex = 0;
    }
    
    // Add a frame to the GIF
    public void AddFrame(SKColor[,] pixels, int delayMs = 100)
    {
        if (pixels == null)
            throw new ArgumentNullException(nameof(pixels));
        
        _width = (ushort)pixels.GetLength(1);
        _height = (ushort)pixels.GetLength(0);
        
        // Generate optimized color palette if not exists
        if (_globalColorTable == null)
        {
            _globalColorTable = GenerateColorTable(pixels);
        }
        
        // Convert pixels to indexed colors
        var indexedData = ConvertToIndexed(pixels);
        
        // Compress using LZW
        var compressedData = LzwCompress(indexedData);
        
        _frames.Add(new GifFrame
        {
            Data = compressedData,
            DelayTime = (ushort)(delayMs / 10), // Delay in hundredths of seconds
            Left = 0,
            Top = 0,
            Width = _width,
            Height = _height,
            Interlaced = false
        });
    }
    
    // Write the complete GIF to stream
    public void WriteToStream(Stream outputStream)
    {
        ArgumentNullException.ThrowIfNull(outputStream);

        _stream = outputStream;
        
        // Write GIF Header
        WriteString("GIF89a");
        
        // Write Logical Screen Descriptor
        WriteLogicalScreenDescriptor();
        
        // Write Global Color Table
        if (_globalColorTable != null)
        {
            _stream.Write(_globalColorTable, 0, _globalColorTable.Length);
        }
        
        // Write each frame
        for (var i = 0; i < _frames.Count; i++)
        {
            WriteGraphicsControlExtension(_frames[i]);
            WriteImageDescriptor(_frames[i]);
            WriteImageData(_frames[i]);
        }
        
        // Write Trailer
        _stream.WriteByte(0x3B);
    }
    
    private void WriteLogicalScreenDescriptor()
    {
        WriteUInt16(_width);
        WriteUInt16(_height);
        
        byte packedFields = 0;
        
        // Global Color Table Flag
        if (_globalColorTable != null)
            packedFields |= 0x80;
        
        // Color Resolution (7 = 8 bits per primary color)
        packedFields |= 0x70;
        
        // Sort Flag
        packedFields |= 0x00;
        
        // Size of Global Color Table (2^3 = 8 colors)
        packedFields |= 0x07;
        
        _stream.WriteByte(packedFields);
        _stream.WriteByte(_backgroundColorIndex);
        _stream.WriteByte(0); // Pixel Aspect Ratio
    }
    
    private void WriteGraphicsControlExtension(GifFrame frame)
    {
        _stream.WriteByte(0x21); // Extension Introducer
        _stream.WriteByte(0xF9); // Graphic Control Label
        _stream.WriteByte(0x04); // Block Size
        
        byte packedFields = 0;
        packedFields |= 0x00; // Disposal Method
        packedFields |= 0x00; // User Input Flag
        packedFields |= 0x00; // Transparent Color Flag
        
        _stream.WriteByte(packedFields);
        WriteUInt16(frame.DelayTime);
        _stream.WriteByte(0x00); // Transparent Color Index
        _stream.WriteByte(0x00); // Block Terminator
    }
    
    private void WriteImageDescriptor(GifFrame frame)
    {
        _stream.WriteByte(0x2C); // Image Separator
        WriteUInt16(frame.Left);
        WriteUInt16(frame.Top);
        WriteUInt16(frame.Width);
        WriteUInt16(frame.Height);
        
        byte packedFields = 0;
        packedFields |= 0x00; // Local Color Table Flag
        packedFields |= 0x00; // Interlace Flag
        packedFields |= 0x00; // Sort Flag
        packedFields |= 0x00; // Reserved
        
        _stream.WriteByte(packedFields);
    }
    
    private void WriteImageData(GifFrame frame)
    {
        _stream.WriteByte(0x08); // LZW Minimum Code Size
        _stream.Write(frame.Data, 0, frame.Data.Length);
        _stream.WriteByte(0x00); // Block Terminator
    }
    
    private byte[] GenerateColorTable(SKColor[,] pixels)
    {
        // Simple color quantization - extract unique colors
        var colors = new List<SKColor>();
        var colorSet = new HashSet<int>();
        
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var c = pixels[y, x];
                var rgb = (c.Red << 16) | (c.Green << 8) | c.Blue;
                
                if (!colorSet.Contains(rgb))
                {
                    colorSet.Add(rgb);
                    colors.Add(c);
                    
                    if (colors.Count >= 256)
                        break;
                }
            }
        }
        
        // Create color table (3 bytes per color: R, G, B)
        var colorTable = new byte[256 * 3];
        
        for (var i = 0; i < colors.Count; i++)
        {
            colorTable[i * 3] = colors[i].Red;
            colorTable[i * 3 + 1] = colors[i].Green;
            colorTable[i * 3 + 2] = colors[i].Blue;
        }
        
        return colorTable;
    }
    
    private byte[] ConvertToIndexed(SKColor[,] pixels)
    {
        var indexed = new byte[_width * _height];
        
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var c = pixels[y, x];
                var closestIndex = 0;
                var minDistance = int.MaxValue;
                
                // Find closest color in color table
                for (var i = 0; i < 256; i++)
                {
                    var rDiff = c.Red - _globalColorTable[i * 3];
                    var gDiff = c.Green - _globalColorTable[i * 3 + 1];
                    var bDiff = c.Blue - _globalColorTable[i * 3 + 2];
                    var distance = rDiff * rDiff + gDiff * gDiff + bDiff * bDiff;
                    
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestIndex = i;
                    }
                }
                
                indexed[y * _width + x] = (byte)closestIndex;
            }
        }
        
        return indexed;
    }
    
    private byte[] LzwCompress(byte[] data)
    {
        var output = new List<byte>();
        var minCodeSize = 8;
        var maxCodeSize = 12;
        
        // Initialize dictionary with 2^minCodeSize entries
        var dictionary = new Dictionary<string, int>();
        for (var i = 0; i < (1 << minCodeSize); i++)
        {
            dictionary.Add(((char)i).ToString(), i);
        }
        
        var nextCode = (1 << minCodeSize) + 2;
        var codeSize = minCodeSize + 1;
        
        var current = "";
        var compressedCodes = new List<int>();
        
        // Add clear code
        compressedCodes.Add(1 << minCodeSize);
        
        foreach (var b in data)
        {
            var next = current + (char)b;
            
            if (dictionary.ContainsKey(next))
            {
                current = next;
            }
            else
            {
                compressedCodes.Add(dictionary[current]);
                
                if (nextCode < (1 << maxCodeSize))
                {
                    dictionary.Add(next, nextCode++);
                }
                
                current = ((char)b).ToString();
            }
        }
        
        if (!string.IsNullOrEmpty(current))
        {
            compressedCodes.Add(dictionary[current]);
        }
        
        // Add end code
        compressedCodes.Add((1 << minCodeSize) + 1);
        
        // Write codes in variable-length format
        var bitStream = new List<byte>();
        var bitsBuffer = 0;
        var bitsCount = 0;
        
        foreach (var code in compressedCodes)
        {
            bitsBuffer |= (code << bitsCount);
            bitsCount += codeSize;
            
            while (bitsCount >= 8)
            {
                bitStream.Add((byte)(bitsBuffer & 0xFF));
                bitsBuffer >>= 8;
                bitsCount -= 8;
            }
        }
        
        if (bitsCount > 0)
        {
            bitStream.Add((byte)(bitsBuffer & 0xFF));
        }
        
        // Write in blocks of up to 255 bytes
        for (var i = 0; i < bitStream.Count; i += 255)
        {
            var blockSize = Math.Min(255, bitStream.Count - i);
            output.Add((byte)blockSize);
            output.AddRange(bitStream.GetRange(i, blockSize));
        }
        
        return output.ToArray();
    }
    
    private void WriteUInt16(ushort value)
    {
        _stream.WriteByte((byte)(value & 0xFF));
        _stream.WriteByte((byte)((value >> 8) & 0xFF));
    }
    
    private void WriteString(string value)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        _stream.Write(bytes, 0, bytes.Length);
    }
    
    private class GifFrame
    {
        public byte[] Data { get; set; }
        public ushort DelayTime { get; set; }
        public ushort Left { get; set; }
        public ushort Top { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public bool Interlaced { get; set; }
    }
}

