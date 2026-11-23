using System.Text;
using CommunityToolkit.Maui.Storage;

namespace Ruqi.App.Services;

/// <summary>
/// 跨平台（Windows / iOS / macOS）备份文件导出与读取实现。
/// </summary>
public sealed class CrossPlatformBackupFilePicker : IBackupFilePicker
{
    public async Task<string?> ExportJsonAsync(string suggestedFileName, string jsonContent, CancellationToken ct = default)
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(jsonContent);
        await using var stream = new MemoryStream(bytes);
        var result = await FileSaver.Default.SaveAsync(suggestedFileName, stream, ct);
        if (!result.IsSuccessful)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(result.FilePath) ? suggestedFileName : result.FilePath;
    }

    public async Task<string?> PickAndReadJsonAsync(CancellationToken ct = default)
    {
        var jsonFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            { DevicePlatform.WinUI, [".json"] },
            { DevicePlatform.macOS, ["public.json", "json"] },
            { DevicePlatform.MacCatalyst, ["public.json", "json"] },
            { DevicePlatform.iOS, ["public.json"] },
            { DevicePlatform.Android, ["application/json", "text/plain", "*/*"] }
        });

        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "选择「如期」JSON 备份文件",
            FileTypes = jsonFileType
        });

        if (picked is null)
        {
            return null;
        }

        await using var stream = await picked.OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }
}
