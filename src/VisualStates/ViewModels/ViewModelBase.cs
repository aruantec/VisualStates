using CommunityToolkit.Mvvm.ComponentModel;

namespace VisualStates.ViewModels;

/// <summary>
/// Base type for all editor view-models; inherits <see cref="ObservableObject"/>
/// property-change support from CommunityToolkit.Mvvm.
/// </summary>
public abstract class ViewModelBase : ObservableObject;
