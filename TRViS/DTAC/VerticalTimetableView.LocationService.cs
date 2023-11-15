using DependencyPropertyGenerator;

using TRViS.IO.Models;
using TRViS.Services;

namespace TRViS.DTAC;

[DependencyProperty<bool>("IsRunStarted")]
[DependencyProperty<bool>("IsLocationServiceEnabled")]
public partial class VerticalTimetableView : Grid
{
	LocationService LocationService { get; } = new();

	public event EventHandler<ValueChangedEventArgs<bool>>? IsLocationServiceEnabledChanged;
	partial void OnIsLocationServiceEnabledChanged(bool newValue)
	{
		logger.Info("IsLocationServiceEnabled is changed to {0}", newValue);

		LocationService.IsEnabled = newValue;
		IsLocationServiceEnabledChanged?.Invoke(this, new(!newValue, newValue));
	}

	private void LocationService_LocationStateChanged(object? sender, LocationStateChangedEventArgs e)
	{
		if (!IsLocationServiceEnabled)
		{
			logger.Debug("IsLocationServiceEnabled is false -> Do nothing");
			return;
		}
		if (e.NewStationIndex < 0)
		{
			logger.Warn("e.NewStationIndex is less than 0 -> Disable LocationService");
			IsLocationServiceEnabled = false;
			return;
		}
		if (RowViewList.Count <= e.NewStationIndex)
		{
			logger.Warn("RowViewList.Count is less than e.NewStationIndex -> Disable LocationService");
			IsLocationServiceEnabled = false;
			return;
		}

		logger.Info("LocationStateChanged: [{0}](State:{1}) Rows[{2}](IsRunningToNextStation: {3})",
			CurrentRunningRowIndex,
			CurrentRunningRow?.LocationState,
			e.NewStationIndex,
			e.IsRunningToNextStation
		);
		if (CurrentRunningRow is not null)
			CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;
		UpdateCurrentRunningLocationVisualizer(RowViewList[e.NewStationIndex], e.IsRunningToNextStation
			? VerticalTimetableRow.LocationStates.RunningToNextStation
			: VerticalTimetableRow.LocationStates.AroundThisStation
		);
		CurrentRunningRow = RowViewList[e.NewStationIndex];
		CurrentRunningRowIndex = e.NewStationIndex;
	}

	public void SetCurrentRunningRow(int index)
		=> SetCurrentRunningRow(index, RowViewList.ElementAtOrDefault(index));

	public void SetCurrentRunningRow(VerticalTimetableRow? value)
		=> SetCurrentRunningRow(value is null ? -1 : RowViewList.IndexOf(value), value);

	void SetCurrentRunningRow(int index, VerticalTimetableRow? value)
	{
		if (CurrentRunningRowIndex == index || CurrentRunningRow == value)
		{
			logger.Trace("CurrentRunningRowIndex is already {0} or CurrentRunningRow is already {1}, so skipping...", index, value?.RowIndex);
			return;
		}

		if (RowViewList.ElementAtOrDefault(index) != value)
			throw new ArgumentException("value is not match with element at given index", nameof(value));

		MainThread.BeginInvokeOnMainThread(() =>
		{
			if (_CurrentRunningRow is not null)
			{
				logger.Debug("CurrentRunningRow[{0}: {1}] is not null -> set LocationState to Undefined",
					_CurrentRunningRow.RowIndex,
					_CurrentRunningRow.RowData.StationName
				);

				_CurrentRunningRow.LocationState = VerticalTimetableRow.LocationStates.Undefined;
			}

			logger.Info("CurrentRunningRow is changed from {0}: `{1}` to {2}: `{3}`",
				CurrentRunningRow?.RowIndex,
				CurrentRunningRow?.RowData.StationName,
				index,
				value?.RowData.StationName
			);
			_CurrentRunningRow = value;

			if (value is not null)
			{
				CurrentRunningRowIndex = index;
				UpdateCurrentRunningLocationVisualizer(value, VerticalTimetableRow.LocationStates.AroundThisStation);

				if (value.LocationState != VerticalTimetableRow.LocationStates.Undefined)
				{
					logger.Debug("value.LocationState is not Undefined -> invoke ScrollRequested");
					ScrollRequested?.Invoke(this, new(Math.Max(value.RowIndex - 1, 0) * RowHeight.Value));
				}
				else
				{
					logger.Debug("value.LocationState is Undefined -> do nothing");
				}
			}
			else
			{
				logger.Debug("value is null -> set CurrentRunningRowIndex to -1");
				CurrentRunningRowIndex = -1;
			}

			NextRunningRow = RowViewList.ElementAtOrDefault(index + 1);
			logger.Debug("NextRunningRow is set to {0}: `{1}`",
				NextRunningRow?.RowIndex,
				NextRunningRow?.RowData.StationName
			);
		});
	}

	void UpdateCurrentRunningLocationVisualizer(VerticalTimetableRow row, VerticalTimetableRow.LocationStates states)
	{
		logger.Info("UpdateCurrentRunningLocationVisualizer: {0} ... {1}", row.RowIndex, states);
		row.LocationState = states;

		int rowCount = row.RowIndex;

		Grid.SetRow(CurrentLocationBoxView, rowCount);
		Grid.SetRow(CurrentLocationLine, rowCount);

		CurrentLocationBoxView.IsVisible = row.LocationState
			is VerticalTimetableRow.LocationStates.AroundThisStation
			or VerticalTimetableRow.LocationStates.RunningToNextStation;
		CurrentLocationLine.IsVisible = row.LocationState is VerticalTimetableRow.LocationStates.RunningToNextStation;

		CurrentLocationBoxView.Margin = row.LocationState
			is VerticalTimetableRow.LocationStates.RunningToNextStation
			? new(0, -(RowHeight.Value / 2)) : new(0);
	}
}
