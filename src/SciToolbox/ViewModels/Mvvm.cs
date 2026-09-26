using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace SciToolbox.ViewModels;

/// <summary>MVVM 基类（INotifyPropertyChanged）。</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    /// <summary>
    /// 通知所有绑定重新取值。语言 / 主题切换后必须调用：
    /// 替换 Content 对象并不会让已建立的绑定重新求值，因此需要显式通知全部属性。
    /// </summary>
    public void RaiseAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

/// <summary>通用命令（ICommand）。</summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = _ => execute();
        if (canExecute != null) _canExecute = _ => canExecute();
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    // 命令内部异常统一兜底并提示，保证失败对用户可见。
    public void Execute(object? parameter)
    {
        try { _execute(parameter); }
        catch (Exception ex) { CommandError.Report(ex); }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    public void RaiseCanExecute() => CommandManager.InvalidateRequerySuggested();
}

/// <summary>带参数命令。</summary>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) =>
        _canExecute?.Invoke(parameter is T t ? t : default) ?? true;

    public void Execute(object? parameter)
    {
        try { _execute(parameter is T t ? t : default); }
        catch (Exception ex) { CommandError.Report(ex); }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}

/// <summary>异步命令（防重入）。</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = _ => execute();
        if (canExecute != null) _canExecute = _ => canExecute();
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (_isRunning) return;
        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try { await _execute(parameter); }
        catch (Exception ex) { CommandError.Report(ex); }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}

/// <summary>命令异常兜底：写崩溃日志并向用户提示。</summary>
internal static class CommandError
{
    public static void Report(Exception ex)
    {
        SciToolbox.Core.Log.Write("Command", ex);
        SciToolbox.Core.Toast.Show(ex.Message);
    }
}
