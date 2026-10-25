using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AquaRappel.Helpers;

/// <summary>Base des ViewModels : notifie la vue quand une propriété change.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action<T?> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute(parameter is T t ? t : default);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _running;
    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<Task> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => !_running;

    public async void Execute(object? parameter)
    {
        if (_running) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await _execute(); }
        catch (Exception ex) { Debug.WriteLine(ex); }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

public class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<T?, Task> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;

    public async void Execute(object? parameter)
    {
        try { await _execute(parameter is T t ? t : default); }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }
}

public static class Format
{
    /// <summary>7.5 -> "7 h 30"</summary>
    public static string Hm(double hours)
    {
        int m = (int)Math.Round(hours * 60);
        return $"{m / 60} h {m % 60:00}";
    }
}
