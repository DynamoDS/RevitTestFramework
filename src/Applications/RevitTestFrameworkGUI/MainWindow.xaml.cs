using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace RTF.Applications
{
    /// <summary>
    /// Interaction logic for View.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly RunnerViewModel vm;

        public MainWindow()
        {
            InitializeComponent();

            vm = new RunnerViewModel(new WpfContext());

            DataContext = vm;

            Closing += View_Closing;
            Loaded += MainWindow_Loaded;
        }

        void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // When running with a UI, we redirect the console output
            // to a text box in the application's interface.

            var outputter = new TextBoxOutputter(ConsoleTextBlock);
            Console.SetOut(outputter);
        }

        private void View_Closing(object sender, CancelEventArgs e)
        {
            vm.CleanupCommand.Execute();
        }

        private void TestDataTreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            vm.SelectedItem = e.NewValue;
        }

        private void UpdateRequired(object sender, RoutedEventArgs e)
        {
            vm.UpdateCommand.Execute();
        }

        private void ProductSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            vm.ChangeProductCommand.Execute(((ComboBox) sender).SelectedIndex);
        }

        private void Assembly_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetAssemblyPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }

        private void WorkingDir_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetWorkingPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private string GetFirstFileFromDropPackage(DragEventArgs e)
        {
            //http://stackoverflow.com/questions/5662509/drag-and-drop-files-into-wpf
            //http://stackoverflow.com/questions/4281857/wpf-drag-and-drop-to-a-textbox

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return string.Empty;
            }

            // Note that you can have more than one file.
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (!files.Any())
            {
                return string.Empty;
            }

            return files.First();
        }

        private void Results_OnDrop(object sender, DragEventArgs e)
        {
            vm.SetResultsPathCommand.Execute(GetFirstFileFromDropPackage(e));
        }

        private void UIElement_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;

            UpdateTbBinding(sender);
        }

        private void UIElement_OnLostFocus(object sender, RoutedEventArgs e)
        {
            UpdateTbBinding(sender);
        }

        private static void UpdateTbBinding(object sender)
        {
            var tBox = (TextBox) sender;

            var be = BindingOperations.GetBindingExpression(tBox, TextBox.TextProperty);
            if (be != null)
            {
                be.UpdateSource();
            }
        }
    }
}
