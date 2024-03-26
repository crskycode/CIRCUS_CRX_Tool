using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

#pragma warning disable IDE0017
#pragma warning disable IDE0063
#pragma warning disable IDE0066
#pragma warning disable IDE0090
#pragma warning disable IDE1006

namespace CIRCUS_CRX
{
    class CRXG
    {
        #region Metadata

        class Clip
        {
            public int field_0 { get; set; }
            public int field_4 { get; set; }
            public int field_6 { get; set; }
            public int field_8 { get; set; }
            public int field_A { get; set; }
            public int field_C { get; set; }
            public int field_E { get; set; }
        }

        int _inner_x;
        int _inner_y;
        int _width;
        int _height;
        int _version;
        int _flags;
        int _bpp; // bpp ( palette size )
        int _unknow;

        readonly List<Clip> _clips = new();

        #endregion

        // Compressed pixel buffer
        byte[] _compr_data;
        // Unpacked pixel buffer
        byte[] _data;

        public void Load(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var reader = new BinaryReader(stream))
            {
                Read(reader);
            }
        }

        public void Save(string filePath)
        {
            using (var stream = File.Create(filePath))
            using (var writer = new BinaryWriter(stream))
            {
                Write(writer);
            }
        }

        void Read(BinaryReader reader)
        {
            if (reader.ReadInt32() != 0x47585243)
            {
                throw new Exception("The file is not a valid CRXG file.");
            }

            _inner_x = reader.ReadInt16();
            _inner_y = reader.ReadInt16();

            _width = reader.ReadInt16();
            _height = reader.ReadInt16();

            _version = reader.ReadInt16();
            _flags = reader.ReadInt16();

            _bpp = reader.ReadInt16();
            _unknow = reader.ReadInt16();

            if (_version != 2 && _version != 3)
            {
                throw new Exception("The version of the file is not supported.");
            }

            if ((_flags & 0xF) > 1)
            {
                throw new Exception("The file contains an unsupported flag.");
            }

            if (_bpp != 0 && _bpp != 1)
            {
                throw new Exception("The BPP of the image is not supported.");
            }

            _clips.Clear();

            if (_version >= 3)
            {
                int clipCount = reader.ReadInt32();

                for (int i = 0; i < clipCount; i++)
                {
                    _clips.Add(new Clip
                    {
                        field_0 = reader.ReadInt32(),
                        field_4 = reader.ReadInt16(),
                        field_6 = reader.ReadInt16(),
                        field_8 = reader.ReadInt16(),
                        field_A = reader.ReadInt16(),
                        field_C = reader.ReadInt16(),
                        field_E = reader.ReadInt16(),
                    });
                }
            }

            int comprSize;

            if ((_flags & 0x10) == 0)
                comprSize = Convert.ToInt32(reader.BaseStream.Length - reader.BaseStream.Position);
            else
                comprSize = reader.ReadInt32();

            _compr_data = reader.ReadBytes(comprSize);

            var data = ZipLib.Inflate(_compr_data, _width * _height * 4);

            switch (_bpp)
            {
                case 0:
                    _data = new byte[3 * _width * _height];
                    DecodeImage(_data, data, _width, _height, 3);
                    break;
                case 1:
                    _data = new byte[4 * _width * _height];
                    DecodeImage(_data, data, _width, _height, 4);
                    break;
            }
        }

        void Write(BinaryWriter writer)
        {
            if (_compr_data == null)
                return;

            if (_version >= 3)
            {
                // Write data block length to file
                _flags |= 0x10;
            }

            writer.Write(0x47585243);

            writer.Write(Convert.ToInt16(_inner_x));
            writer.Write(Convert.ToInt16(_inner_y));

            writer.Write(Convert.ToInt16(_width));
            writer.Write(Convert.ToInt16(_height));

            writer.Write(Convert.ToInt16(_version));
            writer.Write(Convert.ToInt16(_flags));

            writer.Write(Convert.ToInt16(_bpp));
            writer.Write(Convert.ToInt16(_unknow));

            if (_version >= 3)
            {
                writer.Write(_clips.Count);

                foreach (var e in _clips)
                {
                    writer.Write(e.field_0);
                    writer.Write(Convert.ToInt16(e.field_4));
                    writer.Write(Convert.ToInt16(e.field_6));
                    writer.Write(Convert.ToInt16(e.field_8));
                    writer.Write(Convert.ToInt16(e.field_A));
                    writer.Write(Convert.ToInt16(e.field_C));
                    writer.Write(Convert.ToInt16(e.field_E));
                }
            }

            if ((_flags & 0x10) != 0)
            {
                // Version >= 3
                writer.Write(_compr_data.Length);
            }

            writer.Write(_compr_data);

            writer.Flush();
        }

