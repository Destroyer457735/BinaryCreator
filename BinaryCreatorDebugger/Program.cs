// See https://aka.ms/new-console-template for more information
#pragma warning disable CS8602 // Dereference of a possibly null reference.
using System.Collections;
using System.Runtime.InteropServices;
using BinaryCreator;
using Microsoft.VisualBasic.FileIO;

byte[] boolsArray = Binary.ConvertBoolArrayToByteArray(new bool[16] { true, false, false, true, true, true, true, false, false, false, false, true, false, true, true, true });
Console.WriteLine(BitConverter.ToString(boolsArray));
bool[] bools = Binary.ConvertByteArrayToBoolArray(boolsArray);
Console.WriteLine(String.Join("-",bools.Select(b=>b.ToString())));

Binary.BinDataSet bin = new Binary.BinDataSet();
byte[] trailingZeros8 = new byte[8];
byte[] header = new byte[8] { (byte)'b', (byte)'i', (byte)'n', 0x00, 0x00, 0x00, 0x00, 0x00 };
Binary.BinHeader binHeader = new Binary.BinHeader();
binHeader.SetHeader(header);

long[] longs = new long[2048];
List<byte> sectionData = new List<byte>();
Random random = new Random();
sectionData.AddRange(new byte[512]);
for (int i = 0; i<longs.Length; i++)
{
    longs[i] = random.NextInt64();
    sectionData.AddRange(Binary.LongToBytes(longs[i], true));
}

short[] shorts = new short[2048];
List<byte> intData = new List<byte>();
intData.AddRange(new byte[512]);
for (int i = 0; i < longs.Length; i++)
{
    shorts[i] = (short)random.Next(short.MinValue,short.MaxValue);
    intData.AddRange(Binary.ShortToBytes(shorts[i], true));
    intData.AddRange(new byte[] { 0x00, 0x00, 0x00 });
}

float[] floats = new float[4096];
List<byte> floatData = new List<byte>();
floatData.AddRange(new byte[512]);
for (int i = 0; i < floats.Length; i++)
{
    float rnd = Binary.RandomFloatInRange(0, 0.000000000000000000000000000001f, random);
    Console.WriteLine(rnd.ToString());
    floats[i] = rnd;
    floatData.AddRange(Binary.SingleToBytes(floats[i], true));
}

byte[] bytes = new byte[4096];
byte[] _byte = new byte[1];
List<byte> byteData = new List<byte>();
byteData.AddRange(new byte[512]);
for (int i = 0; i < bytes.Length; i++)
{
    random.NextBytes(_byte);
    byteData.AddRange(_byte);
    random.NextBytes(_byte);
    byteData.AddRange(_byte);
    byteData.AddRange(new byte[1]);
    byteData.AddRange(new byte[5]);
}

