using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using TRViS.IO;
using TRViS.IO.Models;

namespace TRViS.ViewModels;

public partial class AppViewModel : ObservableObject
{
	[ObservableProperty]
	ILoader? _Loader;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.WorkGroup>? _WorkGroupList;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.Work>? _WorkList;

	[ObservableProperty]
	IReadOnlyList<TRViS.IO.Models.DB.TrainData>? _DBTrainDataList;

	[ObservableProperty]
	TRViS.IO.Models.DB.WorkGroup? _SelectedWorkGroup;
	[ObservableProperty]
	TRViS.IO.Models.DB.Work? _SelectedWork;
	[ObservableProperty]
	TRViS.IO.Models.DB.TrainData? _SelectedDBTrainData;

	[ObservableProperty]
	TrainData? _SelectedTrainData;

	partial void OnLoaderChanged(ILoader? value)
	{
		SelectedWorkGroup = null;
		WorkGroupList = value?.GetWorkGroupList();
		SelectedWorkGroup = WorkGroupList?.FirstOrDefault();
	}

	partial void OnSelectedWorkGroupChanged(TRViS.IO.Models.DB.WorkGroup? value)
	{
		WorkList = null;
		SelectedWork = null;

		if (value is not null)
		{
			WorkList = Loader?.GetWorkList(value.Id);

			SelectedWork = WorkList?.FirstOrDefault();
		}
	}

	partial void OnSelectedWorkChanged(IO.Models.DB.Work? value)
	{
		DBTrainDataList = null;
		SelectedDBTrainData = null;

		if (value is not null)
		{
			DBTrainDataList = Loader?.GetTrainDataList(value.Id);
			SelectedDBTrainData = DBTrainDataList?.FirstOrDefault();
		}
	}

	partial void OnSelectedDBTrainDataChanged(IO.Models.DB.TrainData? value)
		=> SelectedTrainData = value is null ? null : Loader?.GetTrainData(value.Id);
}
