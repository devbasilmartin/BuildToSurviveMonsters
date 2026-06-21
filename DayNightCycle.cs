using System;

public enum DayPhase { Day, Warning, Night }

public class DayNightCycle
{
    public float DayDuration     = 35f;
    public float NightDuration   = 120f;
    public float WarningLeadTime = 5f;

    public DayPhase Phase      { get; private set; } = DayPhase.Day;
    public int      NightCount { get; private set; } = 0;
    public float    PhaseT     { get; private set; } // 0..1 within current phase

    float _timer;
    bool  _fastForward;

    public event Action? OnDayStart;
    public event Action? OnNightWarning;
    public event Action? OnNightStart;

    public DayNightCycle() => StartDay();

    public void SetFastForward(bool on) => _fastForward = on;

    public void Update(float dt)
    {
        _timer += dt * (_fastForward ? 10f : 1f);

        switch (Phase)
        {
            case DayPhase.Day:
                PhaseT = _timer / DayDuration;
                if (_timer >= DayDuration - WarningLeadTime) EnterWarning();
                break;
            case DayPhase.Warning:
                if (_timer >= DayDuration) StartNight();
                break;
            case DayPhase.Night:
                PhaseT = _timer / NightDuration;
                if (_timer >= NightDuration) StartDay();
                break;
        }
    }

    public float DayRemaining   => Math.Max(0f, DayDuration   - _timer);
    public float NightRemaining => Math.Max(0f, NightDuration - _timer);

    void StartDay()   { Phase = DayPhase.Day;     _timer = 0; OnDayStart?.Invoke(); }
    void EnterWarning(){ Phase = DayPhase.Warning;             OnNightWarning?.Invoke(); }
    void StartNight() { Phase = DayPhase.Night;   _timer = 0; NightCount++; OnNightStart?.Invoke(); }
}
