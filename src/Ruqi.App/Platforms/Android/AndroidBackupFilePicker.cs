using System.Text;
using Android.Content;
using Ruqi.App.Services;

namespace Ruqi.App.Platforms.Android;

public sealed class AndroidBackupFilePicker : IBackupFilePicker
{
    public async Task<string?> ExportJsonAsync(string suggestedFileName, string jsonContent, CancellationToken ct = default)
    {
        var uri = await MainActivity.CreateDocumentAsync(suggestedFileName, "application/json");
        if (uri is null)
        {
            return null;
        }

        var resolver = Platform.AppContext.ContentResolver
            ?? throw new InvalidOperationException("无法访问系统文件服务。");

        await using var stream = resolver.OpenOutputStream(uri)
            ?? throw new IOException("无法打开目标文件进行写入。");
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(jsonContent);
        await writer.FlushAsync(ct);

        return suggestedFileName;
    }

    public async Task<string?> PickAndReadJsonAsync(CancellationToken ct = default)
    {
        var uri = await MainActivity.OpenDocumentAsync(["application/json", "text/plain", "*/*"]);
        if (uri is null)
        {
            return null;
        }

        var resolver = Platform.AppContext.ContentResolver
            ?? throw new InvalidOperationException("无法访问系统文件服务。");

        await using var stream = resolver.OpenInputStream(uri)
            ?? throw new IOException("无法读取所选备份文件。");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}
