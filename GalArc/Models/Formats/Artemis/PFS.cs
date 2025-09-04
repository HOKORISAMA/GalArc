using CommunityToolkit.Mvvm.ComponentModel;
using GalArc.I18n;
using GalArc.Infrastructure.Logging;
using GalArc.Infrastructure.Progress;
using GalArc.Models.Formats.Commons;
using GalArc.Models.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GalArc.Models.Formats.Artemis;

internal class PFS : ArcFormat, IUnpackConfigurable, IPackConfigurable
{
    public override string Name => "PFS";
    public override string Description => "Artemis Archive";
    public override bool CanWrite => true;

    private ArtemisPFSUnpackOptions _unpackOptions;
    public ArcOptions UnpackOptions => _unpackOptions ??= new ArtemisPFSUnpackOptions();

    private ArtemisPFSPackOptions _packOptions;
    public ArcOptions PackOptions => _packOptions ??= new ArtemisPFSPackOptions();

    private class Header
    {
        public string Magic { get; } = "pf";
        public int Version { get; set; }
        public uint IndexSize { get; set; }
        public int FileCount { get; set; }
        public int PathLenSum { get; set; }
    }

    private class PfsEntry : Entry
    {
        public int RelativePathLen { get; set; }
        public string RelativePath { get; set; }
    }

    public override void Unpack(string filePath, string folderPath)
    {
        Header header = new();

        using FileStream fs = File.OpenRead(filePath);
        using BinaryReader br = new(fs);

        if (Encoding.ASCII.GetString(br.ReadBytes(2)) != header.Magic)
        {
            throw new InvalidArchiveException();
        }
        header.Version = br.ReadChar() - '0';
        if (header.Version != 8 && header.Version != 2 && header.Version != 6)
        {
            throw new InvalidVersionException(InvalidVersionType.Unknown);
        }
        Logger.ShowVersion("pfs", header.Version);
        // read header
        header.IndexSize = br.ReadUInt32();
        if (header.Version == 2)
        {
            br.ReadUInt32();
        }
        header.FileCount = br.ReadInt32();
        ProgressManager.SetMax(header.FileCount);
        // compute key
        byte[] key = new byte[20];  // SHA1 hash of index
        if (header.Version == 8)
        {
            fs.Position = 7;
            key = SHA1.HashData(br.ReadBytes((int)header.IndexSize));
            fs.Position = 11;
        }
        Encoding encoding = _unpackOptions.Encoding;
        // read index and save files
        for (int i = 0; i < header.FileCount; i++)
        {
            PfsEntry entry = new();
            entry.RelativePathLen = br.ReadInt32();
            string name = encoding.GetString(br.ReadBytes(entry.RelativePathLen));
            if (name.ContainsInvalidChars())
            {
                throw new Exception(MsgStrings.ErrorContainsInvalid);
            }
            entry.Path = Path.Combine(folderPath, name);

            br.ReadUInt32();
            if (header.Version == 2)
            {
                br.ReadBytes(8);
            }
            entry.Offset = br.ReadUInt32();
            entry.Size = br.ReadUInt32();

            Directory.CreateDirectory(Path.GetDirectoryName(entry.Path));

            long pos = fs.Position;
            fs.Position = entry.Offset;
            byte[] buffer = br.ReadBytes((int)entry.Size);
            if (header.Version == 8)
            {
                for (int j = 0; j < buffer.Length; j++)
                {
                    buffer[j] ^= key[j % 20];
                }
            }
            File.WriteAllBytes(entry.Path, buffer);
            buffer = null;
            fs.Position = pos;
            ProgressManager.Progress();
        }
    }

