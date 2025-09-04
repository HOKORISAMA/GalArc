using CommunityToolkit.Mvvm.ComponentModel;
using GalArc.Infrastructure.Logging;
using GalArc.Infrastructure.Progress;
using GalArc.Models.Formats.Commons;
using GalArc.Models.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GalArc.Models.Formats.Kirikiri;

internal class XP3 : ArcFormat, IPackConfigurable
{
    public override string Name => "XP3";
    public override string Description => "Kirikiri Archive";
    public override bool CanWrite => true;

    private KirikiriXP3PackOptions _packOptions;
    public ArcOptions PackOptions => _packOptions ??= new KirikiriXP3PackOptions();

    private readonly byte[] Magic = Utility.HexStringToByteArray("5850330d0a200a1a8b6701");

    private class Xp3Entry
    {
        internal ulong UnpackedSize { get; set; }
        internal ulong PackedSize { get; set; }
        internal string RelativePath { get; set; }
        internal long DataOffset { get; set; }
        internal bool IsCompressed { get; set; }
        internal string FullPath { get; set; }
    }

    public override void Unpack(string filePath, string folderPath)
    {
        using FileStream fs = File.OpenRead(filePath);
        using BinaryReader br = new(fs);
        if (!br.ReadBytes(11).SequenceEqual(Magic))
        {
            throw new InvalidArchiveException();
        }

        if (br.ReadByte() == 0x17)
        {
            Logger.ShowVersion("xp3", 2);
            br.BaseStream.Position += 20;
        }
        else
        {
            Logger.ShowVersion("xp3", 1);
            br.BaseStream.Position--;
        }

        fs.Position = br.ReadUInt32();
        byte[] Index;
        switch (br.ReadByte())
        {
            case 0:                    //index uncompressed
                long indexSize = br.ReadInt64();
                Index = br.ReadBytes((int)indexSize);
                if (fs.Position != new FileInfo(filePath).Length)
                {
                    throw new InvalidArchiveException("Additional bytes beyond index.");
                }
                break;

            case 1:                    //index compressed
                long packedIndexSize = br.ReadInt64();
                long unpackedIndexSize = br.ReadInt64();
                byte[] packedIndex = br.ReadBytes((int)packedIndexSize);
                if (fs.Position != new FileInfo(filePath).Length)
                {
                    throw new InvalidArchiveException("Additional bytes beyond index.");
                }
                Index = ZlibHelper.Decompress(packedIndex);
                if (Index.Length != unpackedIndexSize)
                {
                    Logger.Info("Index size fails to match.Try reading……");
                }
                break;

            default:
                throw new InvalidArchiveException();
        }
        List<Xp3Entry> entries = [];
        using (MemoryStream ms = new(Index))
        {
            using BinaryReader brIndex = new(ms);
            while (ms.Position < ms.Length)
            {
                string secSig = Encoding.ASCII.GetString(brIndex.ReadBytes(4));
                if (secSig != "File")
                {
                    throw new InvalidArchiveException();
                }
                Xp3Entry entry = new();
                long thisRemaining = brIndex.ReadInt64();
                long thisPos = brIndex.BaseStream.Position;
                long nextPos = thisPos + thisRemaining;

                while (thisRemaining > 0)
                {
                    string secMagic = Encoding.ASCII.GetString(brIndex.ReadBytes(4));
                    long secLen = brIndex.ReadInt64();
                    long secEnd = brIndex.BaseStream.Position + secLen;
                    thisRemaining -= 12 + secLen;
                    switch (secMagic)
                    {
                        case "info":
                            if (brIndex.ReadUInt32() != 0)
                            {
                                Logger.Info("Encrypted file detected. Extracting as is.");
                            }
                            entry.UnpackedSize = brIndex.ReadUInt64();
                            entry.PackedSize = brIndex.ReadUInt64();
                            ushort fileNameLen = brIndex.ReadUInt16();
                            entry.RelativePath = Encoding.Unicode.GetString(brIndex.ReadBytes(fileNameLen * 2));
                            entry.FullPath = Path.Combine(folderPath, entry.RelativePath);
                            brIndex.BaseStream.Position = secEnd;
                            break;

                        case "segm":
                            entry.IsCompressed = brIndex.ReadInt32() != 0;
                            entry.DataOffset = brIndex.ReadInt64();
                            brIndex.BaseStream.Position = secEnd;
                            break;

                        case "adlr":
                            brIndex.BaseStream.Position = secEnd;
                            break;
                    }
                }
                entries.Add(entry);
                ms.Position = nextPos;
            }
        }

        ProgressManager.SetMax(entries.Count);
        foreach (Xp3Entry entry in entries)
        {
            fs.Position = entry.DataOffset;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(entry.FullPath));
            }
            catch (PathTooLongException)
            {
                continue;
            }

