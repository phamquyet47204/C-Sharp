namespace VinhKhanh.Mobile.Views;

public partial class SpeechSettingsPage : ContentPage
{
    public SpeechSettingsPage()
    {
        InitializeComponent();
        LoadSavedValues();
    }

    private void LoadSavedValues()
    {
        var pitch = Preferences.Get("tts_pitch", 1.0f);
        var rate = Preferences.Get("tts_rate", 1.0f);
        PitchSlider.Value = Math.Clamp(pitch, 0.5, 2.0);
        RateSlider.Value = Math.Clamp(rate, 0.5, 2.0);
        PitchValueLabel.Text = PitchSlider.Value.ToString("F1");
        RateValueLabel.Text = RateSlider.Value.ToString("F1");
    }

    private void OnPitchChanged(object sender, ValueChangedEventArgs e)
    {
        var rounded = (float)Math.Round(e.NewValue, 1);
        PitchValueLabel.Text = rounded.ToString("F1");
        Preferences.Set("tts_pitch", rounded);
    }

    private void OnRateChanged(object sender, ValueChangedEventArgs e)
    {
        var rounded = (float)Math.Round(e.NewValue, 1);
        RateValueLabel.Text = rounded.ToString("F1");
        Preferences.Set("tts_rate", rounded);
    }

    private void OnResetClicked(object sender, EventArgs e)
    {
        PitchSlider.Value = 1.0;
        RateSlider.Value = 1.0;
        Preferences.Set("tts_pitch", 1.0f);
        Preferences.Set("tts_rate", 1.0f);
    }
}
