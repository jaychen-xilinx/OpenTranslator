using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenTranslator.Views;

public sealed class TestWindow : Window
{
    public TestWindow()
    {
        Title = "Test - ContentDialog";

        var grid = new Grid
        {
            Background = new SolidColorBrush(Colors.White)
        };

        var button = new Button
        {
            Content = "Show Dialog",
            Width = 150,
            Height = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Click += async (s, e) =>
        {
            var dialog = new ContentDialog
            {
                Title = "Test Dialog",
                Content = "This is a test dialog",
                CloseButtonText = "Close"
            };
            dialog.XamlRoot = grid.XamlRoot;
            await dialog.ShowAsync();
        };

        grid.Children.Add(button);
        Content = grid;
    }
}
