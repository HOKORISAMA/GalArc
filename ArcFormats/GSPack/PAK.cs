using GalArc.Logs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utility.Compression;
using Utility.Extensions;

namespace ArcFormats.GSPack
{
    internal class PAK
    {
        private class Entry
        {
            public string Name { get; set; }
            public uint Size { get; set; }
            public long Offset { get; set; }
        }

        private Dictionary<string, string> NameExtensionPairs => new Dictionary<string, string>
        {
            { "bgm" , ".ogg" },
            { "voice" , ".ogg" },
            { "graphic",".png" },
            { "se" , ".ogg" },
            { "scr" , ".scw" }
        };

        private string[] ValidMagics => new string[] { "DataPack5", "GsPack5", "GsPack4" };

        public void Unpack(string filePath, string folderPath)
        {
            FileStream fs = File.OpenRead(filePath);
            BinaryReader br = new BinaryReader(fs);
            byte[] magicBytes = br.ReadBytes(9);
            string magic = Encoding.ASCII.GetString(magicBytes).TrimEnd('\0');

            bool isValidMagic = false;
            foreach (string validMagic in ValidMagics)
            {
                if (magic.StartsWith(validMagic, StringComparison.OrdinalIgnoreCase))
                {
                    isValidMagic = true;
                    break;
                }
            }
            if (!isValidMagic)
            {
                throw new InvalidDataException($"Not a Valid GSWIN Archive. Found magic: {magic}");
            }

            fs.Position = 0x30;
            int versionMinor = br.ReadUInt16();
            int versionMajor = br.ReadUInt16();
            uint indexSize = br.ReadUInt32();
            uint isEncrypted = br.ReadUInt32();
            int fileCount = br.ReadInt32();
            long dataOffset = br.ReadUInt32();
            int indexOffset = br.ReadInt32();
            int entrySize = versionMajor < 5 ? 0x48 : 0x68;
            int unpackedSize = fileCount * entrySize;
            Logger.InitBar(fileCount);

            byte[] index;
            if (indexSize != 0)
            {
                fs.Position = indexOffset;
                byte[] packedIndex = br.ReadBytes((int)indexSize);

                if ((isEncrypted & 1) != 0)
                {
                    for (int i = 0; i < packedIndex.Length; i++)
                    {
                        packedIndex[i] ^= (byte)i;
                    }
                }

                index = Lzss.Decompress(packedIndex);
            }
            else
            {
                fs.Position = indexOffset;
                index = br.ReadBytes(unpackedSize);
            }

            int currentOffset = 0;
            var entries = new List<Entry>();
            for (int i = 0; i < fileCount; i++)
            {
                string name = index.GetCString(currentOffset, 0x40);
                if (!string.IsNullOrEmpty(name))
                {
                    var entry = new Entry
                    {
                        Name = name,
                        Offset = dataOffset + BitConverter.ToUInt32(index, currentOffset + 0x40),
                        Size = BitConverter.ToUInt32(index, currentOffset + 0x44)
                    };
                    entries.Add(entry);
                }
                currentOffset += entrySize;
            }

            Directory.CreateDirectory(folderPath);
            string ext = string.Empty;
            foreach (var pair in NameExtensionPairs)
            {
                if (Path.GetFileName(filePath).StartsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    ext = pair.Value;
                    break;
                }
            }

            foreach (Entry entry in entries)
            {
                fs.Position = entry.Offset;
                byte[] data = br.ReadBytes((int)entry.Size);

                if ((isEncrypted & 2) != 0)
                {
                    Decrypt(data, entry.Name);
                }
                if (ext == ".scw" && BitConverter.ToUInt32(data, 0) == 0x35776353)  // 'scw5'
                {
                    data = DecryptScript(data);
                }
                File.WriteAllBytes(Path.Combine(folderPath, entry.Name + ext), data);
                data = null;
                Logger.UpdateBar();
            }
            fs.Dispose();
            br.Dispose();
        }

        private void Decrypt(byte[] data, string key)
        {
            int numkey = 0;
            for (int i = 0; i < key.Length; ++i)
            {
                numkey = numkey * 37 + (key[i] | 0x20);
            }

            unsafe
            {
                fixed (byte* data8 = data)
                {
                    int* data32 = (int*)data8;
                    for (int count = data.Length / 4; count > 0; --count)
                    {
                        *data32++ ^= numkey;
                    }
                }
            }
        }

        private byte[] DecryptScript(byte[] data)
        {
            bool isCompressed = BitConverter.ToInt32(data, 20) == -1;
            uint unpackedSize = BitConverter.ToUInt32(data, 24);
            uint packedSize = BitConverter.ToUInt32(data, 28);

            if (isCompressed)
            {
                byte[] bytes = new byte[packedSize];
                Array.Copy(data, 0x1C8, bytes, 0, packedSize);
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= (byte)(i & 0xFF);
                }
                byte[] decompressed = Lzss.Decompress(bytes);
                byte[] result = new byte[decompressed.Length + 0x1C8];
                Array.Copy(data, 0, result, 0, 0x1C8);
                Array.Copy(decompressed, 0, result, 0x1C8, decompressed.Length);
                bytes = null;
                decompressed = null;
                return result;
            }
            else
            {
                for (int i = 0; i < unpackedSize; i++)
                {
                    data[0x1C8 + i] ^= (byte)(i & 0xFF);
                }
                return data;
            }
        }
    }
}
