using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisualStates.Controls;
using VisualStates.Services;
using VisualStates.ViewModels;

namespace VisualStates;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(MainViewModel viewModel, IWindowContext windowContext) : this()
    {
        DataContext = viewModel;
        windowContext.MainWindow = this;
        Title = viewModel.Title;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Title))
                Title = viewModel.Title;
        };
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.Handled || GraphCanvas.ViewModel is null)
            return;

        var position = e.GetPosition(GraphCanvas);
        GraphCanvas.ApplyZoomAt(position, GraphViewport.GetWheelZoomFactor(e.Delta));
        e.Handled = true;
    }

    private void OnViewAreaPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        GraphCanvas.HandlePointerPressed(e);
        if (GraphCanvas.IsViewPanning)
            e.Handled = true;
    }

    private void OnViewAreaPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        if (GraphCanvas.IsViewPanning)
        {
            GraphCanvas.HandlePointerMoved(e);
            e.Handled = true;
        }
    }

    private void OnViewAreaPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Handled || e.Source is GraphCanvas)
            return;

        if (GraphCanvas.IsViewPanning)
        {
            GraphCanvas.HandlePointerReleased(e);
            e.Handled = true;
        }
    }
}