        class Metadata
        {
            public int InnerX { get; set; }
            public int InnerY { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Version { get; set; }
            public int Flags { get; set; }
            public int Bpp { get; set; }
            public int Unknow { get; set; }
            public List<Clip> Clips { get; set; }
        }

        public void ExportMetadata(string filePath)
        {
            var metadata = new Metadata
            {
                InnerX = _inner_x,
                InnerY = _inner_y,
                Width = _width,
                Height = _height,
                Version = _version,
                Flags = _flags,
                Bpp = _bpp,
                Unknow = _unknow,
                Clips = _clips
            };

            JsonSerializerOptions options = new JsonSerializerOptions();
            options.WriteIndented = true;
            var json = JsonSerializer.Serialize(metadata, options);
            File.WriteAllText(filePath, json);
        }

        public void ImportMetadata(string filePath)
        {
            string json = File.ReadAllText(filePath);
            var metadata = JsonSerializer.Deserialize<Metadata>(json);

            _inner_x = metadata.InnerX;
            _inner_y = metadata.InnerY;
            _width = metadata.Width;
            _height = metadata.Height;
            _version = metadata.Version;
            _flags = metadata.Flags;
            _bpp = metadata.Bpp;
            _unknow = metadata.Unknow;
            _clips.Clear();
            _clips.AddRange(metadata.Clips);

            _compr_data = null;
            _data = null;
        }

        public void ExportAsPng(string filePath)
        {
            if (_data == null)
                return;

            switch (_bpp)
            {
                case 0:
                {
                    using var image = Image.LoadPixelData<Bgr24>(_data, _width, _height);
                    image.SaveAsPng(filePath);
                    break;
                }
                case 1:
                {
                    using var image = Image.LoadPixelData<Bgra32>(_data, _width, _height);
                    image.SaveAsPng(filePath);
                    break;
                }
            }
        }

        public void ImportFromPng(string filePath)
        {
            using var image = Image.Load(filePath);

            if (image.Width != _width || image.Height != _height)
            {
                _width = image.Width;
                _height = image.Height;

                Console.WriteLine("WARNING: The width and height of the image file do not match the metadata.");
            }

            if ((image.PixelType.BitsPerPixel == 24 && _bpp != 0) ||
                (image.PixelType.BitsPerPixel == 32 && _bpp != 1))
            {
                Console.WriteLine("WARNING: The BPP of the image file do not match the metadata.");
            }

            byte[] data = null;

            switch (image.PixelType.BitsPerPixel)
            {
                case 24:
                    data = EncodeImageBpp24(image);
                    _bpp = 0;
                    break;
                case 32:
                    data = EncodeImageBpp32(image);
                    _bpp = 1;
                    break;
                default:
                    throw new Exception("The file format is not supported.");
            }

            _compr_data = ZipLib.Deflate(data);
        }

        static int DecodeRow0(byte[] dst, int dst_p, byte[] src, int src_p, int width, int pixel_size)
        {
            var prev_p = dst_p;

            for (var i = 0; i < pixel_size; i++)
            {
                dst[dst_p++] = src[src_p++];
            }

            var remaining = width - 1;

            for (var i = 0; i < remaining; i++)
            {
                for (var j = 0; j < pixel_size; j++)
                {
                    dst[dst_p++] = (byte)(src[src_p++] + dst[prev_p++]);
                }
            }

            return src_p;
        }

        static int DecodeRow1(byte[] dst, int dst_p, byte[] src, int src_p, int width, int pixel_size, int prev_row_p)
        {
            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < pixel_size; j++)
                {
                    dst[dst_p++] = (byte)(src[src_p++] + dst[prev_row_p++]);
                }
            }

