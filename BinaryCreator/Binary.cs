/*//
Binary Creator - Binary Utility Library
Copyright (C) 2026  Alec James McKeever
//*/
using System;
#if NET48

#else
using System.Buffers.Binary;
#endif
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BinaryCreator.Exceptions;

namespace BinaryCreator
{
    [Obsolete("This class is no longer being developed on.\nPlease use the BinaryDataSet class instead.", false)]
    public class Binary
    {
        public static readonly int headerAndVersionSize = 20;
        public static readonly Endian littleEndian = new Endian(false, true);
        public static readonly Endian bigEndian = new Endian(true, false);
        public static readonly BinVersion libraryVersion = new BinVersion(1, 0, 0, 0);

        [StructLayout(LayoutKind.Sequential)]
        public struct BinDataSet
        {
            public BinHeader header;
            public BinVersion versionCustom;
            public BinSectionDefinition[] sectionDefinitions;
            public byte[][] sectionData;
            public void SetDataInSection(byte[] id, byte[] value) { sectionData[GetSectionIndexByID(id)] = value; }
            public ref byte[] GetDataInSection(byte[] id) { Console.WriteLine(GetSectionIndexByID(id)); return ref sectionData[GetSectionIndexByID(id)]; }
            public void SetRVAinSection(byte[] id, ulong value) { GetSectionByID(id).SetRVA(value); }
            public void SetSizeInSection(byte[] id, ulong value) { GetSectionByID(id).SetSize(value); }
            public ref BinSectionDefinition GetSectionByID(byte[] id)
            {
                for (int i = 0; i < sectionDefinitions.Length; i++)
                {
                    if (sectionDefinitions[i].id.SequenceEqual(id))
                    {
                        return ref sectionDefinitions[i];
                    }
                }
                return ref sectionDefinitions[0];
            }
            public int GetSectionIndexByID(byte[] id) { return Array.FindIndex(sectionDefinitions, s => s.id.SequenceEqual(id)); }
            public BinVersion GetLibVersion() { return libraryVersion; }
            public BinVersion GetCustomVersion() { return versionCustom; }
            public void AddSection(byte[] id, byte[] data, bool isReadOnly)
            {
                if (sectionDefinitions == null) { sectionDefinitions = new BinSectionDefinition[0]; }
                if (sectionData == null) { sectionData = new byte[0][]; }

                int origSize = sectionDefinitions.Length;
                Array.Resize(ref sectionDefinitions, origSize + 1);
                sectionDefinitions[origSize] = new BinSectionDefinition(id, 0, 0, isReadOnly);
                Array.Resize(ref sectionData, sectionData.Length + 1);
                GetDataInSection(id) = data;
            }
            public long GetTotalSectionDataLength()
            {
                int sectionDataLength = 0;
                foreach (var section in sectionDefinitions)
                {
                    sectionDataLength += GetDataInSection(section.id).Length;
                }
                return sectionDataLength;
            }
            public long GetTotalFileSize()
            {
                return headerAndVersionSize + (sectionDefinitions.Length * 25) + GetTotalSectionDataLength();
            }
            public void WriteToDisk(string path, Endian endianess)
            {
                using (FileStream stream = File.Create(path))
                {
                    byte[] finalized = new byte[GetTotalFileSize()];
                    stream.Write(header.GetHeader());
                    stream.Write(libraryVersion.GetByteArray());
                    stream.Write(versionCustom.GetByteArray());
                    stream.Write(ConvertValueToBytes((uint)sectionDefinitions.Length, endianess));
                    foreach (BinSectionDefinition section in sectionDefinitions)
                    {
                        stream.Write(section.id);
                        stream.Write(ConvertValueToBytes(section.rva, endianess));
                        stream.Write(ConvertValueToBytes(section.size, endianess));
                        stream.Write(ConvertBoolArrayToByteArray(new bool[] { section.isReadOnly }));
                        stream.Write(new byte[7]);
                    }
                    foreach (byte[] data in sectionData)
                    {
                        stream.Write(data);
                    }
                }
            }
            public void WriteToByteArray(out byte[] _data, Endian endianess)
            {
                List<byte> finalized = new List<byte>();
                finalized.AddRange(header.GetHeader());
                finalized.AddRange(libraryVersion.GetByteArray());
                finalized.AddRange(versionCustom.GetByteArray());
                finalized.AddRange(ConvertValueToBytes((uint)sectionDefinitions.Length, endianess));
                foreach (BinSectionDefinition section in sectionDefinitions)
                {
                    finalized.AddRange(section.id);
                    finalized.AddRange(ConvertValueToBytes(section.rva, endianess));
                    finalized.AddRange(ConvertValueToBytes(section.size, endianess));
                    finalized.AddRange(ConvertBoolArrayToByteArray(new bool[] { section.isReadOnly }));
                    finalized.AddRange(new byte[7]);
                }
                foreach (byte[] data in sectionData)
                {
                    finalized.AddRange(data);
                }
                _data = finalized.ToArray();
            }
            public void RefreshRVAs()
            {
                ulong currentSectionDataBehind = 0;
                for (int i = 0; i < sectionDefinitions.Length; i++)
                {
                    sectionDefinitions[i].rva = currentSectionDataBehind + (ulong)(headerAndVersionSize + (sectionDefinitions.Length * 32));
                    currentSectionDataBehind += (ulong)sectionData[i].Length;
                }
            }
            public void RefreshSizes()
            {
                for (int i = 0; i < sectionDefinitions.Length; i++)
                {
                    sectionDefinitions[i].SetSize((ulong)sectionData[i].Length);
                }
            }
            public void ReadFromByteArray(byte[] data, BinVersion softwareVersion, BinHeader header, Endian endian)
            {
                int DC = 0;
                Span<byte> headerComputed = GetChunkDChandle(ref DC, ref data, 8);
                Span<byte> headerAsSpan = header.GetHeader().AsSpan(0, 8);

                if (!headerAsSpan.SequenceEqual(headerComputed))
                {
                    throw new Exceptions.HeaderMismatchException();
                }

                Span<byte> versionLibCompute = GetChunkDChandle(ref DC, ref data, 4);
                Span<byte> versionCustomCompute = GetChunkDChandle(ref DC, ref data, 4);
                Span<byte> thisVersionLibSpan = libraryVersion.GetByteArray().AsSpan();

                if (!versionLibCompute.SequenceEqual(thisVersionLibSpan))
                {
                    throw new Exceptions.LibraryVersionMismatch();
                }

                Span<byte> customVersionAsSpan = softwareVersion.GetByteArray().AsSpan();

                if (!versionCustomCompute.SequenceEqual(customVersionAsSpan))
                {
                    throw new Exceptions.CustomVersionMismatch();
                }

                Span<byte> sectionAmountSpan = GetChunkDChandle(ref DC, ref data, 4);
                uint sectionAmount = Binary.ReadUIntFromSpan(sectionAmountSpan.ToArray(), endian);

                sectionDefinitions = new BinSectionDefinition[sectionAmount];
                sectionData = new byte[ReadUIntFromByteArray(sectionAmountSpan.ToArray(), endian)][];

                for (int i = 0; i < sectionAmount; i++)
                {
                    sectionDefinitions[i].SetID(GetChunkDChandle(ref DC, ref data, 8).ToArray());
                    sectionDefinitions[i].SetRVA(ReadUlongFromSpan(GetChunkDChandle(ref DC, ref data, 8).ToArray(), endian));
                    sectionDefinitions[i].SetSize(ReadUlongFromSpan(GetChunkDChandle(ref DC, ref data, 8).ToArray(), endian));
                    sectionDefinitions[i].SetIsReadOnly(ConvertByteArrayToBoolArray(GetChunkDChandle(ref DC, ref data, 8).ToArray())[0]);
                    sectionData[i] = new byte[sectionDefinitions[i].size];
                    Array.Copy(data, (int)sectionDefinitions[i].rva, sectionData[i], 0, (int)sectionDefinitions[i].size);
                }
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BinHeader
        {
            public byte[] value { get; private set; }
            public BinHeader(byte[] value) { this.value = value; }
            public void SetHeader(byte[] value) { Array.Resize(ref value, 8); this.value = value; }
            public byte[] GetHeader() { byte[] v = this.value; Array.Resize(ref v, 8); return v; }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BinVersion
        {
            public byte versionMaj { get; private set; }
            public byte versionMajAdd { get; private set; }
            public byte versionMinAdd { get; private set; }
            public byte versionPatch { get; private set; }
            public BinVersion(byte versionMaj, byte versionMajAdd, byte versionMinAdd, byte versionPatch)
            {
                this.versionMaj = versionMaj;
                this.versionMajAdd = versionMajAdd;
                this.versionMinAdd = versionMinAdd;
                this.versionPatch = versionPatch;
            }
            public void SetMajorVersion(byte v) { versionMaj = v; }
            public void SetMajorAdditionVersion(byte v) { versionMajAdd = v; }
            public void SetMinorAdditionVersion(byte v) { versionMinAdd = v; }
            public void SetPatchVersion(byte v) { versionPatch = v; }
            public byte[] GetByteArray() { return new byte[] { versionMaj, versionMajAdd, versionMinAdd, versionPatch }; }
            public void ReadFromBytes(Span<byte> byteSpan)
            {
                if (byteSpan.IsEmpty)
                {
                    throw new ArgumentNullException();
                }
                versionMaj = byteSpan[0];
                versionMajAdd = byteSpan[1];
                versionMinAdd = byteSpan[2];
                versionPatch = byteSpan[3];
            }
            public void ReadFromBytes(byte[] byteArray)
            {
                if (byteArray == null)
                {
                    throw new ArgumentNullException();
                }
                versionMaj = byteArray[0];
                versionMajAdd = byteArray[1];
                versionMinAdd = byteArray[2];
                versionPatch = byteArray[3];
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BinSectionDefinition
        {
            public byte[] id { get; private set; }
            public ulong rva;
            public ulong size { get; private set; }
            public bool isReadOnly { get; private set; }

            public BinSectionDefinition(byte[] id, ulong rva, ulong size, bool isReadOnly)
            {
                this.id = id;
                this.rva = rva;
                this.size = size;
                this.isReadOnly = isReadOnly;
            }

            public void SetID(byte[] id) { Array.Resize(ref id, 8); this.id = id; }
            public byte[] GetID() { byte[] v = this.id; Array.Resize(ref v, 8); return v; }
            public void SetRVA(ulong _rva) { rva = _rva; }
            public void SetSize(ulong size) { this.size = size; }
            public void SetIsReadOnly(bool readOnly) { this.isReadOnly = readOnly; }
            public byte GetIsReadOnlyByte() { return CreateByte(new bool[] { isReadOnly }); }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct Endian
        {
            public bool isBigEndian { get; private set; }
            public bool isLittleEndian { get; private set; }
            public Endian(bool isBigEndian, bool isLittleEndian)
            {
                this.isBigEndian = isBigEndian; this.isLittleEndian = isLittleEndian;
            }
            public bool IsBigEndian() { return isBigEndian; }
            public bool IsLittleEndian() { return isLittleEndian; }
            public void SetBigEndian() { isBigEndian = true; isLittleEndian = false; }
            public void SetLittleEndian() { isLittleEndian = true; isBigEndian = false; }
        }
        /// <summary>
        /// Get a chunck of data from a byte array while incrementing a data counter.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="data"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static Span<byte> GetChunkDChandle(ref int dc, ref byte[] data, int amount)
        {
            Span<byte> finalized;
            finalized = data.AsSpan(dc, amount);
            dc += amount;
            return finalized;
        }

        public static BinVersion ExtractCustomVersionFromBytes(byte[] data, Endian endianness)
        {
            int dc = 0;
            GetChunkDChandle(ref dc, ref data, 12);
            BinVersion ver = new BinVersion();
            ver.ReadFromBytes(GetChunkDChandle(ref dc, ref data, 4));
            return ver;
        }

        #region Converters
        public static byte[] ConvertValueToBytes(object value, Endian endianness)
        {
            Span<byte> span = new Span<byte>();
            switch (value)
            {
                case ushort s:
                    span = new byte[2];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)value);
                    } else
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)value);
                    }
                    break;

                case short s:
                    span = new byte[2];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteInt16BigEndian(span, (short)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(span, (short)value);
                    }
                    break;

                case uint s:
                    span = new byte[4];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(span, (uint)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)value);
                    }
                    break;

