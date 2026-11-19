using System.Text;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Plugin.TagConfiguration.Models;

namespace AP.Plugin.TagConfiguration.Services;

/// <summary>
/// 点表 CSV 解析（批量导入）。支持英文/中文表头、引号字段、可选 IntervalMs。
/// 所有错误一次性返回，调用方在全量成功后才写入列表。
/// </summary>
public static class TagCsvParser
{
    public static TagCsvParseResult Parse(string csv)
    {
        var rows = new List<TagEditRow>();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(csv))
            return new TagCsvParseResult(rows, ["CSV 文件为空"]);

        var lines = csv.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var headerLine = lines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (headerLine == null)
            return new TagCsvParseResult(rows, ["CSV 文件为空"]);

        var headers = ParseLine(headerLine).Select(NormalizeHeader).ToList();
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < headers.Count; i++)
            index[headers[i]] = i;

        var nameIndex = FindHeader(index, "name", "点名");
        var deviceIndex = FindHeader(index, "deviceid", "device", "设备", "设备id");
        var addressIndex = FindHeader(index, "address", "地址");
        if (nameIndex < 0 || deviceIndex < 0 || addressIndex < 0)
        {
            errors.Add("CSV 表头必须包含 Name/DeviceId/Address（或 点名/设备/地址）");
            return new TagCsvParseResult(rows, errors);
        }

        var dataTypeIndex = FindHeader(index, "datatype", "type", "类型", "数据类型");
        var accessIndex = FindHeader(index, "access", "读写", "读写方向");
        var groupIndex = FindHeader(index, "group", "分组");
        var unitIndex = FindHeader(index, "unit", "单位");
        var descriptionIndex = FindHeader(index, "description", "desc", "描述");
        var intervalIndex = FindHeader(index, "intervalms", "interval", "采集周期", "周期");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var lineNo = 1; lineNo < lines.Length; lineNo++)
        {
            var line = lines[lineNo];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var fields = ParseLine(line);
            string Get(int i) => i >= 0 && i < fields.Count ? fields[i].Trim() : string.Empty;

            var name = Get(nameIndex);
            var deviceId = Get(deviceIndex);
            var address = Get(addressIndex);
            if (string.IsNullOrWhiteSpace(name))
                errors.Add($"第 {lineNo + 1} 行：点名不能为空");
            else if (!names.Add(name))
                errors.Add($"第 {lineNo + 1} 行：点名重复 '{name}'");

            if (string.IsNullOrWhiteSpace(deviceId))
                errors.Add($"第 {lineNo + 1} 行：设备 Id 不能为空");
            if (string.IsNullOrWhiteSpace(address))
                errors.Add($"第 {lineNo + 1} 行：地址不能为空");

            var dataType = TagDataType.Int16;
            var dataTypeText = Get(dataTypeIndex);
            if (!string.IsNullOrWhiteSpace(dataTypeText) &&
                !Enum.TryParse(dataTypeText, true, out dataType))
            {
                errors.Add($"第 {lineNo + 1} 行：未知数据类型 '{dataTypeText}'");
            }

            var access = TagAccess.ReadWrite;
            var accessText = Get(accessIndex);
            if (!string.IsNullOrWhiteSpace(accessText) &&
                !Enum.TryParse(accessText, true, out access))
            {
                errors.Add($"第 {lineNo + 1} 行：未知读写方向 '{accessText}'");
            }

            int? interval = null;
            var intervalText = Get(intervalIndex);
            if (!string.IsNullOrWhiteSpace(intervalText))
            {
                if (int.TryParse(intervalText, out var parsed) && parsed > 0)
                    interval = parsed;
                else
                    errors.Add($"第 {lineNo + 1} 行：采集周期必须是正整数（毫秒）");
            }

            if (errors.Count > 0) continue;

            rows.Add(new TagEditRow
            {
                Name = name,
                DeviceId = deviceId,
                Address = address,
                DataType = dataType,
                Access = access,
                Group = NullIfEmpty(Get(groupIndex)),
                Unit = NullIfEmpty(Get(unitIndex)),
                Description = NullIfEmpty(Get(descriptionIndex)),
                IntervalOverrideMs = interval
            });
        }

        if (rows.Count == 0 && errors.Count == 0)
            errors.Add("CSV 中没有数据行");

        return new TagCsvParseResult(errors.Count == 0 ? rows : new List<TagEditRow>(), errors);
    }

    private static string NormalizeHeader(string header)
        => header.Trim().TrimStart('\uFEFF').Replace(" ", string.Empty).ToLowerInvariant();

    private static int FindHeader(IReadOnlyDictionary<string, int> index, params string[] names)
    {
        foreach (var name in names)
            if (index.TryGetValue(name, out var i)) return i;
        return -1;
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>解析一行 CSV，支持双引号包裹、双引号转义和字段内逗号。</summary>
    public static IReadOnlyList<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    current.Append(ch);
                }
            }
            else if (ch == '"')
            {
                inQuotes = true;
            }
            else if (ch == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }
}

public sealed record TagCsvParseResult(IReadOnlyList<TagEditRow> Rows, IReadOnlyList<string> Errors)
{
    public bool Success => Errors.Count == 0;
}
