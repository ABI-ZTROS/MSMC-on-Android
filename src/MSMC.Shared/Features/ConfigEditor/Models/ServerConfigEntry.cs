// -----------------------------------------------------------------------------
// 文件名: ServerConfigEntry.cs
// 命名空间: io.NET.ZTR_OS.Features.ConfigEditor.Models
// 功能描述: 服务器配置条目数据契约，封装单条配置的键值与元数据
// 依赖组件: CommunityToolkit.Mvvm, ServerConfigDescriptor
// 设计模式: 贫血模型 + ObservableObject 属性变更通知
// -----------------------------------------------------------------------------
using CommunityToolkit.Mvvm.ComponentModel;
using SvcDescriptor = io.NET.ZTR_OS.Features.ConfigEditor.Services.ServerConfigDescriptor;

namespace io.NET.ZTR_OS.Features.ConfigEditor.Models;

/// <summary>
/// 服务器配置条目数据契约，封装单条配置项的键值对、来源及校验状态。
/// 作为配置编辑器的 ViewModel，支持属性变更通知以驱动 UI 绑定更新。
/// </summary>
public partial class ServerConfigEntry : ObservableObject
{
    /// <summary>
    /// 配置项的键名。
    /// 对应配置文件中的属性名，如 server-port、motd 等。
    /// </summary>
    [ObservableProperty] private string _key = string.Empty;

    /// <summary>
    /// 配置项的当前值。
    /// 用户编辑后的实时值。
    /// </summary>
    [ObservableProperty] private string _value = string.Empty;

    /// <summary>
    /// 配置项所属源文件的路径。
    /// 标识该配置来自 server.properties 或其他配置文件。
    /// </summary>
    [ObservableProperty] private string _sourceFile = string.Empty;

    /// <summary>
    /// 指示该配置项是否已被修改。
    /// 当 Value 与 OriginalValue 不一致时为 true。
    /// </summary>
    [ObservableProperty] private bool _isModified;

    /// <summary>
    /// 配置项的原始值。
    /// 加载配置时的初始值，用于对比变更与撤销操作。
    /// </summary>
    [ObservableProperty] private string _originalValue = string.Empty;

    /// <summary>
    /// 指示当前值是否通过校验。
    /// 校验失败时 ErrorMessage 包含具体错误描述。
    /// </summary>
    [ObservableProperty] private bool _isValid = true;

    /// <summary>
    /// 校验失败时的错误信息。
    /// 校验通过时为 null。
    /// </summary>
    [ObservableProperty] private string? _errorMessage;

    /// <summary>
    /// 配置项的描述符引用。
    /// 包含配置项的元数据定义，如显示名称、数据类型、取值范围等。
    /// 无对应描述符时为 null。
    /// </summary>
    public SvcDescriptor? Descriptor { get; set; }

    /// <summary>
    /// 配置项分类（覆盖 Descriptor.Category）。
    /// 用例：
    ///   - 普通条目：null 时回退 Descriptor.Category → "其他"
    ///   - 错误条目："__ERROR__" 单独分组，前端渲染为 Alert 横幅
    ///   - 无 Descriptor 条目：按此字段明确分类
    /// </summary>
    [ObservableProperty] private string? _category;

    /// <summary>
    /// 显示名称覆盖值。
    /// 当 Descriptor 没有元数据但又需要强制显示文案（例如 "__ERROR__" 条目）时，
    /// 通过此字段设置友好名；非空时 DisplayName 直接取本字段，不再走 Descriptor 回退链。
    /// </summary>
    [ObservableProperty] private string? _displayNameOverride;

    /// <summary>
    /// 配置项的显示名称。
    /// 优先级：DisplayNameOverride（显式覆盖）→ Descriptor.DisplayName（元数据）→ Key（原始键名）。
    /// </summary>
    public string DisplayName => DisplayNameOverride ?? Descriptor?.DisplayName ?? Key;

    /// <summary>
    /// 配置项的友好显示名称。
    /// 优先使用描述符中的本地化名称；无描述符时将 kebab-case 或 snake_case 的键名转换为 Title Case。
    /// 例如 "network-compression-threshold" 转换为 "Network Compression Threshold"。
    /// </summary>
    public string FriendlyDisplayName
    {
        get
        {
            if (Descriptor is not null)
                return Descriptor.DisplayName;

            if (string.IsNullOrEmpty(Key))
                return "(空)";

            var words = Key.Split('-', '.', '_');
            return string.Join(' ', words.Select(w =>
                string.IsNullOrEmpty(w) ? w :
                char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
        }
    }

    /// <summary>
    /// 配置项的描述文本。
    /// 优先使用描述符中的说明，无描述符时返回空字符串。
    /// </summary>
    public string Description => Descriptor?.Description ?? string.Empty;

    /// <summary>
    /// 指示修改该配置项是否需要重启服务器才能生效。
    /// 基于描述符的 RequiresRestart 属性安全访问。
    /// </summary>
    public bool RequiresRestart => Descriptor?.RequiresRestart == true;

    /// <summary>
    /// 指示配置项是否为布尔类型。
    /// 用于 UI 控制切换按钮的显隐。
    /// </summary>
    public bool IsBoolType => Descriptor?.ValueType.Equals("bool", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// 指示配置项是否为枚举类型。
    /// 用于 UI 控制下拉选择框的显隐。
    /// </summary>
    public bool IsEnumType => Descriptor?.AllowedValues?.Length > 0;

    /// <summary>
    /// 指示配置项是否为数值类型。
    /// 支持 int、float、double、long 四种数值类型。
    /// 用于 UI 控制数值输入框的显隐。
    /// </summary>
    public bool IsNumericType => Descriptor != null && 
        (Descriptor.ValueType.Equals("int", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("float", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("double", StringComparison.OrdinalIgnoreCase) ||
         Descriptor.ValueType.Equals("long", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// 指示配置项是否为普通字符串类型。
    /// 即非布尔、非枚举、非数值的配置项，使用文本框编辑。
    /// </summary>
    public bool IsStringType => !IsBoolType && !IsEnumType && !IsNumericType;
}