            byte[] data = br.ReadBytes((int)entry.PackedSize);
            if (entry.UnpackedSize != entry.PackedSize)
            {
                data = ZlibHelper.Decompress(data);
            }
            File.WriteAllBytes(entry.FullPath, data);
            data = null;
            ProgressManager.Progress();
        }
    }

    public override void Pack(string folderPath, string filePath)
    {
        using FileStream xp3Stream = File.Create(filePath);
        using BinaryWriter bw = new(xp3Stream);
        bw.Write(Magic);
        if (_packOptions.Version == 2)
        {
            bw.Write((long)0x17);
            bw.Write(1);
            bw.Write((byte)0x80);
            bw.Write((long)0);
        }
        bw.Write((long)0);//index offset to 0x00

        DirectoryInfo d = new(folderPath);
        FileInfo[] files = d.GetFiles("*", SearchOption.AllDirectories);
        ProgressManager.SetMax(files.Length);

        using MemoryStream ms = new();
        using BinaryWriter bwEntry = new(ms);

        foreach (FileInfo file in files)
        {
            long offset = xp3Stream.Position;
            long originalSize = file.Length;
            long compressedSize = originalSize;
            byte[] fileData = File.ReadAllBytes(file.FullName);
            uint adler32 = Adler32.Compute(fileData);
            if (_packOptions.CompressContents)
            {
                fileData = ZlibHelper.Compress(fileData);
                bw.Write(fileData);
                compressedSize = fileData.Length;
            }
            else
            {
                bw.Write(fileData);
            }
            fileData = null;
            //File
            bwEntry.Write(Encoding.ASCII.GetBytes("File"));
            string thisFilePath = file.FullName[(folderPath.Length + 1)..].Replace("\\", "/");
            bwEntry.Write((long)(90 + (2 * thisFilePath.Length)));
            //info
            bwEntry.Write(Encoding.ASCII.GetBytes("info"));
            bwEntry.Write((long)(22 + (2 * thisFilePath.Length)));
            bwEntry.Write(0);
            bwEntry.Write(originalSize);
            bwEntry.Write(compressedSize);
            bwEntry.Write((ushort)thisFilePath.Length);
            bwEntry.Write(Encoding.Unicode.GetBytes(thisFilePath));
            //segment
            bwEntry.Write(Encoding.ASCII.GetBytes("segm"));
            bwEntry.Write((long)0x1c);
            bwEntry.Write(compressedSize == originalSize ? 0 : 1);
            bwEntry.Write(offset);
            bwEntry.Write(originalSize);
            bwEntry.Write(compressedSize);
            //adler
            bwEntry.Write(Encoding.ASCII.GetBytes("adlr"));
            bwEntry.Write((long)4);
            bwEntry.Write(adler32);
            //bwEntry.Write(0);
            ProgressManager.Progress();
        }

        long indexOffset;
        long uncomLen = ms.Length;
        if (_packOptions.CompressIndex)
        {
            bw.Write((byte)1);
            byte[] compressedIndex = ZlibHelper.Compress(ms.ToArray());
            long comLen = compressedIndex.Length;
            bw.Write(comLen);           //8
            bw.Write(uncomLen);         //8
            bw.Write(compressedIndex);
            indexOffset = xp3Stream.Length - 8 - 1 - 8 - comLen;
        }
        else
        {
            bw.Write((byte)0);
            bw.Write(uncomLen);
            bw.Write(ms.ToArray());
            indexOffset = xp3Stream.Length - 8 - 1 - uncomLen;
        }
        xp3Stream.Position = _packOptions.Version == 1 ? Magic.Length : 32;
        bw.Write(indexOffset);
    }
}

internal partial class KirikiriXP3PackOptions : ArcOptions
{
    [ObservableProperty]
    private bool compressIndex = true;
    [ObservableProperty]
    private bool compressContents = true;
    [ObservableProperty]
    private int version = 2;
    [ObservableProperty]
    private IReadOnlyList<int> versions = [1, 2];
}
