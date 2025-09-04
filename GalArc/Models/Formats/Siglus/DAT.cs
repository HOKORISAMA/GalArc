using GalArc.Infrastructure.Progress;
using GalArc.Models.Formats.Commons;
using GalArc.Models.Utils;
using System;
using System.IO;

namespace GalArc.Models.Formats.Siglus;

internal class DAT : PCK
{
    public override string Name => "DAT";
    public override string Description => "Siglus Engine Gameexe.dat Archive";

    private const string UnpackedFileName = "Gameexe.ini";

    private SiglusPCKUnpackOptions _unpackOptions;
    public override ArcOptions UnpackOptions => _unpackOptions ??= new SiglusPCKUnpackOptions();

    public override void Unpack(string filePath, string folderPath)
    {
        using FileStream fs = File.OpenRead(filePath);
        using BinaryReader br = new(fs);
        ScenePckEntry entry = new();
        uint reserve = br.ReadUInt32();
        if (reserve != 0)
        {
            throw new InvalidArchiveException();
        }
        bool flag = br.ReadUInt32() == 1;
        entry.Data = br.ReadBytes((int)br.BaseStream.Length - 8);
        byte[] key = flag ? (_unpackOptions.TryEachKey ? TryAllSchemes(entry, 1, _unpackOptions) : _unpackOptions.Key) : null;

        ProgressManager.SetMax(1);
        SiglusUtils.DecryptWithKey(entry.Data, key);
        SiglusUtils.Decrypt(entry.Data, 1);

        entry.PackedLength = BitConverter.ToUInt32(entry.Data, 0);
        if (entry.PackedLength != entry.Data.Length)
        {
            throw new InvalidSchemeException();
        }
        entry.UnpackedLength = BitConverter.ToUInt32(entry.Data, 4);
        byte[] input = new byte[entry.PackedLength - 8];
        Buffer.BlockCopy(entry.Data, 8, input, 0, input.Length);
        try
        {
            entry.Data = SiglusUtils.Decompress(input, entry.UnpackedLength);
        }
        catch
        {
            throw new InvalidSchemeException();
        }

        using (FileStream fw = File.Create(Path.Combine(folderPath, UnpackedFileName)))
        {
            fw.Write([0xff, 0xfe], 0, 2);
            fw.Write(entry.Data, 0, entry.Data.Length);
        }
        ProgressManager.Progress();
    }
}
