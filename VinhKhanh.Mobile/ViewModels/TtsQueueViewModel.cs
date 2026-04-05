using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VinhKhanh.Mobile.Models;
using VinhKhanh.Mobile.Services;

namespace VinhKhanh.Mobile.ViewModels;

public partial class TtsQueueViewModel : ObservableObject
{
    private readonly NarrationEngine _engine;

    [ObservableProperty]
    private ObservableCollection<PoiRecord> _queueItems = new();

    [ObservableProperty]
    private string _currentlyPlayingName = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isPanelVisible;

    public TtsQueueViewModel(NarrationEngine engine)
    {
        _engine = engine;
        _engine.QueueChanged += OnQueueChanged;
        RefreshQueue();
    }

    private void OnQueueChanged()
    {
        MainThread.BeginInvokeOnMainThread(RefreshQueue);
    }

    private void RefreshQueue()
    {
        QueueItems = new ObservableCollection<PoiRecord>(_engine.CurrentQueue);
        CurrentlyPlayingName = _engine.CurrentlyPlaying?.Name ?? string.Empty;
        IsPlaying = _engine.IsPlaying;
        IsPanelVisible = _engine.IsPlaying || _engine.CurrentQueue.Count > 0;
    }

    [RelayCommand]
    private async Task SkipAsync() => await _engine.SkipCurrentAsync();

    [RelayCommand]
    private async Task ClearQueueAsync() => await _engine.ClearQueueAsync();
}
