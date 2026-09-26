namespace SciToolbox.ViewModels;

/// <summary>
/// Segoe Fluent / MDL2 字形常量。
/// 用码点构造而不是在源码里直接放私有区字符：后者在编辑器中显示为空白，容易被误删。
/// </summary>
public static class UiIcon
{
    public static string Of(int code) => char.ConvertFromUtf32(code);

    public const int Home = 0xE80F;
    public const int Star = 0xE734;
    public const int StarFill = 0xE735;
    public const int History = 0xE823;
    public const int Library = 0xE8A5;
    public const int Settings = 0xE713;
    public const int Search = 0xE721;
    public const int Chevron = 0xE70D;
    public const int Back = 0xE72B;
    public const int GlobalNav = 0xE700;
    public const int ChevronLeft = 0xE76B;
    public const int ChevronRight = 0xE76C;
    public const int Offline = 0xE8CD;
    public const int Clock = 0xE823;
    public const int Cache = 0xE895;
    public const int Warning = 0xE7BA;
    public const int Error = 0xE783;
    public const int Refresh = 0xE72C;
    public const int Close = 0xE711;
    public const int Add = 0xE710;
    public const int Compare = 0xE8A4;
    public const int Copy = 0xE8C8;
    public const int Note = 0xE70F;
    public const int Trash = 0xE74D;
    public const int Clear = 0xE894;
    public const int Filter = 0xE71C;
    public const int Link = 0xE71B;
    public const int OpenInNew = 0xE8A7;
    public const int Download = 0xE896;
    public const int More = 0xE712;
    public const int Collections = 0xE8B9;

    public static string StarGlyph(bool filled) => Of(filled ? StarFill : Star);
}
