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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace BinaryCreator
{
    public class Binary
    {
        public static readonly int headerAndVersionSize = 20;
        public static readonly Endian littleEndian = new Endian(false, true);
        public static readonly Endian bigEndian = new Endian(true, false);
        public static readonly BinVersion libraryVersion = new BinVersion(1,0,0,0);

        [StructLayout(LayoutKind.Sequential)]
        public struct BinDataSet
        {
            public BinHeader header;
            public BinVersion versionCustom;
            public BinSectionDefinition[] sectionDefinitions;
            public byte[][] sectionData;
            public void SetDataInSection(byte[] id, byte[] value) { sectionData[GetSectionIndexByID(id)] = value; }
            public ref byte[] GetDataInSection(byte[] id) { Console.WriteLine(GetSectionIndexByID(id));  return ref sectionData[GetSectionIndexByID(id)]; }
            public void SetRVAinSection(byte[] id, ulong value) { GetSectionByID(id).SetRVA(value); }
            public void SetSizeInSection(byte[] id, ulong value) { GetSectionByID(id).SetSize(value); }
            public ref BinSectionDefinition GetSectionByID(byte[] id) 
            {
                for(int i = 0; i < sectionDefinitions.Length; i++)
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
                Array.Resize(ref sectionDefinitions, origSize+1);
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
                //byte[] finalized = new byte[GetTotalFileSize()];
                //int dc = 0; //Data Counter
                //header.GetHeader().CopyTo(finalized, dc); dc += 8;
                //versionLib.GetByteArray().CopyTo(finalized, dc); dc += 4;
                //versionCustom.GetByteArray().CopyTo(finalized, dc); dc += 4;
                //foreach(BinSectionDefinition section in sectionDefinitions)
                //{
                //    section.id.CopyTo(finalized, dc); dc += 8;
                //    ULongToBytes(section.rva).CopyTo(finalized, dc); dc += 8;
                //    ULongToBytes(section.size).CopyTo(finalized, dc); dc += 8;
                //    finalized[dc] = ConvertBoolArrayToByte([section.isReadOnly]); dc += 1;
                //}
                //foreach (byte[] data in sectionData)
                //{
                //    data.CopyTo(finalized, dc); dc += data.Length;
                //}
                using(FileStream stream = File.Create(path))
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
                for (int i = 0; i<sectionDefinitions.Length; i++)
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
            public void ReadFromByteArray(byte[] data, BinVersion softwareVersion, BinHeader header, Endian endian )
            {
                int DC = 0;
                Span<byte> headerComputed = GetChunkDChandle(ref DC, ref data, 8);
                Span<byte> headerAsSpan = header.GetHeader().AsSpan(0,8);
                
                if (!headerAsSpan.SequenceEqual(headerComputed))
                {
                    throw new Exceptions.HeaderMismatchException();
                }

                Span<byte> versionLibCompute = GetChunkDChandle(ref DC, ref data, 4);
                Span<byte> versionCustomCompute = GetChunkDChandle(ref DC, ref data, 4);
                Span<byte> thisVersionLibSpan = libraryVersion.GetByteArray().AsSpan();
                
                if(!versionLibCompute.SequenceEqual(thisVersionLibSpan))
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

                for (int i = 0; i<sectionAmount; i++)
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
            public void SetHeader(byte[] value) { Array.Resize(ref value, 8);  this.value = value; }
            public byte[] GetHeader() { byte[] v = this.value; Array.Resize(ref v, 8); return v; }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BinVersion
        {
            public byte versionMaj {  get; private set; }
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

            public void SetID(byte[] id) { Array.Resize(ref id, 8); this.id = id;}
            public byte[] GetID() { byte[] v = this.id;  Array.Resize(ref v, 8); return v; }
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
                        if(BitConverter.IsLittleEndian)
                            span.Reverse();
                    } else
                    {
                        if(!BitConverter.IsLittleEndian)
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
            if(BigEndian&&BitConverter.IsLittleEndian || !BigEndian && !BitConverter.IsLittleEndian) { Array.Reverse(bytes); }
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
            byte[] finalized = new byte[(int)Math.Ceiling(source.Length/8f)];
            bool[] currentBools = new bool[8];
            for(int i = 0; i < finalized.Length; i++)
            {
                for(int k = 0; k < 8; k++)
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

                result.CopyTo(finalized, index*8);
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
}