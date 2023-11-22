using System.Collections.Specialized;
using System.Web;

using TRViS.IO;
using TRViS.ViewModels;

namespace TRViS.RootPages;

public partial class SelectTrainPage : ContentPage
{
	public static readonly string NameOfThisClass = nameof(SelectTrainPage);
	private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
	AppViewModel viewModel { get; }

	public SelectTrainPage()
	{
		logger.Trace("Creating (AppViewModel: {0})", viewModel);

		InitializeComponent();

		this.viewModel = InstanceManager.AppViewModel;
		this.BindingContext = viewModel;

		logger.Trace("Created");
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (App.AppLinkUri is not null)
		{
			logger.Info("AppLinkUri is not null: {0}", App.AppLinkUri);
			CancellationTokenSource cts = new();
			await HandleAppLinkUriAsync(App.AppLinkUri, cts.Token);
			App.AppLinkUri = null;
		}
		else
		{
			logger.Info("AppLinkUri is null");
		}
		viewModel.Loader ??= new SampleDataLoader();
	}

	const string OPEN_FILE_JSON = "/open/json";
	const string OPEN_FILE_SQLITE = "/open/sqlite";
	async Task HandleAppLinkUriAsync(Uri uri, CancellationToken token)
	{
		if (uri.Host != "app")
		{
			logger.Warn("Uri.Host is not `app`: {0}", uri.Host);
			return;
		}
		// if (uri.LocalPath != OPEN_FILE_JSON && uri.LocalPath != OPEN_FILE_SQLITE)
		if (uri.LocalPath != OPEN_FILE_JSON)
		{
			logger.Warn("Uri.LocalPath is not valid: {0}", uri.LocalPath);
			return;
		}
		if (string.IsNullOrEmpty(uri.Query))
		{
			logger.Warn("Uri.Query is null or empty");
			return;
		}
		NameValueCollection queryParams = HttpUtility.ParseQueryString(uri.Query);
		string? path = queryParams["path"];
		if (string.IsNullOrEmpty(path))
		{
			logger.Warn("Uri.Query is not valid (query[`path`] not found): {0}", uri.Query);
			return;
		}
		if (!path.StartsWith("https://"))
		{
			logger.Warn("path is not valid (not HTTPS): {0}", path);
			return;
		}

		bool openFile = await Utils.DisplayAlert(this, "外部ファイルを開く", $"ファイル `{path}` を開きますか?", "はい", "いいえ");
		logger.Info("Uri: {0} -> openFile: {1}", path, openFile);
		if (!openFile)
		{
			return;
		}

		await LoadExternalFileAsync(path, uri.LocalPath, token);
	}
	async Task LoadExternalFileAsync(string path, string fileType, CancellationToken token)
	{
		try
		{
			logger.Info("checking file size and type...");
			using HttpRequestMessage request = new(HttpMethod.Head, path);
			using HttpResponseMessage checkResult = await InstanceManager.HttpClient.SendAsync(request, token);
			if (!checkResult.IsSuccessStatusCode)
			{
				logger.Warn("File Size Check Failed with status code: {0} ({1})", checkResult.StatusCode, checkResult.Content);
				await Utils.DisplayAlert(this, "Cannot Open File", $"File Size Check Failed: {checkResult.StatusCode}\n{checkResult.Content}", "OK");
				return;
			}

#if DEBUG
			// パフォーマンスとプライバシーの理由で、ヘッダーの内容はDEBUGビルドのみ表示する
			IEnumerable<string> headerStrs = checkResult.Content.Headers.Select(x => $"{x.Key}: {string.Join(", ", x.Value)}");
			logger.Trace("ResponseHeaders: {0}", string.Join(", ", headerStrs));
#endif

			if (checkResult.Content.Headers.ContentLength is not long contentLength)
			{
				logger.Warn("File Size Check Failed (Content-Length not set) -> check continue or not");
				bool downloadContinue = await Utils.DisplayAlert(this, "Continue to download?", "ダウンロードするファイルのサイズが不明です。ダウンロードを継続しますか?", "続ける", "やめる");
				if (!downloadContinue)
				{
					logger.Info("User canceled");
					return;
				}
			}
			else
			{
				logger.Info("File Size Check Succeeded: {0} bytes", contentLength);
				bool downloadContinue = await Utils.DisplayAlert(this, "Continue to download?", $"ダウンロードするファイルのサイズは {contentLength} byte です。このファイルをダウンロードしますか?", "続ける", "やめる");
				if (!downloadContinue)
				{
					logger.Info("User canceled");
					return;
				}
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "File Size Check Failed");
			await Utils.DisplayAlert(this, "Cannot Open File", ex.ToString(), "OK");
			return;
		}

		try
		{
			using Stream stream = await InstanceManager.HttpClient.GetStreamAsync(path, token);

			switch (fileType)
			{
				case OPEN_FILE_JSON:
					logger.Debug("Loading JSON File");
					viewModel.Loader = await LoaderJson.InitFromStreamAsync(stream, token);
					logger.Trace("LoaderJson Initialized");
					break;
				case OPEN_FILE_SQLITE:
					logger.Debug("Loading SQLite File");
					// 一旦ローカルに保存してから読み込む
					logger.Error("Not Implemented");
					await Utils.DisplayAlert(this, "Not Implemented", "Open External SQLite file is Not Implemented", "OK");
					logger.Trace("LoaderSQL Initialized");
					return;
				default:
					logger.Warn("Uri.LocalPath is not valid: {0}", fileType);
					return;
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "Loading File Failed");
			await Utils.DisplayAlert(this, "Cannot Open File", ex.ToString(), "OK");
			return;
		}

		await Utils.DisplayAlert(this, "Success!", "外部ファイルの読み込みが完了しました", "OK");
	}

	async void Button_Clicked(object sender, EventArgs e)
	{
		logger.Info("Select File Button Clicked");

		try
		{
			var result = await FilePicker.Default.PickAsync();

			if (result is not null)
			{
				logger.Info("File Selected: {0}", result.FullPath);
				viewModel.Loader?.Dispose();

				if (result.FullPath.EndsWith(".json"))
				{
					logger.Debug("Loading JSON File");
					viewModel.Loader = await LoaderJson.InitFromFileAsync(result.FullPath);
					logger.Trace("LoaderJson Initialized");
				}
				else
				{
					logger.Debug("Loading SQLite File");
					viewModel.Loader = new LoaderSQL(result.FullPath);
					logger.Trace("LoaderSQL Initialized");
				}
			}
			else
			{
				logger.Info("File Selection Canceled (PickAsync result is null))");
			}
		}
		catch (Exception ex)
		{
			logger.Error(ex, "File Selection Failed");
			await Utils.DisplayAlert(this, "Cannot Open File", ex.ToString(), "OK");
		}

		logger.Info("Select File Button Clicked Processing Complete");
	}
}
