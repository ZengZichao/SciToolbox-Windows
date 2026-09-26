using System.Windows.Controls;
using System.Windows.Input;
using SciToolbox.Core;
using SciToolbox.ViewModels;

namespace SciToolbox.Views;

public partial class CompareHostControl : UserControl
{
    private sealed class FieldVm { public string Label { get; init; } = ""; public string Value { get; init; } = ""; }
    private sealed class ColumnVm
    {
        public string ToolName { get; init; } = "";
        public string Title { get; init; } = "";
        public List<FieldVm> Fields { get; init; } = new();
        public ICommand RemoveCommand { get; init; } = null!;
        public string RemoveTip { get; init; } = "";
    }

    public CompareHostControl()
    {
        InitializeComponent();
        ComparisonStore.Shared.Changed += () => Dispatcher.Invoke(Refresh);
        Loaded += (_, _) => Refresh();
        // 语言切换后重新生成列内容（对比栏的字段名和"加载中"状态都是本地化文本）
        AppLanguage.Shared.Changed += () => Dispatcher.Invoke(Refresh);
    }

    private void Refresh()
    {
        var cols = new List<ColumnVm>();
        foreach (var item in ComparisonStore.Shared.Items)
        {
            var fields = new List<FieldVm>();
            if (item.Detail != null)
                fields.AddRange(ComparisonStore.FieldsOf(item.Detail, item.ToolName, item.Category)
                    .Select(f => new FieldVm { Label = f.Label, Value = string.IsNullOrEmpty(f.Value) ? "—" : f.Value }));
            else
                fields.Add(new FieldVm { Label = DT.T("状态", "Status"), Value = L10n.T(L10n.Key.LoadingCompare) });

            var captured = item;
            cols.Add(new ColumnVm
            {
                ToolName = item.ToolName,
                Title = item.Detail?.HeaderTitle ?? item.ItemId,
                Fields = fields,
                RemoveTip = L10n.T(L10n.Key.RemoveFromCompare, item.Detail?.HeaderTitle ?? item.ItemId),
                RemoveCommand = new RelayCommand(() => ComparisonStore.Shared.Remove(captured))
            });
        }
        Columns.ItemsSource = cols;
    }
}