    public override void Pack(string folderPath, string filePath)
    {
        //init
        Header header = new()
        {
            Version = _packOptions.Version,
            PathLenSum = 0
        };
        List<PfsEntry> entries = [];
        string[] files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
        string[] relativePaths = Utility.GetRelativePaths(files, folderPath);
        Encoding encoding = _packOptions.Encoding;
        header.PathLenSum = Utility.GetLengthSum(relativePaths, encoding);
        header.FileCount = files.Length;
        ProgressManager.SetMax(header.FileCount);
        Array.Sort(relativePaths, StringComparer.Ordinal);

        //add entry
        for (int i = 0; i < header.FileCount; i++)
        {
            PfsEntry entry = new();
            entry.Path = Path.Combine(folderPath, relativePaths[i]);
            entry.Size = (uint)new FileInfo(entry.Path).Length;
            entry.RelativePath = relativePaths[i];
            entry.RelativePathLen = encoding.GetByteCount(relativePaths[i]);
            entries.Add(entry);
        }

        switch (header.Version)
        {
            case 8:
                header.IndexSize = (uint)(4 + (16 * header.FileCount) + header.PathLenSum + 4 + (8 * header.FileCount) + 12);

                //write header
                MemoryStream ms8 = new();
                BinaryWriter writer8 = new(ms8);
                writer8.Write(Encoding.ASCII.GetBytes(header.Magic));
                writer8.Write((byte)header.Version);
                writer8.Write(header.IndexSize);
                writer8.Write(header.FileCount);

                //write entry
                uint offset8 = header.IndexSize + 7;

                foreach (PfsEntry file in entries)
                {
                    writer8.Write(file.RelativePathLen);
                    writer8.Write(encoding.GetBytes(file.RelativePath));
                    writer8.Write(0); // reserved
                    writer8.Write(offset8);
                    writer8.Write(file.Size);
                    offset8 += file.Size;
                }

                long posOffsetTable = ms8.Position;
                int offsetCount = header.FileCount + 1;
                writer8.Write(offsetCount);
                uint total = 4;

                //write table
                foreach (PfsEntry entry in entries)
                {
                    total = total + 4 + (uint)entry.RelativePathLen;
                    uint posOffset = total;
                    writer8.Write(posOffset);
                    writer8.Write(0);
                    total += 12;
                }
                writer8.Write(0); // EOF of offset table
                writer8.Write(0); // EOF of offset table
                uint tablePos = (uint)(posOffsetTable - 7);
                writer8.Write(tablePos);

                //write data
                byte[] key = new byte[20];
                byte[] buf = ms8.ToArray();
                byte[] xorBuf = new byte[buf.Length - 7];
                Buffer.BlockCopy(buf, 7, xorBuf, 0, buf.Length - 7);
                key = SHA1.HashData(xorBuf);
                FileStream fw8 = File.Create(filePath);
                fw8.Write(buf, 0, buf.Length);
                foreach (PfsEntry entry in entries)
                {
                    byte[] fileData = File.ReadAllBytes(entry.Path);
                    for (int i = 0; i < fileData.Length; i++)
                    {
                        fileData[i] ^= key[i % 20];
                    }
                    fw8.Write(fileData, 0, fileData.Length);
                    fileData = null;
                    ProgressManager.Progress();
                }
                fw8.Dispose();
                ms8.Dispose();
                writer8.Dispose();
                return;

            case 2:
                header.IndexSize = (uint)(8 + (24 * header.FileCount) + header.PathLenSum);

                //write header
                MemoryStream ms2 = new();
                BinaryWriter writer2 = new(ms2);
                writer2.Write(Encoding.ASCII.GetBytes(header.Magic));
                writer2.Write((byte)header.Version);
                writer2.Write(header.IndexSize);
                writer2.Write((uint)0);
                writer2.Write(header.FileCount);
                uint offset2 = header.IndexSize + 7;

                //write entry
                foreach (PfsEntry file in entries)
                {
                    writer2.Write((uint)file.RelativePathLen);
                    writer2.Write(encoding.GetBytes(file.RelativePath));
                    writer2.Write((uint)16);
                    writer2.Write((uint)0);
                    writer2.Write((uint)0);
                    writer2.Write(offset2);
                    writer2.Write(file.Size);
                    offset2 += file.Size;
                }

                //write data
                FileStream fw2 = File.Create(filePath);
                ms2.WriteTo(fw2);

                foreach (PfsEntry file in entries)
                {
                    byte[] fileData = File.ReadAllBytes(file.Path);
                    fw2.Write(fileData, 0, fileData.Length);
                    fileData = null;
                    ProgressManager.Progress();
                }
                fw2.Dispose();
                ms2.Dispose();
                writer2.Dispose();
                return;

            case 6:
                header.IndexSize = (uint)(4 + (16 * header.FileCount) + header.PathLenSum + 4 + (8 * header.FileCount) + 12);
                //indexsize=(filecount)4byte+(pathlen+0x00000000+offset to begin+file size)16byte*filecount+pathlensum+(file count+1)4byte+8*filecount+(0x00000000)4byte*2+(offsettablebegin-0x7)4byte

                //write header
                MemoryStream ms6 = new();
                BinaryWriter writer6 = new(ms6);
                writer6.Write(Encoding.ASCII.GetBytes(header.Magic));
                writer6.Write((byte)header.Version);
                writer6.Write(header.IndexSize);
                writer6.Write(header.FileCount);

                //write entry
                uint offset6 = header.IndexSize + 7;
                foreach (PfsEntry file in entries)
                {
                    uint filenameSize = (uint)file.RelativePathLen;//use utf-8 for japanese character in file name
                    writer6.Write(filenameSize);
                    writer6.Write(encoding.GetBytes(file.RelativePath));
                    writer6.Write(0); // reserved
                    writer6.Write(offset6);
                    writer6.Write(file.Size);
                    offset6 += file.Size;
                }

                long posOffsetTable6 = ms6.Position;
                int offsetCount6 = header.FileCount + 1;
                writer6.Write(offsetCount6);//filecount + 1
                uint total6 = 4;

                //write table
                foreach (PfsEntry file in entries)
                {
                    total6 = total6 + 4 + (uint)file.RelativePathLen;//use utf-8 for japanese character in file name
                    uint posOffset = total6;
                    writer6.Write(posOffset);
                    writer6.Write(0); // reserved
                    total6 += 12;
                }
                writer6.Write(0); // EOF of offset table
                writer6.Write(0); // EOF of offset table
                uint tablePos6 = (uint)(posOffsetTable6 - 7);
                writer6.Write(tablePos6);

                //write data
                FileStream fw6 = File.Create(filePath);
                ms6.WriteTo(fw6);
                foreach (PfsEntry file in entries)
                {
                    byte[] fileData = File.ReadAllBytes(file.Path);
                    fw6.Write(fileData, 0, fileData.Length);
                    fileData = null;
                    ProgressManager.Progress();
                }
                fw6.Dispose();
                ms6.Dispose();
                writer6.Dispose();
                return;
        }
    }
}

internal partial class ArtemisPFSUnpackOptions : ArcOptions
{
    [ObservableProperty]
    private IReadOnlyList<Encoding> encodings = ArcEncoding.SupportedEncodings;
    [ObservableProperty]
    private Encoding encoding = Encoding.UTF8;
}

internal partial class ArtemisPFSPackOptions : ArcOptions
{
    [ObservableProperty]
    private IReadOnlyList<Encoding> encodings = ArcEncoding.SupportedEncodings;
    [ObservableProperty]
    private Encoding encoding = Encoding.UTF8;
    [ObservableProperty]
    private ObservableCollection<int> versions = [8, 6, 2];
    [ObservableProperty]
    private int version = 8;
}
