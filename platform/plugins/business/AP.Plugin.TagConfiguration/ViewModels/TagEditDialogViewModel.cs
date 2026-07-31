#region

using AP.Contracts.Hardware.DeviceRuntime;
using AP.Plugin.TagConfiguration.Models;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

#endregion

namespace AP.Plugin.TagConfiguration.ViewModels;

/// <summary>
/// 设备下拉选项。
/// </summary>
public sealed record DeviceOption(string DeviceId, string Display);

/// <summary>
/// 点编辑窗口 ViewModel（新增/编辑共用一个窗口）。
/// 确定时做必填/重名/周期格式本地校验，并调用 <see cref="ITagTableValidator"/>
/// 对候选点表做全量预检（与启动加载同一规则），有错留在窗内提示。
/// </summary>
public partial class TagEditDialogViewModel : ViewModelBase
{
    private readonly ITagTableValidator _tagTableValidator;

    private IReadOnlyList<TagEditRow> _existingRows = [];
    private string? _originalName;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private DeviceOption? _selectedDevice;

    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private TagDataType _selectedDataType = TagDataType.Int16;

    [ObservableProperty]
    private TagAccess _selectedAccess = TagAccess.ReadWrite;

    [ObservableProperty]
    private string _intervalOverrideText = string.Empty;

    [ObservableProperty]
    private string _group = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public TagEditDialogViewModel(
        IDeviceRegistry deviceRegistry,
        ITagTableValidator tagTableValidator)
    {
        _tagTableValidator = tagTableValidator;

        Devices = deviceRegistry.Devices
            .Select(d => new DeviceOption(d.Info.DeviceId, $"{d.Info.DeviceId}（{d.Info.DriverType}）"))
            .ToList();
    }

    public IReadOnlyList<DeviceOption> Devices { get; }

    public TagDataType[] DataTypes { get; } = Enum.GetValues<TagDataType>();

    public TagAccess[] Accesses { get; } = Enum.GetValues<TagAccess>();

    public bool IsSaved { get; private set; }

    /// <summary>保存成功后的编辑结果。</summary>
    public TagEditRow? ResultRow { get; private set; }

    public void InitializeForCreate(IReadOnlyList<TagEditRow> existingRows)
    {
        IsSaved = false;
        ResultRow = null;
        _existingRows = existingRows;
        _originalName = null;

        Title = "新增点";
        Name = string.Empty;
        SelectedDevice = Devices.FirstOrDefault();
        Address = string.Empty;
        SelectedDataType = TagDataType.Int16;
        SelectedAccess = TagAccess.ReadWrite;
        IntervalOverrideText = string.Empty;
        Group = string.Empty;
        Unit = string.Empty;
        Description = string.Empty;
        ErrorMessage = string.Empty;
    }

    public void InitializeForEdit(TagEditRow row, IReadOnlyList<TagEditRow> existingRows)
    {
        IsSaved = false;
        ResultRow = null;
        _existingRows = existingRows;
        _originalName = row.Name;

        Title = $"编辑点：{row.Name}";
        Name = row.Name;
        SelectedDevice = Devices.FirstOrDefault(d =>
            string.Equals(d.DeviceId, row.DeviceId, StringComparison.OrdinalIgnoreCase)) ?? Devices.FirstOrDefault();
        Address = row.Address;
        SelectedDataType = row.DataType;
        SelectedAccess = row.Access;
        IntervalOverrideText = row.IntervalOverrideMs?.ToString() ?? string.Empty;
        Group = row.Group ?? string.Empty;
        Unit = row.Unit ?? string.Empty;
        Description = row.Description ?? string.Empty;
        ErrorMessage = string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "点名不能为空";
            return;
        }

        if (SelectedDevice == null)
        {
            ErrorMessage = "请选择设备";
            return;
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            ErrorMessage = "地址不能为空";
            return;
        }

        int? intervalOverride = null;
        if (!string.IsNullOrWhiteSpace(IntervalOverrideText))
        {
            if (!int.TryParse(IntervalOverrideText.Trim(), out var interval) || interval <= 0)
            {
                ErrorMessage = "采集周期必须为正整数（毫秒），留空表示跟随默认";
                return;
            }
            intervalOverride = interval;
        }

        var trimmedName = Name.Trim();
        var isDuplicate = _existingRows.Any(r =>
            !string.Equals(r.Name, _originalName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
        if (isDuplicate)
        {
            ErrorMessage = $"点名 '{trimmedName}' 已存在";
            return;
        }

        var row = new TagEditRow
        {
            Name = trimmedName,
            DeviceId = SelectedDevice.DeviceId,
            Address = Address,
            DataType = SelectedDataType,
            Access = SelectedAccess,
            IntervalOverrideMs = intervalOverride,
            Group = Group,
            Unit = Unit,
            Description = Description
        };

        // 全量预检：候选点表 = 既有行（编辑时替换原行）+ 本行，与启动加载同一套校验规则
        var candidate = _existingRows
            .Where(r => !string.Equals(r.Name, _originalName, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ToDefinition())
            .Append(row.ToDefinition())
            .ToList();
        var errors = _tagTableValidator.Validate(candidate);
        if (errors.Count > 0)
        {
            ErrorMessage = string.Join(Environment.NewLine, errors);
            return;
        }

        IsSaved = true;
        ResultRow = row;
        OnRequestClose();
    }

    [RelayCommand]
    private void Cancel()
    {
        IsSaved = false;
        OnRequestClose();
    }
}