byte[] dataSection = new byte[8] { (byte)'.', (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0x00, 0x00, 0x00 };
byte[] intSection = new byte[8] { (byte)'.', (byte)'i', (byte)'n', (byte)'t', (byte)'s', 0x00, 0x00, 0x00 };
byte[] floatSection = new byte[8] { (byte)'.', (byte)'f', (byte)'l', (byte)'o', (byte)'a', (byte)'t', 0x00, 0x00 };
byte[] byteSection = new byte[8] { (byte)'.', (byte)'b', (byte)'y', (byte)'t', (byte)'e', 0x00, 0x00, 0x00 };
byte[] ptrSection = new byte[8] { (byte)'.', (byte)'p', (byte)'t', (byte)'r', 0x00, 0x00, 0x00, 0x00 };

bin.header.SetHeader(header);
bin.AddSection(
    dataSection,
    sectionData.ToArray(),
    true
);
Binary.BinSectionDefinition test = bin.sectionDefinitions[0];
Console.WriteLine(object.Equals(test, bin.GetSectionByID(dataSection)));
bin.GetSectionByID(dataSection).rva = ulong.MaxValue;

bin.AddSection(
    intSection,
    intData.ToArray(),
    true
);
bin.SetRVAinSection(intSection, ulong.MaxValue);

bin.AddSection(
    floatSection,
    floatData.ToArray(),
    true
);
bin.SetRVAinSection(floatSection, ulong.MaxValue);

//bin.AddSection(
//    byteSection,
//    byteData.ToArray(),
//    true
//);

//bin.SetRVAinSection(byteSection, ulong.MaxValue);

bin.RefreshRVAs();
bin.RefreshSizes();

Console.WriteLine(BitConverter.ToString(bin.header.value));
Console.WriteLine(BitConverter.ToString(bin.sectionDefinitions[0].GetID()));
Console.WriteLine($"Section data: {BitConverter.ToString(bin.GetDataInSection(new byte[8] { (byte)'.', (byte)'d', (byte)'a', (byte)'t', (byte)'a', 0x00, 0x00, 0x00 }))}");
Console.WriteLine(bin.GetSectionByID(dataSection).rva.ToString());
Console.WriteLine(bin.GetTotalFileSize());
bin.WriteToDisk(System.AppDomain.CurrentDomain.BaseDirectory + @"\BinaryData.bin", Binary.littleEndian);
byte[] finalized;
bin.WriteToByteArray(out finalized, Binary.littleEndian);
Console.WriteLine(BitConverter.ToString(finalized));
Console.WriteLine(Binary.ReadShortFromByteArray(new byte[] { 0x10, 0xFF, 0x01 }, Binary.littleEndian, 0));

Binary.BinDataSet readSet = new Binary.BinDataSet();
readSet.ReadFromByteArray(finalized, new Binary.BinVersion(),binHeader,Binary.littleEndian);
Console.WriteLine(BitConverter.ToString(readSet.GetDataInSection(dataSection)));

Console.WriteLine("================ Testing Smart Converters ================");
int incrementer = 0;
byte[] SmartConvertByteArray = new byte[16] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x00, 0x01, 0x02, 0x03, 0x04, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05 };
Console.WriteLine("Converting Byte Array to Smart Byte Array");
byte[] SmartConvertByteArrayConverted = Binary.SmartConvertByteArrayToBytes(SmartConvertByteArray, Binary.littleEndian);
Console.WriteLine($"Result: {BitConverter.ToString(SmartConvertByteArrayConverted)}");
Console.WriteLine("Converting Back to Normal Byte Array");
byte[] ReconvertedSmartByteArray = Binary.SmartConvertBytesToByteArray(SmartConvertByteArrayConverted, Binary.littleEndian, ref incrementer);
Console.WriteLine($"Result: {BitConverter.ToString(ReconvertedSmartByteArray)}");
if (SmartConvertByteArray.SequenceEqual(ReconvertedSmartByteArray))
    Console.WriteLine("Results Match!");
else Console.WriteLine("Results Don't Match!");
Console.WriteLine($"Incrementer: {incrementer}");

Console.WriteLine("================ Testing Bool Array to Byte Array ================");
bool[] boolArray = new bool[random.Next(100)];
for(int i = 0; i < boolArray.Length; i++)
{
    boolArray[i] = random.Next(2) == 0;
}
Console.WriteLine($"Converting bools to byte array: {string.Join(",",boolArray)}");
finalized = Binary.ConvertBoolArrayToByteArray(boolArray);
Console.WriteLine($"Result: {BitConverter.ToString(finalized)}");
Console.WriteLine($"Converting back to byte array...");
bool[] convertedBools = Binary.ConvertByteArrayToBoolArray(finalized);
Console.WriteLine($"Result: {string.Join(",", convertedBools)}");
if (convertedBools.Zip(boolArray, (a, b) => a == b).All(x => x))
    Console.WriteLine($"Results Match!");
else Console.WriteLine($"Results Don't Match!");