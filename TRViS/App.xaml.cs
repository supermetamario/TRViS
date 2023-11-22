using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;

namespace TRViS;

public partial class App : Application
{
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

	public App()
	{
		logger.Trace("App Creating (URL: {0})", AppLinkUri?.ToString() ?? "(null))");

		InitializeComponent();

		MainPage = new AppShell();

		logger.Trace("App Created");
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		Window window = base.CreateWindow(activationState);

		logger.Info("Window Created");

		window.Destroying += WindowOnDestroying;

		return window;
	}

	private void WindowOnDestroying(object? sender, EventArgs e)
	{
		// Destroying => この時点で、削除されようとしているWindowはまだWindowsに含まれている
		logger.Info("Window Destroying... (Count: {0})", Windows.Count);

		if (sender is Window window)
			window.Destroying -= WindowOnDestroying;

		if (Windows.Count <= 1)
		{
			InstanceManager.Dispose();
			NLog.LogManager.Flush();
			NLog.LogManager.Shutdown();
		}
	}

	public static Uri? AppLinkUri { get; set; }

	public static void SetAppLinkUri(Uri uri)
	{
		logger.Info("AppLinkUri: {0}", uri);

		if (Current is not App app)
		{
			logger.Warn("App.Current is not App");
			AppLinkUri = uri;
			return;
		}

		if (app.MainPage is null)
		{
			logger.Warn("App.Current.MainPage is null");
			AppLinkUri = uri;
			return;
		}

		HandleAppLinkUriAsync(uri);
	}

	protected override void OnAppLinkRequestReceived(Uri uri)
	{
		base.OnAppLinkRequestReceived(uri);

		logger.Info("AppLinkUri: {0}", uri);

		HandleAppLinkUriAsync(uri);
	}

	protected override void OnStart()
	{
		logger.Info("App Start");
		base.OnStart();

		if (AppLinkUri is not null)
		{
			logger.Info("AppLinkUri is not null: {0}", AppLinkUri);
			HandleAppLinkUriAsync(AppLinkUri);
		}
	}

	static Task HandleAppLinkUriAsync(Uri uri)
		=> HandleAppLinkUriAsync(uri, CancellationToken.None);
	static Task HandleAppLinkUriAsync(Uri uri, CancellationToken cancellationToken)
	{
		return InstanceManager.AppViewModel.HandleAppLinkUriAsync(uri, cancellationToken).ContinueWith(t =>
		{
			AppLinkUri = null;
			if (t.IsFaulted)
			{
				logger.Error(t.Exception, "HandleAppLinkUriAsync Failed");
				Crashes.TrackError(t.Exception);
			}
		}, cancellationToken);
	}
}