            return src_p;
        }

        static int DecodeRow2(byte[] dst, int dst_p, byte[] src, int src_p, int width, int pixel_size, int prev_row_p)
        {
            for (var i = 0; i < pixel_size; i++)
            {
                dst[dst_p++] = src[src_p++];
            }

            var remaining = width - 1;

            for (var i = 0; i < remaining; i++)
            {
                for (var j = 0; j < pixel_size; j++)
                {
                    dst[dst_p++] = (byte)(src[src_p++] + dst[prev_row_p++]);
                }
            }

            return src_p;
        }

        static int DecodeRow3(byte[] dst, int dst_p, byte[] src, int src_p, int width, int pixel_size, int prev_row_p)
        {
            prev_row_p += pixel_size;

            var count = width - 1;

            for (var i = 0; i < count; i++)
            {
                for (var j = 0; j < pixel_size; j++)
                {
                    dst[dst_p++] = (byte)(src[src_p++] + dst[prev_row_p++]);
                }
            }

            for (var i = 0; i < pixel_size; i++)
            {
                dst[dst_p++] = src[src_p++];
            }

            return src_p;
        }

        static int DecodeRow4(byte[] dst, int dst_p, byte[] src, int src_p, int width, int pixel_size)
        {
            for (var offset = 0; offset < pixel_size; offset++)
            {
                var dst_c = dst_p + offset;
                var remaining = width;
                do
                {
                    var value = src[src_p++];
                    dst[dst_c] = value;
                    dst_c += pixel_size;
                    remaining--;
                    if (remaining == 0)
                        break;
                    if (value == src[src_p])
                    {
                        src_p++;
                        var count = src[src_p++];
                        remaining -= count;
                        for (var j = 0; j < count; j++)
                        {
                            dst[dst_c] = value;
                            dst_c += pixel_size;
                        }
                    }
                } while (remaining > 0);
            }

            return src_p;
        }

        static void DecodeImage(byte[] dst, byte[] src, int width, int height, int pixel_size)
        {
            var src_p = 0;
            var dst_p = 0;

            var prev_row_p = 0;

            for (int y = 0; y < height; y++)
            {
                switch (src[src_p++])
                {
                    case 0:
                        src_p = DecodeRow0(dst, dst_p, src, src_p, width, pixel_size);
                        break;
                    case 1:
                        src_p = DecodeRow1(dst, dst_p, src, src_p, width, pixel_size, prev_row_p);
                        break;
                    case 2:
                        src_p = DecodeRow2(dst, dst_p, src, src_p, width, pixel_size, prev_row_p);
                        break;
                    case 3:
                        src_p = DecodeRow3(dst, dst_p, src, src_p, width, pixel_size, prev_row_p);
                        break;
                    case 4:
                        src_p = DecodeRow4(dst, dst_p, src, src_p, width, pixel_size);
                        break;
                    default:
                        throw new InvalidDataException();
                }

                prev_row_p = dst_p;
                dst_p += pixel_size * width;
            }

            if (pixel_size == 4)
            {
                for (var p = 0; p < dst.Length; p += pixel_size)
                {
                    var a = dst[p + 0];
                    var b = dst[p + 1];
                    var g = dst[p + 2];
                    var r = dst[p + 3];

                    dst[p + 0] = b;
                    dst[p + 1] = g;
                    dst[p + 2] = r;
                    dst[p + 3] = (byte)(0xFF - a);
                }
            }
        }

        static int EncodeBpp24Row0(byte[] dst, int dst_p, Image<Bgr24> source, int y)
        {

            dst[dst_p++] = source[0, y].B;
            dst[dst_p++] = source[0, y].G;
            dst[dst_p++] = source[0, y].R;

            for (int x1 = 1, x2 = 0; x1 < source.Width; x1++, x2++)
            {
                dst[dst_p++] = (byte)(source[x1, y].B - source[x2, y].B);
                dst[dst_p++] = (byte)(source[x1, y].G - source[x2, y].G);
                dst[dst_p++] = (byte)(source[x1, y].R - source[x2, y].R);
            }

            return dst_p;
        }

        static int EncodeBpp32Row0(byte[] dst, int dst_p, Image<Bgra32> source, int y)
        {
            dst[dst_p++] = source[0, y].A;
            dst[dst_p++] = source[0, y].B;
            dst[dst_p++] = source[0, y].G;
            dst[dst_p++] = source[0, y].R;

            for (int x1 = 1, x2 = 0; x1 < source.Width; x1++, x2++)
            {
                dst[dst_p++] = (byte)(source[x1, y].A - source[x2, y].A);
                dst[dst_p++] = (byte)(source[x1, y].B - source[x2, y].B);
                dst[dst_p++] = (byte)(source[x1, y].G - source[x2, y].G);
                dst[dst_p++] = (byte)(source[x1, y].R - source[x2, y].R);
            }

            return dst_p;
        }

        static byte[] EncodeImageBpp24(Image image)
        {
            var source = image.CloneAs<Bgr24>();

            var output = new byte[(3 * source.Width * source.Height) + source.Height];
            var dst_p = 0;

            for (int y = source.Height - 1; y >= 0; y--)
            {
                // Just use a simple encoding method to make it work.
                output[dst_p++] = 0;
                dst_p = EncodeBpp24Row0(output, dst_p, source, y);
            }

            return output;
        }

        static byte[] EncodeImageBpp32(Image image)
        {
            var source = image.CloneAs<Bgra32>();

            // Flip alpha channel
            source.ProcessPixelRows(accessor =>
            {
                for (var y = 0; y < source.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);

                    for (var x = 0; x < source.Width; x++)
                    {
                        row[x].A = (byte)(0xFF - row[x].A);
                    }
                }
            });

            var output = new byte[(4 * source.Width * source.Height) + source.Height];
            var dst_p = 0;

            for (int y = 0; y < source.Height; y++)
            {
                // Just use a simple encoding method to make it work.
                output[dst_p++] = 0;
                dst_p = EncodeBpp32Row0(output, dst_p, source, y);
            }

            return output;
        }
    }
}
