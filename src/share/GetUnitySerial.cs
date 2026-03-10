using System;
using System.Buffers;
using System.IO;
using System.Text;
var licenseCertificate = Environment.GetEnvironmentVariable("UNITY_LICENSE");
if (string.IsNullOrEmpty(licenseCertificate))
{
    Console.Error.WriteLine("The UNITY_LICENSE environment variable is not set.");
    Environment.Exit(1);
}

const string startKey = "<DeveloperData Value=\"";
const string endKey = "\"/>";

var startIndex = licenseCertificate.AsSpan().IndexOf(startKey);
if (startIndex == -1)
{
    Console.Error.WriteLine("The license certificate does not contain DeveloperData.");
    Environment.Exit(1);
}

startIndex += startKey.Length;
var endIndex = licenseCertificate.AsSpan(startIndex).IndexOf(endKey);
if (endIndex == -1)
{
    Console.Error.WriteLine("The license certificate contains malformed DeveloperData.");
    Environment.Exit(1);
}

var base64Serial = licenseCertificate.AsSpan().Slice(startIndex, endIndex);
var maxLength = base64Serial.Length * 3 / 4;
var serialBytes = ArrayPool<byte>.Shared.Rent(maxLength);
try
{
    if (!Convert.TryFromBase64Chars(base64Serial, serialBytes, out var bytesWritten))
    {
        Console.Error.WriteLine("Failed to decode the base64-encoded DeveloperData.");
        Environment.Exit(1);
    }

    // Skip the first 4 bytes and convert to string
    var serial = Encoding.UTF8.GetString(serialBytes, 4, bytesWritten - 4);

    var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
    if (!string.IsNullOrEmpty(output))
    {
        using var writer = File.AppendText(output);

        writer.Write("serial=");
        writer.WriteLine(serial);
    }
}
finally
{
    ArrayPool<byte>.Shared.Return(serialBytes);
}