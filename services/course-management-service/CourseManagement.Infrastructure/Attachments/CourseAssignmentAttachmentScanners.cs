using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using CourseManagement.Application.CourseAssignments;
using Microsoft.Extensions.Options;

namespace CourseManagement.Infrastructure.Attachments;

public sealed class DevelopmentCourseAssignmentAttachmentScanner : ICourseAssignmentAttachmentScanner
{
    public async Task<CourseAssignmentAttachmentScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        var buffer = new byte[64 * 1024];
        while (await content.ReadAsync(buffer.AsMemory(), cancellationToken) > 0) { }
        return new CourseAssignmentAttachmentScanResult(IsClean: true);
    }
}

public sealed class ClamAvCourseAssignmentAttachmentScanner(IOptions<CourseAssignmentAttachmentStorageOptions> options)
    : ICourseAssignmentAttachmentScanner
{
    private readonly CourseAssignmentAttachmentStorageOptions _options = options.Value;

    public async Task<CourseAssignmentAttachmentScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ScannerTimeoutSeconds));
        using var client = new TcpClient();
        await client.ConnectAsync(_options.ClamAvHost, _options.ClamAvPort, timeout.Token);
        await using var network = client.GetStream();
        await network.WriteAsync("zINSTREAM\0"u8.ToArray(), timeout.Token);
        var buffer = new byte[64 * 1024];
        var length = new byte[sizeof(int)];
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(), timeout.Token)) > 0)
        {
            BinaryPrimitives.WriteInt32BigEndian(length, read);
            await network.WriteAsync(length, timeout.Token);
            await network.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        await network.WriteAsync(new byte[sizeof(int)], timeout.Token);
        await network.FlushAsync(timeout.Token);

        var response = await ReadResponseAsync(network, timeout.Token);
        if (response.EndsWith("OK", StringComparison.OrdinalIgnoreCase))
            return new CourseAssignmentAttachmentScanResult(IsClean: true);
        if (response.Contains("FOUND", StringComparison.OrdinalIgnoreCase))
        {
            var separator = response.IndexOf(':');
            var threat = separator >= 0 ? response[(separator + 1)..].Replace("FOUND", string.Empty,
                StringComparison.OrdinalIgnoreCase).Trim() : "ClamAV threat";
            return new CourseAssignmentAttachmentScanResult(IsClean: false, threat);
        }
        throw new InvalidOperationException("ClamAV returned an unexpected scan response.");
    }

    private static async Task<string> ReadResponseAsync(NetworkStream network, CancellationToken cancellationToken)
    {
        var response = new List<byte>(128);
        var buffer = new byte[1];
        while (response.Count < 4096)
        {
            var read = await network.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0 || buffer[0] is (byte)'\n' or (byte)'\0') break;
            response.Add(buffer[0]);
        }
        if (response.Count == 0) throw new InvalidOperationException("ClamAV returned an empty scan response.");
        return Encoding.UTF8.GetString(response.ToArray());
    }
}