                case int s:
                    span = new byte[4];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteInt32BigEndian(span, (int)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(span, (int)value);
                    }
                    break;

                case ulong s:
                    span = new byte[8];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt64LittleEndian(span, (ulong)value);
                    }
                    break;

                case long s:
                    span = new byte[8];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteInt64BigEndian(span, (long)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt64LittleEndian(span, (long)value);
                    }
                    break;
#if NET8_0_OR_GREATER
                case UInt128 s:
                    span = new byte[16];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteUInt128BigEndian(span, (UInt128)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt128LittleEndian(span, (UInt128)value);
                    }
                    break;

                case Int128 s:
                    span = new byte[16];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteInt128BigEndian(span, (Int128)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt128LittleEndian(span, (Int128)value);
                    }
                    break;
#endif
                case float s:
#if NET8_0_OR_GREATER
                    span = new byte[4];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteSingleBigEndian(span, (float)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteSingleLittleEndian(span, (float)value);
                    }
                    break;
#else
                    span = BitConverter.GetBytes((float)value);
                    if (endianness.isBigEndian)
                    {
                        if (BitConverter.IsLittleEndian)
                            span.Reverse();
                    } else
                    {
                        if (!BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    break;
#endif
                case double s:
#if NET8_0_OR_GREATER
                    span = new byte[8];
                    if (endianness.isBigEndian)
                    {
                        BinaryPrimitives.WriteDoubleBigEndian(span, (double)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteDoubleLittleEndian(span, (double)value);
                    }
                    break;
#else
                    span = BitConverter.GetBytes((double)value);
                    if (endianness.isBigEndian)
                    {
                        if (BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    else
                    {
                        if (!BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    break;
#endif
            }
            return span.ToArray();
        }



        public static byte[] SmartConvertByteArrayToBytes(byte[] data, Endian endianness)
        {
            byte[] startIndex = ConvertValueToBytes(data.Length, endianness);
            return startIndex.Concat(data).ToArray();
        }
        public static byte[] SmartConvertByteArrayToBytes(Span<byte> data, Endian endianness)
        {
            byte[] startIndex = ConvertValueToBytes(data.Length, endianness);
            return startIndex.Concat(data.ToArray()).ToArray();
        }

        public static byte[] SmartConvertBytesToByteArray(byte[] data, Endian endianness, int index = 0)
        {
            int arrayLength = ReadIntFromByteArray(data, endianness, index);
            Span<byte> finalized = new Span<byte>(data, index + 4, arrayLength);
            return finalized.ToArray();
        }
        public static byte[] SmartConvertBytesToByteArray(byte[] data, Endian endianness, ref int incrementer, int index = 0)
        {
            int arrayLength = ReadIntFromByteArray(data, endianness, index);
            incrementer += 4 + arrayLength;
            Span<byte> finalized = new Span<byte>(data, index + 4, arrayLength);
            return finalized.ToArray();
        }


        public static byte CreateByte(bool[] bits)
        {
            if (bits.Length > 8)
            {
                throw new ArgumentOutOfRangeException();
            }
            return (byte)bits.Reverse().Select((val, i) => Convert.ToByte(val) << i).Sum();
        }
        public static byte[] IntToBytes(int integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] UIntToBytes(uint integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] LongToBytes(long integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ULongToBytes(ulong integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ShortToBytes(short integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] UShortToBytes(short integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] SingleToBytes(float integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] DoubleToBytes(double integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ConvertBoolArrayToByteArray(bool[] source)
        {
            byte[] finalized = new byte[(int)Math.Ceiling(source.Length / 8f)];
            bool[] currentBools = new bool[8];
            for (int i = 0; i < finalized.Length; i++)
            {
                for (int k = 0; k < 8; k++)
                {
                    currentBools[k] = k + (i * 8) + 1 <= source.Length ? source[k + (i * 8)] : false;
                }
                byte result = 0;
                int index = 0;

                foreach (bool b in currentBools)
                {
                    if (b)
                        result |= (byte)(1 << (7 - index));

                    index++;
                }
                finalized[i] = result;
            }
            return finalized;
        }
        public static bool[] ConvertByteArrayToBoolArray(byte[] b)
        {
            bool[] finalized = new bool[b.Length * 8];
            int index = 0;
            foreach (byte b2 in b)
            {
                bool[] result = new bool[8];

                // check each bit in the byte. if 1 set to true, if 0 set to false
                for (int i = 0; i < 8; i++)
                    result[i] = (b2 & (1 << i)) != 0;

                // reverse the array
                Array.Reverse(result);

                result.CopyTo(finalized, index * 8);
                index++;
            }
            return finalized;
        }
        #endregion
        #region Random
        public static float RandomFloatInRange(float min, float max, Random random)
        {
#if NETSTANDARD2_1
            var x = (float)random.NextDouble();
#else
            var x = random.NextSingle();
#endif

            return x * max + (1 - x) * min;
        }
        public static double RandomDoubleInRange(float min, float max, Random random)
        {
            var x = random.NextDouble();

            return x * max + (1 - x) * min;
        }
        #endregion
        #region Readers


        public static ushort ReadUshortFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt16(copied);
        }
        public static ushort ReadUshortFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt16(copied);
        }



        public static short ReadShortFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt16(copied);
        }
        public static short ReadShortFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            incrementer += 2;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt16(copied);
        }


        public static uint ReadUIntFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt32(copied);
        }
        public static uint ReadUIntFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt32(copied);
        }


        public static int ReadIntFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt32(copied);
        }
        public static int ReadIntFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt32(copied);
        }


