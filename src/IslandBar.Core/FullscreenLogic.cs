namespace IslandBar.Core;

public static class FullscreenLogic
{
    // Values of QUERY_USER_NOTIFICATION_STATE from shellapi.h
    public const int Busy = 2, RunningD3DFullScreen = 3, PresentationMode = 4;

    /// <summary>True when Windows says a fullscreen app or presentation is in front.</summary>
    public static bool IsFullscreen(int notificationState) =>
        notificationState is Busy or RunningD3DFullScreen or PresentationMode;
}