        public static ulong ReadUlongFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt64(copied);
        }
        public static ulong ReadUlongFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToUInt64(copied);
        }


        public static long ReadLongFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt64(copied);
        }
        public static long ReadLongFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToInt64(copied);
        }


        public static float ReadSingleFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToSingle(copied);
        }
        public static float ReadSingleFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToSingle(copied);
        }


        public static double ReadDoubleFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToDouble(copied);
        }
        public static double ReadDoubleFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess.IsBigEndian()) { copied.Reverse(); }
            return BitConverter.ToDouble(copied);
        }



        #endregion
        #region SpanReaders
        public static ushort ReadUshortFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToUInt16(bytes);
        }
        public static short ReadShortFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToInt16(bytes);
        }
        public static uint ReadUIntFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToUInt32(bytes);
        }
        public static int ReadIntFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToInt32(bytes);
        }
        public static ulong ReadUlongFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToUInt64(bytes);
        }
        public static long ReadLongFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToInt64(bytes);
        }
        public static float ReadSingleFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToSingle(bytes);
        }
        public static double ReadDoubleFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess.IsBigEndian()) { bytes.Reverse(); }
            return BitConverter.ToDouble(bytes);
        }

        #endregion

        #region Debug
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MyStructure
        {
            public int ID;
            public float Value;
            public fixed byte Data[16];
        }
        #endregion
    }
    public enum StreamMode
    {
        File, Memory
    }
    public enum Endian
    {
        Big, Little
    }
    public class BinaryDataSet
    {
        public StreamMode streamMode { get; private set; }
        public List<Section> sections = new List<Section>();
        public string tempPath { get; private set; }
        public Version customVersion;
        public Header header;
        public Endian endian = Endian.Little;
        public BinaryDataSet(string tempPath, StreamMode streamMode, Version? customVersion = null, Header? header = null)
        {
            this.streamMode = streamMode;
            this.tempPath = tempPath;
            if (customVersion != null)
                this.customVersion = customVersion;
            else
                this.customVersion = new Version(1, 0, 0, 0);
            if (header != null)
                this.header = header;
            else
                this.header = new Header(new byte[32]);
        }
        public Section AddSection(byte[] id, StreamMode? streamMode = null) 
        {
            Section section = new Section(id, 0, 0, streamMode == null ? this.streamMode : (StreamMode)streamMode, tempPath);
            sections.Add(section);
            return section;
        }
        public void RemoveSection(byte[] id, int index = 0)
        {
            int sectionIndex = GetSectionIndexByID(id, index);
            sections.RemoveAt(sectionIndex);
        }
        public void GetSection(byte[] id, out Section section, int index = 0)
        {
            int sectionIndex = GetSectionIndexByID(id, index);
            section = sections[sectionIndex];
        }
        private int GetSectionIndexByID(byte[] id, int index = 0)
        {
            return sections.FindIndex(s => s.ID.SequenceEqual(id));
        }
        public void BuildToDisk(string path, Endian? endian = null, int bufferSize = 4096)
        {
            // NOTES
            // Order of section definitions : [SECTION ID SIZE] [SECTION ID] [SECTION RVA] [SECTION SIZE] [SECTION CHECKSUM]

            RefreshRVAs();
            if (!File.Exists(path))
            {
                using(FileStream fs = File.Create(path))
                {
                    fs.Close();
                }
            } else
            {
                File.Delete(path);
                using (FileStream fs = File.Create(path))
                {
                    fs.Close();
                }
            }
            Endian currentEndian = endian == null ? this.endian : (Endian)endian;
            FileStream appendStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize);
            using (SHA256 sha256 = SHA256.Create())
            {
                appendStream.Write(header.headerBytes);
                appendStream.Write(Constants.thisVersion.GetBytes());
                appendStream.Write(this.customVersion.GetBytes());
                appendStream.Write(Methods.ConvertValueToBytes(sections.Count, currentEndian));
                for(int i = 0; i < sections.Count; i++)
                {
                    sections[i].StreamPosition(0);
                    byte[] hashValue;
                    if (sections[i].fileStream != null)
                        hashValue = sha256.ComputeHash(sections[i].fileStream);
                    else if (sections[i].memoryStream != null)
                        hashValue = sha256.ComputeHash(sections[i].memoryStream);
                    else
                        throw new UninstantiatedSectionStreamException($"Stream for {BitConverter.ToString(sections[i].ID)} doesn't exist");
                    appendStream.Write(Methods.SmartConvertByteArrayToBytes(sections[i].ID, currentEndian));
                    appendStream.Write(Methods.ConvertValueToBytes(sections[i].RVA, currentEndian));
                    appendStream.Write(Methods.ConvertValueToBytes(sections[i].SIZE, currentEndian));
                    appendStream.Write(hashValue);
                }
                for (int i = 0; i < sections.Count; i++)
                {
                    sections[i].StreamAppendToOtherFile(appendStream, sections[i].GetLength(),0,8192);
                    appendStream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.None, bufferSize);
                }
            }
            appendStream.Close();
            appendStream.Dispose();
        }
        public void ReadFromFile(string path, Header header, Version customVersion, Endian? endian = null, bool force = false)
        {
            Endian currentEndian = endian == null ? this.endian : (Endian)endian;
            FileStream fs = File.OpenRead(path);
            using (SHA256 sha256 = SHA256.Create())
            {
                fs.Position = 0;
                byte[] headerBytes = new byte[32];
                fs.Read(headerBytes, 0, 32);
                if (!headerBytes.SequenceEqual(header.headerBytes))
                {
                    throw new HeaderMismatchException("The specified header doesn't match the file header");
                }
                byte[] libraryVersionBytes = new byte[8];
                fs.Read(libraryVersionBytes, 0, 8);
                if (!Constants.thisVersion.GetBytes().SequenceEqual(libraryVersionBytes))
                {
                    throw new LibraryVersionMismatch("This file was built with a different version of binary creator and therfore cannot be read");
                }
                byte[] customVersionBytes = new byte[8];
                fs.Read(customVersionBytes, 0, 8);
                if (!customVersion.GetBytes().SequenceEqual(customVersionBytes))
                {
                    throw new CustomVersionMismatch("The specified custom version doesn't match in the file");
                }
                int sectionCount = 0;
                byte[] sectionCountBytes = new byte[4];
                fs.Read(sectionCountBytes, 0, 4);
                sectionCount = Methods.ReadIntFromByteArray(sectionCountBytes, currentEndian);

                byte[] RVA_Bytes = new byte[8];
                byte[] SIZE_Bytes = new byte[8];
                byte[] checksumBytes = new byte[32];
                byte[] checksumBytesCompute = new byte[32];
                byte[] id;
                long rva;
                long size;
                Section section;

                for (int i = 0; i < sectionCount; i++)
                {
                    id = Methods.SmartReadBytesFromStream(fs, currentEndian);
                    fs.Read(RVA_Bytes, 0, 8);
                    fs.Read(SIZE_Bytes, 0, 8);
                    fs.Read(checksumBytes, 0, 32);
                    rva = Methods.ReadLongFromByteArray(RVA_Bytes, currentEndian);
                    size = Methods.ReadLongFromByteArray(SIZE_Bytes, currentEndian);
                    AddSection(id,streamMode);
                    GetSection(id, out section);
                    long oldPosition = fs.Position;
                    fs.Position = rva;
                    section.StreamAppendFromOtherFile(fs, size, 0, 8192);
                    fs = File.OpenRead(path);
                    if (!force)
                    {
                        checksumBytesCompute = sha256.ComputeHash(section.GetAsStream());
                        if (!checksumBytes.SequenceEqual(checksumBytesCompute))
                        {
                            CleanUp();
                            throw new ChecksumMismatchException();
                        }
                    }
                    fs.Position = oldPosition;
                }
            }
            fs.Close();
            fs.Dispose();
        }
        public void RefreshRVAs()
        {
            RefreshSizes();
            long totalOffset = Constants.headerSize + Constants.versionsSize + Constants.sectionAmountIntSize + (sections.Count * (Constants.checksumSize + Constants.sectionEntryMetaSize));
            foreach(Section section in sections)
            {
                totalOffset += section.ID.Length;
            }
            for(int i = 0; i < sections.Count; i++)
            {
                sections[i].RVA = totalOffset;
                totalOffset += sections[i].SIZE;
            }
        }
        public void RefreshSizes()
        {
            for(int i = 0; i < sections.Count; i++)
            {
                sections[i].SIZE = sections[i].GetLength();
            }
        }
        public void ConvertToFileStream()
        {

        }
        public void ConvertToMemoryStream()
        {

        }
        public void CleanUp()
        {
            for (int i = 0; i < sections.Count; i++)
            {
                sections[i].Cleanup();
            }
            sections.Clear();

        }
        ~BinaryDataSet() { CleanUp(); }
    }
    public class Section
    {
        public byte[] ID;
        public long RVA;
        public long SIZE;
        public FileStream? fileStream;
        public MemoryStream? memoryStream;
        public string tempPath { get; private set; }
        public string tempFile { get; private set; }
        public Endian endian { get; private set; }
        private Random rng = new Random();

        public Section(byte[] id, long rva, long size, StreamMode streamMode, string tempPath)
        {
            ID = id;
            RVA = rva;
            SIZE = size;
            this.tempPath = tempPath;
            if (streamMode == StreamMode.File)
            {
                bool fileExists = true;
                while (fileExists)
                {
                    fileExists = File.Exists(tempPath);
                    tempFile = Path.Combine(tempPath, "SECTION_" + Methods.RandomString(8, rng));
                }
                memoryStream = null;
                if (!Directory.Exists(tempPath))
                    Directory.CreateDirectory(tempPath);
                fileStream = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } else
            {
                memoryStream = new MemoryStream();
                fileStream = null;
            }
        }
        public Stream GetAsStream()
        {
            if (fileStream != null)
                return fileStream;
            else if (memoryStream != null)
                return memoryStream;
            else
                throw new UninstantiatedSectionStreamException();
        }
        public long GetLength()
        {
            if (fileStream != null)
                return fileStream.Length;
            else if (memoryStream != null)
                return memoryStream.Length;
            else
                return -1L;
        }
        public void StreamWrite(byte[] data, int offset, int count)
        {
            if (fileStream != null)
                fileStream.Write(data, offset, count);
            else if (memoryStream != null)
                memoryStream.Write(data, offset, count);
        }
        public void StreamRead(ref byte[] buffer, int offset, int count)
        {
            if (fileStream != null)
                fileStream.Read(buffer, offset, count);
            else if (memoryStream != null)
                memoryStream.Read(buffer, offset, count);
        }
        public void StreamPosition(int address)
        {
            if (fileStream != null)
                fileStream.Position = address;
            else if (memoryStream != null)
                memoryStream.Position = address;
        }
        public void StreamRemove(int offset, int count, int bufferSize = 4096)
        {
            if (fileStream != null)
            {
                string oldTempFile = tempFile;
                tempFile = GetNewTempFile();
                long oldPosition = fileStream.Position;
                fileStream.Close();
                fileStream = new FileStream(tempFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                using (FileStream readFileStream = new FileStream(oldTempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (offset + oldPosition > 0)
                    {
                        StreamAppendFromOtherFile(readFileStream, offset + oldPosition, bufferSize: bufferSize);
                    }
                    if (readFileStream.Length - (offset + oldPosition) - count != 0)
                    {
                        readFileStream.Position = offset + oldPosition + count;
                        StreamAppendFromOtherFile(readFileStream, readFileStream.Length - (offset + oldPosition) - count, bufferSize: bufferSize);
                    }
                }
                File.Delete(oldTempFile);
            } else if (memoryStream != null)
            {
                byte[] data = new byte[memoryStream.Length - count];
                long oldPosition = memoryStream.Position;
                if (offset + oldPosition > 0)
                {
                    memoryStream.Read(data, 0, offset + (int)oldPosition);
                }
                if (memoryStream.Length - (offset + oldPosition) - count != 0)
                {
                    memoryStream.Position = offset + oldPosition + count;
                    memoryStream.Read(data, 0, (int)memoryStream.Length - (int)(offset + oldPosition) - count);
                }
            }
        }
        /// <summary>
        /// Adds bytes to the end of the data
        /// </summary>
        /// <param name="buffer">The data to add</param>
        public void StreamAppend(byte[] buffer)
        {
            if (fileStream != null)
            {
                string path = fileStream.Name;
                long oldPosition = fileStream.Position;
                fileStream.Close();
                using (FileStream appendStream = new FileStream(path, FileMode.Append))
                {
                    appendStream.Write(buffer);
                }
                fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fileStream.Position = oldPosition;
            } else if (memoryStream != null)
            {
                byte[] newBuffer = new byte[memoryStream.Length + buffer.Length];
                Buffer.BlockCopy(memoryStream.ToArray(), 0, newBuffer, 0, (int)memoryStream.Length);
                Buffer.BlockCopy(buffer, 0, newBuffer, (int)memoryStream.Length, buffer.Length);
                memoryStream.Close(); memoryStream.Dispose();
                memoryStream = new MemoryStream(newBuffer);
            }
        }
        /// <summary>
        /// Adds bytes to the end of the data directly from a filestream. StreamMode.File Supports files larger than 2GB.
        /// </summary>
        /// <param name="file">The stream to append from</param>
        /// <param name="length">The length of the data to append to the section</param>
        /// <param name="offset">The offset in the file to append from</param>
        /// <param name="bufferSize">The size of the buffer for the operation</param>
        public void StreamAppendFromOtherFile(FileStream file, long length, int offset = 0, int bufferSize = 4096)
        {
            if (fileStream != null)
            {
                string path = fileStream.Name;
                long oldPosition = fileStream.Position;
                long oldFilePosition = file.Position;
                fileStream.Close();
                byte[] buffer = new byte[bufferSize];
                using (FileStream appendStream = new FileStream(path, FileMode.Append))
                {
                    for (int i = 0; i < Math.Ceiling((double)length / (double)bufferSize); i++)
                    {
                        long availableSpace = length - ((long)i * bufferSize);
                        file.Position = ((long)i * bufferSize) + offset + oldFilePosition;
                        if (availableSpace >= bufferSize)
                        {
                            file.Read(buffer, offset, bufferSize);
                            appendStream.Write(buffer);
                        } else if (availableSpace < 0)
                        {
                            buffer = new byte[length];
                            file.Read(buffer, offset, (int)length);
                            appendStream.Write(buffer);
                        }
                        else
                        {
                            buffer = new byte[availableSpace];
                            file.Read(buffer, offset, (int)availableSpace); // availableSpace should be able to convert safely to int as the available space would be in range.
                            appendStream.Write(buffer);
                        }
                    }
                }
                fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                fileStream.Position = oldPosition;
            } else if(memoryStream != null)
            {
                if (length + memoryStream.Length > int.MaxValue)
                    throw new OverflowException("Cannot complete the operation as it will result in an overflow");
                byte[] addBytes = new byte[length];
                byte[] originalBytes = memoryStream.ToArray();
                file.Read(addBytes, offset, addBytes.Length);
                memoryStream = new MemoryStream(originalBytes.Concat(addBytes).ToArray());
            }
        }
        public void StreamAppendFromMemoryStream(MemoryStream stream, int length, int offset = 0)
        {
            if (memoryStream != null && fileStream == null)
            {
                if (length + memoryStream.Length > int.MaxValue)
                    throw new OverflowException("Cannot complete the operation as it will result in an overflow");
            }
            byte[] data = new byte[length];
            stream.Read(data, offset, length);
            StreamAppend(data);
        }
        public void StreamAppendToOtherFile(FileStream file, long length, int offset = 0, int bufferSize = 4096)
        {
            if (fileStream != null)
            {
                string filePath = file.Name;
                long oldPosition = fileStream.Position;
                long oldFilePosition = file.Position;
                
                file.Close();
                fileStream.Position = 0;
                byte[] buffer = new byte[bufferSize];
                using (FileStream appendStream = new FileStream(filePath, FileMode.Append))
                {
                    for (int i = 0; i < Math.Ceiling((double)length / (double)bufferSize); i++)
                    {
                        long availableSpace = length - ((long)i * bufferSize);
                        fileStream.Position = ((long)i * bufferSize) + offset;
                        if (availableSpace >= bufferSize)
                        {
                            fileStream.Read(buffer, offset, bufferSize);
                            appendStream.Write(buffer);
                        }
                        else if (availableSpace < 0)
                        {
                            buffer = new byte[length];
                            fileStream.Read(buffer, offset, (int)length);
                            appendStream.Write(buffer);
                        }
                        else
                        {
                            buffer = new byte[availableSpace];
                            fileStream.Read(buffer, offset, (int)availableSpace); // availableSpace should be able to convert safely to int as the available space would be in range.
                            appendStream.Write(buffer);
                        }
                    }
                    appendStream.Close();
                }
            }
            else if (memoryStream != null)
            {
                string filePath = file.Name;
                long oldFilePosition = file.Position;

                file.Close();
                file = new FileStream(filePath, FileMode.Append);

                byte[] data = memoryStream.ToArray();
                file.Write(data);
                file.Close();
            }
        }
        private string GetNewTempFile()
        {
            bool fileExists = true;
            string newTempFile = String.Empty;
            while (fileExists)
            {
                fileExists = File.Exists(tempPath);
                newTempFile = Path.Combine(tempPath, "SECTION_" + Methods.RandomString(8, rng));
            }
            return newTempFile;
        }
        private void SetToBigEndian()
        {
            endian = Endian.Big;
        }
        private void SetToLittleEndian()
        {
            endian = Endian.Little;
        }
        public void Cleanup()
        {
            if (fileStream != null)
            {
                fileStream.Close();
                fileStream.Dispose();
                File.Delete(tempFile);
            }
            if (memoryStream != null)
            {
                memoryStream.Close();
                memoryStream.Dispose();
            }
        }
        ~Section()
        {
            Cleanup();
        }
    }
    public static class Methods
    {
        public static string RandomString(int length, Random rng)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[rng.Next(s.Length)]).ToArray());
        }
        public static Endian GetArchitectureEndian()
        {
            if (BitConverter.IsLittleEndian)
                return Endian.Little;
            else
                return Endian.Big;
        }

        /// <summary>
        /// Get a chunck of data from a byte array while incrementing a data counter.
        /// </summary>
        /// <param name="dc"></param>
        /// <param name="data"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public static Span<byte> GetChunkDChandle(ref int dc, ref byte[] data, int amount)
        {
            Span<byte> finalized;
            finalized = data.AsSpan(dc, amount);
            dc += amount;
            return finalized;
        }
        #region Converters
        public static byte[] ConvertValueToBytes(object value, Endian endianness)
        {
            Span<byte> span = new Span<byte>();
            switch (value)
            {
                case ushort s:
                    span = new byte[2];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt16LittleEndian(span, (ushort)value);
                    }
                    break;

                case short s:
                    span = new byte[2];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteInt16BigEndian(span, (short)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt16LittleEndian(span, (short)value);
                    }
                    break;

                case uint s:
                    span = new byte[4];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(span, (uint)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)value);
                    }
                    break;

                case int s:
                    span = new byte[4];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteInt32BigEndian(span, (int)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt32LittleEndian(span, (int)value);
                    }
                    break;

                case ulong s:
                    span = new byte[8];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteUInt64BigEndian(span, (ulong)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt64LittleEndian(span, (ulong)value);
                    }
                    break;

                case long s:
                    span = new byte[8];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteInt64BigEndian(span, (long)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt64LittleEndian(span, (long)value);
                    }
                    break;
#if NET8_0_OR_GREATER
                case UInt128 s:
                    span = new byte[16];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteUInt128BigEndian(span, (UInt128)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteUInt128LittleEndian(span, (UInt128)value);
                    }
                    break;

                case Int128 s:
                    span = new byte[16];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteInt128BigEndian(span, (Int128)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteInt128LittleEndian(span, (Int128)value);
                    }
                    break;
#endif
                case float s:
#if NET8_0_OR_GREATER
                    span = new byte[4];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteSingleBigEndian(span, (float)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteSingleLittleEndian(span, (float)value);
                    }
                    break;
#else
                    span = BitConverter.GetBytes((float)value);
                    if (endianness == Endian.Big)
                    {
                        if (BitConverter.IsLittleEndian)
                            span.Reverse();
                    } else
                    {
                        if (!BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    break;
#endif
                case double s:
#if NET8_0_OR_GREATER
                    span = new byte[8];
                    if (endianness == Endian.Big)
                    {
                        BinaryPrimitives.WriteDoubleBigEndian(span, (double)value);
                    }
                    else
                    {
                        BinaryPrimitives.WriteDoubleLittleEndian(span, (double)value);
                    }
                    break;
#else
                    span = BitConverter.GetBytes((double)value);
                    if (endianness == Endian.Big)
                    {
                        if (BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    else
                    {
                        if (!BitConverter.IsLittleEndian)
                            span.Reverse();
                    }
                    break;
#endif
            }
            return span.ToArray();
        }
        public static byte[] SmartConvertByteArrayToBytes(byte[] data, Endian endianness)
        {
            byte[] startIndex = ConvertValueToBytes(data.Length, endianness);
            return startIndex.Concat(data).ToArray();
        }
        public static byte[] SmartConvertByteArrayToBytes(Span<byte> data, Endian endianness)
        {
            byte[] startIndex = ConvertValueToBytes(data.Length, endianness);
            return startIndex.Concat(data.ToArray()).ToArray();
        }
        public static byte[] SmartConvertBytesToByteArray(byte[] data, Endian endianness, int index = 0)
        {
            int arrayLength = ReadIntFromByteArray(data, endianness, index);
            Span<byte> finalized = new Span<byte>(data, index + 4, arrayLength);
            return finalized.ToArray();
        }
        public static byte[] SmartReadBytesFromStream(Stream fs, Endian endianness, int index = 0)
        {
            byte[] arrayLengthBytes = new byte[4];
            fs.Read(arrayLengthBytes, index, arrayLengthBytes.Length);
            int arrayLength = ReadIntFromByteArray(arrayLengthBytes, endianness, 0);
            byte[] finalized = new byte[arrayLength];
            fs.Read(finalized, 0, finalized.Length);
            return finalized;
        }
        public static byte[] SmartConvertBytesToByteArray(byte[] data, Endian endianness, ref int incrementer, int index = 0)
        {
            int arrayLength = ReadIntFromByteArray(data, endianness, index);
            incrementer += 4 + arrayLength;
            Span<byte> finalized = new Span<byte>(data, index + 4, arrayLength);
            return finalized.ToArray();
        }
        public static byte CreateByte(bool[] bits)
        {
            if (bits.Length > 8)
            {
                throw new ArgumentOutOfRangeException();
            }
            return (byte)bits.Reverse().Select((val, i) => Convert.ToByte(val) << i).Sum();
        }
        public static byte[] IntToBytes(int integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] UIntToBytes(uint integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] LongToBytes(long integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ULongToBytes(ulong integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ShortToBytes(short integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] UShortToBytes(short integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] SingleToBytes(float integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] DoubleToBytes(double integer, bool BigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(integer);
            if (BigEndian && BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
            return bytes;
        }
        public static byte[] ConvertBoolArrayToByteArray(bool[] source)
        {
            byte[] finalized = new byte[(int)Math.Ceiling(source.Length / 8f)];
            bool[] currentBools = new bool[8];
            for (int i = 0; i < finalized.Length; i++)
            {
                for (int k = 0; k < 8; k++)
                {
                    currentBools[k] = k + (i * 8) + 1 <= source.Length ? source[k + (i * 8)] : false;
                }
                byte result = 0;
                int index = 0;

                foreach (bool b in currentBools)
                {
                    if (b)
                        result |= (byte)(1 << (7 - index));

                    index++;
                }
                finalized[i] = result;
            }
            return finalized;
        }
        public static bool[] ConvertByteArrayToBoolArray(byte[] b)
        {
            bool[] finalized = new bool[b.Length * 8];
            int index = 0;
            foreach (byte b2 in b)
            {
                bool[] result = new bool[8];

                // check each bit in the byte. if 1 set to true, if 0 set to false
                for (int i = 0; i < 8; i++)
                    result[i] = (b2 & (1 << i)) != 0;

                // reverse the array
                Array.Reverse(result);

                result.CopyTo(finalized, index * 8);
                index++;
            }
            return finalized;
        }
        #endregion
        #region Random
        public static float RandomFloatInRange(float min, float max, Random random)
        {
#if NETSTANDARD2_1
            var x = (float)random.NextDouble();
#else
            var x = random.NextSingle();
#endif

            return x * max + (1 - x) * min;
        }
        public static double RandomDoubleInRange(float min, float max, Random random)
        {
            var x = random.NextDouble();

            return x * max + (1 - x) * min;
        }
        #endregion
        #region Readers
        public static ushort ReadUshortFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt16(copied);
        }
        public static ushort ReadUshortFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt16(copied);
        }
        public static ushort ReadUShortFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt16(bytes);
        }
        public static short ReadShortFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt16(copied);
        }
        public static short ReadShortFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 2);
            incrementer += 2;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt16(copied);
        }
        public static short ReadShortFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[2];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt16(bytes);
        }
        public static uint ReadUIntFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt32(copied);
        }
        public static uint ReadUIntFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt32(copied);
        }
        public static uint ReadUIntFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt32(bytes);
        }
        public static int ReadIntFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt32(copied);
        }
        public static int ReadIntFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt32(copied);
        }
        public static int ReadIntFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt32(bytes);
        }
        public static ulong ReadUlongFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt64(copied);
        }
        public static ulong ReadUlongFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToUInt64(copied);
        }
        public static ulong ReadULongFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt64(bytes);
        }
        public static long ReadLongFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt64(copied);
        }
        public static long ReadLongFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToInt64(copied);
        }
        public static long ReadLongFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt64(bytes);
        }
        public static float ReadSingleFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToSingle(copied);
        }
        public static float ReadSingleFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 4);
            incrementer += 4;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToSingle(copied);
        }
        public static float ReadSingleFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[4];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToSingle(bytes);
        }
        public static double ReadDoubleFromByteArray(byte[] bytes, Endian endianess, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToDouble(copied);
        }
        public static double ReadDoubleFromByteArray(byte[] bytes, Endian endianess, ref int incrementer, int startIndex = 0)
        {
            Span<byte> copied = bytes.AsSpan(startIndex, 8);
            incrementer += 8;
            if (endianess == Endian.Big) { copied.Reverse(); }
            return BitConverter.ToDouble(copied);
        }
        public static double ReadDoubleFromStream(Stream stream, Endian endianess, int offset = 0)
        {
            byte[] bytes = new byte[8];
            stream.Read(bytes, offset, bytes.Length);
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToDouble(bytes);
        }
        #endregion
        #region SpanReaders
        public static ushort ReadUshortFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt16(bytes);
        }
        public static short ReadShortFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt16(bytes);
        }
        public static uint ReadUIntFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt32(bytes);
        }
        public static int ReadIntFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt32(bytes);
        }
        public static ulong ReadUlongFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToUInt64(bytes);
        }
        public static long ReadLongFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToInt64(bytes);
        }
        public static float ReadSingleFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToSingle(bytes);
        }
        public static double ReadDoubleFromSpan(Span<byte> bytes, Endian endianess)
        {
            if (endianess == Endian.Big) { bytes.Reverse(); }
            return BitConverter.ToDouble(bytes);
        }

        #endregion
    }
    public class Version
    {
        public short major = 1;
        public short minor = 0;
        public short patch = 0;
        public short preRelease = 0;
        public Version(short major = 1, short minor = 0, short patch = 0, short preRelease = 0)
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
            this.preRelease = preRelease;
        }
        public byte[] GetBytes()
        {
            List<byte> bytesList = new List<byte>();
            bytesList.AddRange(Methods.ConvertValueToBytes(major, Endian.Little));
            bytesList.AddRange(Methods.ConvertValueToBytes(minor, Endian.Little));
            bytesList.AddRange(Methods.ConvertValueToBytes(patch, Endian.Little));
            bytesList.AddRange(Methods.ConvertValueToBytes(preRelease, Endian.Little));
            return bytesList.ToArray();
        }
        public void GetFromBytes(byte[] bytes)
        {
            int dc = 0;
            major = Methods.ReadShortFromByteArray(bytes, Endian.Little, dc); dc += 2;
            minor = Methods.ReadShortFromByteArray(bytes, Endian.Little, dc); dc += 2;
            patch = Methods.ReadShortFromByteArray(bytes, Endian.Little, dc); dc += 2;
            preRelease = Methods.ReadShortFromByteArray(bytes, Endian.Little, dc); dc += 2;
        }
    }
    public class Header
    {
        public byte[] headerBytes { get; private set; }
        public Header(byte[] bytes)
        {
            if(bytes == null) throw new ArgumentNullException("bytes");
            if (bytes.Length < 32) throw new IncorrectHeaderSizeException("The header size was smaller than 32 bytes");
            if (bytes.Length > 32) throw new IncorrectHeaderSizeException("The header size was greater than 32 bytes");
            headerBytes = bytes;
        }
    }
}